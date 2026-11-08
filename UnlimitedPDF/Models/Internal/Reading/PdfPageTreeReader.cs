namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Lists the pages of an existing PDF in order, with the attributes they inherit from the page tree resolved
/// (WR-035), and reads every object the document's pages, bookmarks, destinations, and layers reach, so a damaged
/// object fails at load rather than later.
/// </summary>
internal static class PdfPageTreeReader
{
    #region Fields and Constants

    private static readonly string[] InheritedKeys = { "Resources", "MediaBox", "CropBox", "Rotate" };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the pages of <paramref name="file"/> in reading order.
    /// </summary>
    /// <param name="file">The file that was read.</param>
    /// <returns>The pages.</returns>
    /// <exception cref="PdfReadException">The page tree loops or is invalid, a reached object is damaged, or the
    /// document has no pages.</exception>
    public static List<PdfSourcePage> ReadPages(PdfSourceFile file)
    {
        var table = file.Objects;
        var pages = new List<PdfSourcePage>();
        var visited = new HashSet<int>();
        if (file.Catalog["Pages"] is not PdfReadReference root)
            throw PdfReadException.Damaged("The document catalog has no page tree.");

        Walk(table, root, new Dictionary<string, PdfReadValue>(StringComparer.Ordinal), visited, pages, depth: 0);
        if (pages.Count == 0) throw new PdfReadException(PdfReadError.Empty, "The document has no pages.");

        CheckReachable(file, pages);
        return pages;
    }

    #endregion

    #region Private Methods

    // Reads every object reachable from the pages and from the catalog entries import uses, skipping /Parent links.
    private static void CheckReachable(PdfSourceFile file, List<PdfSourcePage> pages)
    {
        var table = file.Objects;
        var seen = new HashSet<int>();
        var pending = new Stack<PdfReadValue>();
        foreach (var page in pages)
        {
            seen.Add(page.ObjectNumber);
            pending.Push(page.Dictionary);
            foreach (var value in page.Inherited.Values) pending.Push(value);
        }
        foreach (var key in new[] { "Outlines", "Dests", "Names", "OCProperties" })
        {
            if (file.Catalog[key] is { } value) pending.Push(value);
        }

        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case PdfReadReference reference:
                    if (seen.Add(reference.ObjectNumber)) pending.Push(table.Get(reference.ObjectNumber));
                    break;
                case PdfReadArray array:
                    foreach (var item in array.Items) pending.Push(item);
                    break;
                case PdfReadDictionary dictionary:
                    foreach (var key in dictionary.Keys)
                    {
                        if (key != "Parent") pending.Push(dictionary[key]!);
                    }
                    break;
                case PdfReadStream stream:
                    pending.Push(stream.Dictionary);
                    break;
            }
        }
    }

    private static void Walk(
        PdfObjectTable table,
        PdfReadReference node,
        Dictionary<string, PdfReadValue> inherited,
        HashSet<int> visited,
        List<PdfSourcePage> pages,
        int depth)
    {
        if (depth > PdfParser.MaxDepth) throw PdfReadException.Damaged("The page tree is nested too deeply.");
        if (!visited.Add(node.ObjectNumber)) throw PdfReadException.Damaged("The page tree refers back to itself.");
        if (table.Get(node.ObjectNumber) is not PdfReadDictionary dictionary)
            throw PdfReadException.Damaged($"Page tree object {node.ObjectNumber} is not a dictionary.");

        var own = new Dictionary<string, PdfReadValue>(inherited, StringComparer.Ordinal);
        foreach (var key in InheritedKeys)
        {
            if (dictionary[key] is { } value && value is not PdfReadNull) own[key] = value;
        }

        bool isNode = dictionary["Type"] is PdfReadName { Value: "Pages" }
            || (dictionary["Type"] is not PdfReadName { Value: "Page" } && dictionary.ContainsKey("Kids"));
        if (!isNode)
        {
            pages.Add(new PdfSourcePage(node.ObjectNumber, dictionary, own));
            return;
        }

        if (table.Resolve(dictionary["Kids"]) is not PdfReadArray kids)
            throw PdfReadException.Damaged($"Page tree node {node.ObjectNumber} has no /Kids.");
        foreach (var kid in kids.Items)
        {
            if (kid is not PdfReadReference reference)
                throw PdfReadException.Damaged($"Page tree node {node.ObjectNumber} lists a page that is not a reference.");
            Walk(table, reference, own, visited, pages, depth + 1);
        }
    }

    #endregion

    #endregion
}

/// <summary>
/// One page of an existing PDF.
/// </summary>
/// <param name="ObjectNumber">The page object's number in its file.</param>
/// <param name="Dictionary">The page dictionary.</param>
/// <param name="Inherited">The page's own or inherited <c>/Resources</c>, <c>/MediaBox</c>, <c>/CropBox</c>, and
/// <c>/Rotate</c>, as written (possibly references).</param>
internal sealed record PdfSourcePage(int ObjectNumber, PdfReadDictionary Dictionary, IReadOnlyDictionary<string, PdfReadValue> Inherited);
