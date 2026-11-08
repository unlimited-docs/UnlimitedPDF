namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Builds a merged document's bookmarks (WR-036): one top-level bookmark per import, titled by the caller, else the
/// file's title, else its file name, else "Document N", holding the file's own bookmarks. A bookmark whose page was
/// not imported is dropped and its children move up a level; open and closed states are kept; titles are copied as
/// bytes and caller titles are written as UTF-16 when they are not plain ASCII.
/// </summary>
internal static class OutlineBuilder
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the bookmark objects' syntax: the root first, then each bookmark depth first.
    /// </summary>
    /// <param name="imports">The imports in call order.</param>
    /// <param name="numberAt">Returns the object number of the object at an index in the result (0 is the root).</param>
    /// <returns>The objects' syntax, starting with the root.</returns>
    public static List<string> Build(IReadOnlyList<OutlineImport> imports, Func<int, int> numberAt)
    {
        var top = new List<OutlineNode>();
        for (int i = 0; i < imports.Count; i++)
        {
            var import = imports[i];
            var node = new OutlineNode(TitleBytes(import, i + 1), $"[{import.PageMap[import.FirstSourcePage]} 0 R /Fit]", null, open: true);
            node.Children.AddRange(Convert(import));
            top.Add(node);
        }

        // Number the root, then every bookmark depth first.
        int firstNumber = numberAt(0);
        int next = 1;
        var ordered = new List<OutlineNode>();
        void Number(OutlineNode node)
        {
            node.ObjectNumber = numberAt(next++);
            ordered.Add(node);
            foreach (var child in node.Children) Number(child);
        }
        foreach (var node in top) Number(node);

        var objects = new List<string>
        {
            Dictionary(new (string, string)[]
            {
                ("Type", "/Outlines"),
                ("First", $"{top[0].ObjectNumber} 0 R"),
                ("Last", $"{top[top.Count - 1].ObjectNumber} 0 R"),
                ("Count", top.Sum(node => 1 + (node.Open ? Visible(node) : 0)).ToString(CultureInfo.InvariantCulture)),
            }),
        };
        foreach (var node in ordered) objects.Add(Write(node, top, firstNumber));
        return objects;
    }

    #endregion

    #region Private Methods

    private static List<OutlineNode> Convert(OutlineImport import)
    {
        var table = import.Source.Objects;
        var annotations = new AnnotationImporter(import.Source);
        var imported = new HashSet<int>(import.PageMap.Keys);
        var visited = new HashSet<int>();

        List<OutlineNode> Siblings(PdfReadValue? first, int depth)
        {
            var result = new List<OutlineNode>();
            if (depth > PdfParser.MaxDepth) return result;
            var current = first;
            while (current is PdfReadReference reference && visited.Add(reference.ObjectNumber))
            {
                if (table.Resolve(reference) is not PdfReadDictionary item) break;
                var children = Siblings(item["First"], depth + 1);
                string? destination = null;
                string? action = null;
                if (annotations.Destination(item["Dest"], imported) is { } dest)
                {
                    destination = DestinationSyntax(dest, import.PageMap);
                }
                else if (table.Resolve(item["A"]) is PdfReadDictionary a)
                {
                    string kind = (a["S"] as PdfReadName)?.Value ?? "";
                    if (kind == "GoTo" && annotations.Destination(a["D"], imported) is { } goTo) destination = DestinationSyntax(goTo, import.PageMap);
                    else if (kind == "URI" && table.Resolve(a["URI"]) is PdfReadString uri) action = "<< /S /URI /URI " + Hex(uri.Bytes) + " >>";
                }

                bool hasTarget = destination is not null || action is not null;
                bool targetsElsewhere = !hasTarget && (item.ContainsKey("Dest") || item.ContainsKey("A"));
                if (targetsElsewhere || (!hasTarget && children.Count == 0))
                {
                    // The bookmark's page was not imported: its children take its place.
                    result.AddRange(children);
                }
                else
                {
                    var title = table.Resolve(item["Title"]) is PdfReadString text ? text.Bytes : Array.Empty<byte>();
                    bool open = table.Resolve(item["Count"]) is not PdfReadNumber { Value: < 0 };
                    var node = new OutlineNode(title, destination, action, open);
                    node.Children.AddRange(children);
                    result.Add(node);
                }
                current = item["Next"];
            }
            return result;
        }

        return table.Resolve(import.Source.Catalog["Outlines"]) is PdfReadDictionary root ? Siblings(root["First"], 0) : new List<OutlineNode>();
    }

    private static string DestinationSyntax(PdfReadArray destination, IReadOnlyDictionary<int, int> pageMap) =>
        Encoding.ASCII.GetString(PdfValueWriter.Write(destination, number => pageMap.TryGetValue(number, out int page) ? page : null));

    private static string Dictionary(IEnumerable<(string Key, string Value)> entries) =>
        "<< " + string.Join(" ", entries.Select(e => $"/{e.Key} {e.Value}")) + " >>";

    // The list holding the node and the object number of its parent (0 for a top-level bookmark).
    private static List<OutlineNode> FindSiblings(OutlineNode node, List<OutlineNode> top, out int parentNumber)
    {
        parentNumber = 0;
        if (top.Contains(node)) return top;
        var pending = new Stack<OutlineNode>(top);
        while (pending.Count > 0)
        {
            var candidate = pending.Pop();
            if (candidate.Children.Contains(node))
            {
                parentNumber = candidate.ObjectNumber;
                return candidate.Children;
            }
            foreach (var child in candidate.Children) pending.Push(child);
        }
        return top;
    }

    private static string Hex(byte[] bytes)
    {
        var sb = new StringBuilder();
        PdfValueWriter.AppendHex(sb, bytes);
        return sb.ToString();
    }

    // A caller or fallback title as PDF text bytes: ASCII as is, anything else as UTF-16BE with a byte-order mark.
    private static byte[] TextBytes(string text) => text.All(c => c >= 0x20 && c < 0x7F)
        ? Encoding.ASCII.GetBytes(text)
        : new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes(text)).ToArray();

    private static byte[] TitleBytes(OutlineImport import, int position)
    {
        string title = import.BookmarkTitle ?? import.Title ?? import.FileName
            ?? "Document " + position.ToString(CultureInfo.InvariantCulture);
        return TextBytes(title);
    }

    // The number of bookmarks shown under an open bookmark: each child, plus the children of open children.
    private static int Visible(OutlineNode node) => node.Children.Sum(child => 1 + (child.Open ? Visible(child) : 0));

    private static string Write(OutlineNode node, List<OutlineNode> top, int rootNumber)
    {
        var siblings = FindSiblings(node, top, out int parentNumber);
        int parent = parentNumber == 0 ? rootNumber : parentNumber;
        int index = siblings.IndexOf(node);

        var entries = new List<(string, string)> { ("Title", Hex(node.Title)), ("Parent", $"{parent} 0 R") };
        if (index > 0) entries.Add(("Prev", $"{siblings[index - 1].ObjectNumber} 0 R"));
        if (index < siblings.Count - 1) entries.Add(("Next", $"{siblings[index + 1].ObjectNumber} 0 R"));
        if (node.Children.Count > 0)
        {
            int count = Visible(node);
            entries.Add(("First", $"{node.Children[0].ObjectNumber} 0 R"));
            entries.Add(("Last", $"{node.Children[node.Children.Count - 1].ObjectNumber} 0 R"));
            entries.Add(("Count", (node.Open ? count : -count).ToString(CultureInfo.InvariantCulture)));
        }
        if (node.Destination is not null) entries.Add(("Dest", node.Destination));
        if (node.Action is not null) entries.Add(("A", node.Action));
        return Dictionary(entries);
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed class OutlineNode
    {
        public OutlineNode(byte[] title, string? destination, string? action, bool open)
        {
            Title = title;
            Destination = destination;
            Action = action;
            Open = open;
        }

        public string? Action { get; }

        public List<OutlineNode> Children { get; } = new();

        public string? Destination { get; }

        public int ObjectNumber { get; set; }

        public bool Open { get; }

        public byte[] Title { get; }
    }

    #endregion
}

/// <summary>
/// What the bookmark builder needs from one import.
/// </summary>
/// <param name="Source">The imported file's objects.</param>
/// <param name="PageMap">Source page object number to the page's object number in the document, for this import.</param>
/// <param name="FirstSourcePage">The source object number of the import's first page.</param>
/// <param name="BookmarkTitle">The caller's title, or null.</param>
/// <param name="Title">The file's document title, or null.</param>
/// <param name="FileName">The file name of a file loaded from a path, or null.</param>
internal sealed record OutlineImport(
    PdfSourceFile Source,
    IReadOnlyDictionary<int, int> PageMap,
    int FirstSourcePage,
    string? BookmarkTitle,
    string? Title,
    string? FileName);
