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
    /// Copies the pages at <paramref name="pageIndexes"/> (0-based) in that order, a page listed more than once
    /// becoming one page per listing, numbering new objects from <paramref name="firstNumber"/> (WR-036, WR-040).
    /// Links and bookmarks to a repeated page target its first copy; every copy has its own annotations. Links to
    /// pages not listed are removed. With <paramref name="trimResources"/>, each page keeps only the resources its
    /// content uses (<see cref="ResourceTrimmer"/>).
    /// </summary>
    /// <param name="pageIndexes">The indexes of the pages, in the order they are added; not empty.</param>
    /// <param name="firstNumber">The first free object number in the document.</param>
    /// <param name="trimResources">True to copy only the resources the pages use (extract, remove, and page
    /// selections, WR-040); false to copy every resource a page reaches (merge, split, and page runs).</param>
    /// <returns>The objects to add, in number order, and the new page object numbers in the order listed.</returns>
    /// <exception cref="PdfReadException">An object cannot be read (only for a file that changed since it was
    /// checked, which cannot happen through the public API).</exception>
    public ImportedPages Import(IReadOnlyList<int> pageIndexes, int firstNumber, bool trimResources)
    {
        int next = firstNumber;

        // Every listing gets its page object number first, in the order listed. Links and bookmarks target a page's
        // first copy. Copies are made in rounds: round k holds each page's k-th listing, so no round holds a page
        // twice and each round copies its own annotations.
        var pageMap = new Dictionary<int, int>();
        var pageNumbers = new List<int>(pageIndexes.Count);
        var rounds = new List<List<(int Index, int Number)>>();
        var listings = new Dictionary<int, int>();
        foreach (int index in pageIndexes)
        {
            int number = next++;
            pageNumbers.Add(number);
            pageMap.TryAdd(_file.Pages[index].ObjectNumber, number);
            int round = listings.TryGetValue(index, out int seen) ? seen : 0;
            listings[index] = round + 1;
            if (round == rounds.Count) rounds.Add(new List<(int, int)>());
            rounds[round].Add((index, number));
        }

        var imported = new HashSet<int>(pageMap.Keys);
        var shared = new Dictionary<int, int>(_shared);
        var shareable = new Dictionary<int, int>();
        var objects = new List<PdfRawObject>();
        var trimmer = trimResources ? new ResourceTrimmer(_file.Source.Objects) : null;
        for (int i = 0; i < rounds.Count; i++)
        {
            next = CopyRound(rounds[i], imported, pageMap, shared, shareable, objects, next, isRepeat: i > 0, trimmer);
        }

        objects.Sort((a, b) => a.ObjectNumber.CompareTo(b.ObjectNumber));
        return new ImportedPages(objects, pageNumbers, shareable);
    }

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
    public ImportedPages Import(int first, int last, int firstNumber) =>
        Import(Enumerable.Range(first, last - first + 1).ToList(), firstNumber, trimResources: false);

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

    // Copies one round of pages (no page twice) and every object they reach that is not shared yet, numbering from
    // next; returns the next free number. Shareable copies join shared for later rounds and later imports. In a
    // repeat round, each kept annotation's /P names the copy that holds it rather than the page's first copy. With a
    // trimmer, each page's resources become the trimmed copy, and trimmed forms, patterns, Type 3 fonts, and
    // appearances are copied in place of their source objects. A trimmed copy depends only on its source object, so it
    // is shared like any other copy: a page listed again, or imported again later, reuses it (review R-02).
    private int CopyRound(List<(int Index, int Number)> round, HashSet<int> imported, Dictionary<int, int> pageMap,
        Dictionary<int, int> shared, Dictionary<int, int> shareable, List<PdfRawObject> objects, int next, bool isRepeat,
        ResourceTrimmer? trimmer)
    {
        var table = _file.Source.Objects;

        // Annotations decide what each page keeps, and may add content streams that paint form fields.
        int temporary = FirstTemporary;
        var rewrites = new Dictionary<int, PageRewrite>();
        var overrides = new Dictionary<int, PdfReadDictionary>();
        var removed = new HashSet<int>();
        var added = new Dictionary<int, (int Number, byte[] Data)>();
        foreach (var (index, _) in round)
        {
            var rewrite = _annotations.Rewrite(_file.Pages[index], imported, () => temporary--);
            rewrites[index] = rewrite;
            foreach (var (number, value) in rewrite.Overrides) overrides[number] = value;
            removed.UnionWith(rewrite.Removed);
            foreach (var (temporaryNumber, data) in rewrite.NewStreams) added[temporaryNumber] = (next++, data);
        }
        if (trimmer is not null)
        {
            foreach (var (index, _) in round) Trim(_file.Pages[index], rewrites[index], trimmer);
        }
        removed.ExceptWith(overrides.Keys);

        var holders = new Dictionary<int, int>();
        if (isRepeat)
        {
            foreach (var (index, number) in round)
            {
                int page = _file.Pages[index].ObjectNumber;
                foreach (var annotation in rewrites[index].Overrides.Values)
                {
                    if (annotation["P"] is not PdfReadReference { } holder || holder.ObjectNumber != page) continue;
                    int marker = temporary--;
                    holders[marker] = number;
                    annotation.Set("P", new PdfReadReference(marker, 0));
                }
            }
        }

        // Number every object the pages reach that this document does not hold yet. A rewritten annotation is always
        // copied anew, since it depends on which pages are imported and which copy holds it.
        var copied = new Dictionary<int, int>();
        var order = new List<int>();
        var pending = new Stack<PdfReadValue>();
        foreach (var (index, _) in round)
        {
            foreach (var (_, value) in PageEntries(_file.Pages[index], rewrites[index])) pending.Push(value);
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
                    if (shared.ContainsKey(number)) break;
                    var target = Source(number, table, trimmer);
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
            source < 0 ? (added.TryGetValue(source, out var stream) ? stream.Number : holders.TryGetValue(source, out int holder) ? holder : null)
            : removed.Contains(source) ? null
            : pageMap.TryGetValue(source, out int page) ? page
            : copied.TryGetValue(source, out int own) ? own
            : shared.TryGetValue(source, out int sharedCopy) ? sharedCopy
            : null;

        foreach (var (index, number) in round)
            objects.Add(new PdfRawObject(number, PageBody(_file.Pages[index], rewrites[index], Map)));
        foreach (var (number, data) in added.Values)
            objects.Add(new PdfRawObject(number, Encoding.ASCII.GetBytes($"<< /Length {data.Length.ToString(CultureInfo.InvariantCulture)} >>"), data));
        foreach (int source in order)
        {
            bool isRewritten = overrides.TryGetValue(source, out var rewritten);
            PdfReadValue value = isRewritten ? rewritten! : Source(source, table, trimmer);
            objects.Add(Copy(copied[source], value, Map));
            if (!isRewritten && !ReferencesPage(value))
            {
                shareable[source] = copied[source];
                shared[source] = copied[source];
            }
        }
        return next;
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

    // The value copied for a source object: its trimmed copy when the trimmer made one, else the object itself.
    private static PdfReadValue Source(int number, PdfObjectTable table, ResourceTrimmer? trimmer) =>
        trimmer is not null && trimmer.Overrides.TryGetValue(number, out var trimmed) ? trimmed : table.Get(number);

    // Replaces the page's resources with the trimmed copy, unless its content cannot be read.
    private void Trim(PdfSourcePage page, PageRewrite rewrite, ResourceTrimmer trimmer)
    {
        var table = _file.Source.Objects;
        var resources = rewrite.Resources ?? (page.Inherited.TryGetValue("Resources", out var inherited) ? inherited : null);
        var annotations = new List<PdfReadDictionary>();
        if (rewrite.Annots is PdfReadArray kept)
        {
            foreach (var item in kept.Items)
            {
                var annotation = item is PdfReadReference reference && rewrite.Overrides.TryGetValue(reference.ObjectNumber, out var copy)
                    ? copy
                    : table.Resolve(item) as PdfReadDictionary;
                if (annotation is not null) annotations.Add(annotation);
            }
        }

        var trimmed = trimmer.TrimPage(resources, page.Dictionary["Contents"], rewrite.NewStreams.Values, annotations);
        if (!ReferenceEquals(trimmed, resources)) rewrite.Resources = trimmed;
    }

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
