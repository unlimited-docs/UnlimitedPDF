namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Copies pages of an existing PDF into a document (WR-036): every object a page reaches, renumbered from a given
/// object number, and the page dictionary rewritten to hang under the document's page tree with its inherited
/// attributes resolved. Nothing is added to the document here; the caller commits the result in one step.
/// </summary>
internal sealed class PageImporter
{
    #region Fields and Constants

    // Page keys that are never copied: the tree link (rewritten), and the article beads, tagging, page actions,
    // metadata, private data, and tab order that merged pages drop (WR-036).
    private static readonly HashSet<string> DroppedPageKeys = new(StringComparer.Ordinal)
    {
        "Parent", "B", "StructParents", "AA", "Metadata", "PieceInfo", "Tabs",
    };

    private static readonly string[] InheritedKeys = { "MediaBox", "CropBox", "Rotate", "Resources" };

    // Temporary numbers for new objects start below -1, which stands for the document's page tree.
    private const int FirstTemporary = -10;

    private readonly AnnotationImporter _annotations;
    private readonly PdfFile _file;
    private readonly IReadOnlyDictionary<int, int> _shared;
    private readonly HashSet<int> _sourcePages;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates an importer for <paramref name="file"/>.
    /// </summary>
    /// <param name="file">The file to copy pages from.</param>
    /// <param name="shared">Objects of this file already copied into the document by an earlier import, which are
    /// reused (for example a font shared by every page).</param>
    public PageImporter(PdfFile file, IReadOnlyDictionary<int, int> shared)
    {
        _file = file;
        _shared = shared;
        _sourcePages = new HashSet<int>(file.Pages.Select(page => page.ObjectNumber));
        _annotations = new AnnotationImporter(file.Source);
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Copies pages <paramref name="first"/> to <paramref name="last"/> (0-based, inclusive), numbering new objects
    /// from <paramref name="firstNumber"/>.
    /// </summary>
    /// <param name="first">The index of the first page.</param>
    /// <param name="last">The index of the last page.</param>
    /// <param name="firstNumber">The first free object number in the document.</param>
    /// <returns>The objects to add, in number order, and the new page object numbers.</returns>
    /// <exception cref="PdfReadException">An object cannot be read (only for a file that changed since it was
    /// checked, which cannot happen through the public API).</exception>
    public ImportedPages Import(int first, int last, int firstNumber)
    {
        var table = _file.Source.Objects;
        int next = firstNumber;
        var pageMap = new Dictionary<int, int>();
        for (int i = first; i <= last; i++) pageMap[_file.Pages[i].ObjectNumber] = next++;

        // Annotations decide what each page keeps, and may add content streams that paint form fields.
        int temporary = FirstTemporary;
        var imported = new HashSet<int>(pageMap.Keys);
        var rewrites = new Dictionary<int, PageRewrite>();
        var overrides = new Dictionary<int, PdfReadDictionary>();
        var removed = new HashSet<int>();
        var added = new Dictionary<int, (int Number, byte[] Data)>();
        for (int i = first; i <= last; i++)
        {
            var rewrite = _annotations.Rewrite(_file.Pages[i], imported, () => temporary--);
            rewrites[i] = rewrite;
            foreach (var (number, value) in rewrite.Overrides) overrides[number] = value;
            removed.UnionWith(rewrite.Removed);
            foreach (var (temporaryNumber, data) in rewrite.NewStreams) added[temporaryNumber] = (next++, data);
        }
        removed.ExceptWith(overrides.Keys);

        // Number every object the pages reach that this document does not hold yet. A rewritten annotation is always
        // copied anew, since it depends on which pages this call imports.
        var copied = new Dictionary<int, int>();
        var order = new List<int>();
        var pending = new Stack<PdfReadValue>();
        for (int i = first; i <= last; i++)
        {
            foreach (var (_, value) in PageEntries(_file.Pages[i], rewrites[i])) pending.Push(value);
        }
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case PdfReadReference reference:
                    int number = reference.ObjectNumber;
                    if (number < 0 || removed.Contains(number) || _sourcePages.Contains(number) || copied.ContainsKey(number)) break;
                    if (overrides.TryGetValue(number, out var rewritten))
                    {
                        copied[number] = next++;
                        order.Add(number);
                        pending.Push(rewritten);
                        break;
                    }
                    if (_shared.ContainsKey(number)) break;
                    var target = table.Get(number);
                    if (IsPageTreeNode(target)) break;
                    copied[number] = next++;
                    order.Add(number);
                    pending.Push(target);
                    break;
                case PdfReadArray array:
                    foreach (var item in array.Items) pending.Push(item);
                    break;
                case PdfReadDictionary dictionary:
                    foreach (var key in dictionary.Keys) pending.Push(dictionary[key]!);
                    break;
                case PdfReadStream stream:
                    pending.Push(stream.Dictionary);
                    break;
            }
        }

        int? Map(int source) =>
            source < 0 ? (added.TryGetValue(source, out var stream) ? stream.Number : null)
            : removed.Contains(source) ? null
            : pageMap.TryGetValue(source, out int page) ? page
            : copied.TryGetValue(source, out int own) ? own
            : _shared.TryGetValue(source, out int shared) ? shared
            : null;

        var objects = new List<PdfRawObject>();
        for (int i = first; i <= last; i++)
            objects.Add(new PdfRawObject(pageMap[_file.Pages[i].ObjectNumber], PageBody(_file.Pages[i], rewrites[i], Map)));
        foreach (var (number, data) in added.Values)
            objects.Add(new PdfRawObject(number, Encoding.ASCII.GetBytes($"<< /Length {data.Length.ToString(CultureInfo.InvariantCulture)} >>"), data));

        var shareable = new Dictionary<int, int>();
        foreach (int source in order)
        {
            bool isRewritten = overrides.TryGetValue(source, out var rewritten);
            PdfReadValue value = isRewritten ? rewritten! : table.Get(source);
            objects.Add(Copy(copied[source], value, Map));
            if (!isRewritten && !ReferencesPage(value)) shareable[source] = copied[source];
        }

        objects.Sort((a, b) => a.ObjectNumber.CompareTo(b.ObjectNumber));
        return new ImportedPages(objects, pageMap.Values.OrderBy(n => n).ToList(), shareable);
    }

    #endregion

    #region Private Methods

    private static PdfRawObject Copy(int number, PdfReadValue value, Func<int, int?> map)
    {
        if (value is not PdfReadStream stream) return new PdfRawObject(number, PdfValueWriter.Write(value, map));

        // The stored data is copied unchanged, so its length is written as a direct number.
        byte[] data = stream.Data.ToArray();
        var entries = stream.Dictionary.Keys
            .Where(key => key != "Length")
            .Select(key => (key, stream.Dictionary[key]!))
            .Append(("Length", (PdfReadValue)new PdfReadNumber(data.Length.ToString(CultureInfo.InvariantCulture), isInteger: true)));
        var sb = new StringBuilder();
        PdfValueWriter.AppendDictionary(sb, entries, map);
        return new PdfRawObject(number, Encoding.ASCII.GetBytes(sb.ToString()), data);
    }

    private static bool IsPageTreeNode(PdfReadValue value) =>
        value is PdfReadDictionary dictionary && dictionary["Type"] is PdfReadName { Value: "Pages" or "Page" };

    // The page dictionary as it is copied: type and tree link first, then the inherited attributes, then the page's
    // other entries in their written order.
    private static byte[] PageBody(PdfSourcePage page, PageRewrite rewrite, Func<int, int?> map)
    {
        var entries = new List<(string, PdfReadValue)>
        {
            ("Type", new PdfReadName("Page")),
            ("Parent", new PdfReadReference(-1, 0)),
        };
        entries.AddRange(PageEntries(page, rewrite));

        var sb = new StringBuilder();
        PdfValueWriter.AppendDictionary(sb, entries, number => number == -1 ? 2 : map(number));
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    // The page's inherited attributes, then its other entries, without the keys merged pages drop, and with the
    // annotations, content, and resources the rewrite changes.
    private static IEnumerable<(string Key, PdfReadValue Value)> PageEntries(PdfSourcePage page, PageRewrite rewrite)
    {
        foreach (var key in InheritedKeys)
        {
            if (key == "Resources" && rewrite.Resources is { } resources) yield return (key, resources);
            else if (page.Inherited.TryGetValue(key, out var value)) yield return (key, value);
        }
        foreach (var key in page.Dictionary.Keys)
        {
            if (key == "Type" || DroppedPageKeys.Contains(key) || Array.IndexOf(InheritedKeys, key) >= 0) continue;
            if (key == "Annots")
            {
                if (rewrite.Annots is { } annotations) yield return (key, annotations);
            }
            else if (key == "Contents" && rewrite.Contents is { } contents)
            {
                yield return (key, contents);
            }
            else
            {
                yield return (key, page.Dictionary[key]!);
            }
        }
        if (!page.Dictionary.ContainsKey("Contents") && rewrite.Contents is { } added) yield return ("Contents", added);
    }

    // Whether the value itself holds a page reference, so its copy depends on which pages this import maps.
    private bool ReferencesPage(PdfReadValue value) => value switch
    {
        PdfReadReference reference => _sourcePages.Contains(reference.ObjectNumber),
        PdfReadArray array => array.Items.Any(ReferencesPage),
        PdfReadDictionary dictionary => dictionary.Keys.Any(key => ReferencesPage(dictionary[key]!)),
        PdfReadStream stream => ReferencesPage(stream.Dictionary),
        _ => false,
    };

    #endregion

    #endregion
}

/// <summary>
/// The result of one import: objects to add in number order, the new page objects, and the copies a later import of
/// the same file may reuse.
/// </summary>
/// <param name="Objects">Every new object, ascending by number.</param>
/// <param name="PageNumbers">The new page objects' numbers, in page order.</param>
/// <param name="Shareable">Source object number to new number for copies that hold no page reference.</param>
internal sealed record ImportedPages(List<PdfRawObject> Objects, List<int> PageNumbers, Dictionary<int, int> Shareable);
