namespace UnlimitedPDF.Models.Internal.Html.Parsing;

/// <summary>
/// Builds the element tree from tokens with the HTML Standard's error recovery for the elements the library supports
/// (WR-038, AC-19): implied end tags for <c>p</c>, <c>li</c>, <c>dt</c>, <c>dd</c>, headings, and table parts;
/// implied <c>tbody</c> and <c>tr</c>; misnested formatting elements closed and reopened; stray end tags dropped; text
/// outside <c>body</c> moved into it; and text inside table structure moved before the table. Ignored elements (AC-12)
/// are dropped with their content and reported; unknown elements keep their content and are reported once per name.
/// It is iterative and stops with <see cref="PdfHtmlErrorReason.NestingTooDeep"/> when elements nest deeper than the
/// limit (A06).
/// </summary>
internal sealed class HtmlTreeBuilder
{
    #region Fields and Constants

    private static readonly HashSet<string> BlockClosesParagraph = Set(
        "address", "article", "aside", "blockquote", "div", "dl", "fieldset", "figcaption", "figure", "footer", "form",
        "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "main", "nav", "ol", "p", "pre", "section", "table", "ul",
        "li", "dd", "dt", "center", "details", "summary", "menu", "dir", "listing");

    private static readonly HashSet<string> FormattingElements = Set(
        "a", "b", "big", "code", "em", "font", "i", "nobr", "s", "small", "strike", "strong", "tt", "u");

    private static readonly HashSet<string> Headings = Set("h1", "h2", "h3", "h4", "h5", "h6");

    private static readonly HashSet<string> HeadElements = Set("title", "style", "link", "meta", "base");

    // Elements skipped with their content (AC-12).
    private static readonly HashSet<string> IgnoredElements = Set(
        "script", "noscript", "iframe", "object", "embed", "video", "audio", "canvas", "svg", "math", "template",
        "input", "select", "textarea", "button", "option", "optgroup", "datalist", "frameset", "frame", "applet",
        "picture", "source", "track", "param", "map", "area", "dialog", "progress", "meter", "output");

    private static readonly HashSet<string> ScopeBoundaries = Set("html", "table", "td", "th", "caption", "template");

    // The supported elements (AC-05); other elements are kept as inline containers with a warning.
    private static readonly HashSet<string> SupportedElements = Set(
        "html", "head", "body", "title", "style", "link", "meta", "div", "p", "span", "br", "hr",
        "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "address", "section", "article", "header", "footer",
        "main", "nav", "aside", "figure", "figcaption", "b", "strong", "i", "em", "u", "s", "del", "ins", "small",
        "sub", "sup", "code", "kbd", "mark", "a", "ul", "ol", "li", "dl", "dt", "dd", "table", "caption", "thead",
        "tbody", "tfoot", "tr", "th", "td", "colgroup", "col", "img");

    private static readonly HashSet<string> TableContext = Set("table", "tbody", "thead", "tfoot", "tr");

    private static readonly HashSet<string> TableSections = Set("tbody", "thead", "tfoot");

    private static readonly HashSet<string> VoidElements = Set(
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr");

    // Active formatting elements; null entries are markers (table cells and captions).
    private readonly List<HtmlElement?> _formatting = new();
    private readonly int _maxDepth;
    private readonly List<HtmlElement> _stack = new();
    private readonly List<HtmlStyleSource> _styles = new();
    private readonly StringBuilder _styleText = new();
    private readonly HashSet<string> _unknownReported = new(StringComparer.Ordinal);
    private readonly HtmlWarnings _warnings;
    private HtmlElement _body = default!;
    private bool _inBody;
    private HtmlElement _head = default!;
    private HtmlElement _root = default!;
    private HtmlToken? _styleOpen;
    private string? _title;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the builder.
    /// </summary>
    /// <param name="maxDepth">The deepest element nesting allowed.</param>
    /// <param name="warnings">Receives unsupported-element warnings.</param>
    public HtmlTreeBuilder(int maxDepth, HtmlWarnings warnings)
    {
        _maxDepth = maxDepth;
        _warnings = warnings;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the tree.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The tree.</returns>
    /// <exception cref="PdfHtmlException">Elements nest deeper than the limit.</exception>
    public HtmlDocumentTree Build(IEnumerable<HtmlToken> tokens)
    {
        _root = new HtmlElement("html", Array.Empty<KeyValuePair<string, string>>(), 1);
        _head = new HtmlElement("head", Array.Empty<KeyValuePair<string, string>>(), 1);
        _body = new HtmlElement("body", Array.Empty<KeyValuePair<string, string>>(), 1);
        _root.Append(_head);
        _root.Append(_body);
        _stack.Add(_root);

        string? ignoring = null;
        int ignoredDepth = 0;
        string? titleOpen = null;
        foreach (var token in tokens)
        {
            // Inside an ignored element only its own nesting matters.
            if (ignoring is not null)
            {
                if (token.Kind == HtmlTokenKind.StartTag && token.Value == ignoring && !token.SelfClosing) ignoredDepth++;
                else if (token.Kind == HtmlTokenKind.EndTag && token.Value == ignoring && --ignoredDepth == 0) ignoring = null;
                continue;
            }

            switch (token.Kind)
            {
                case HtmlTokenKind.Text:
                    if (titleOpen is not null) { _title = (_title ?? string.Empty) + token.Value; break; }
                    if (_styleOpen is not null) { _styleText.Append(token.Value); break; }
                    InsertText(token);
                    break;
                case HtmlTokenKind.StartTag:
                    if (IgnoredElements.Contains(token.Value))
                    {
                        _warnings.Add(PdfHtmlWarningKind.UnsupportedElement, token.Value, $"The <{token.Value}> element is not supported and was skipped with its content.", token.Line);
                        if (!token.SelfClosing && !VoidElements.Contains(token.Value)) { ignoring = token.Value; ignoredDepth = 1; }
                        break;
                    }
                    if (token.Value == "title") { titleOpen = "title"; break; }
                    if (token.Value == "style")
                    {
                        _styleOpen = token;
                        _styleText.Clear();
                        if (token.SelfClosing) CloseStyle();
                        break;
                    }
                    StartTag(token);
                    break;
                case HtmlTokenKind.EndTag:
                    if (token.Value == "title") { titleOpen = null; break; }
                    if (token.Value == "style") { CloseStyle(); break; }
                    EndTag(token);
                    break;
            }
        }
        if (_styleOpen is not null) CloseStyle();

        RemovePreLeadingNewlines(_body);
        return new HtmlDocumentTree(_root, _body, _styles, _title);
    }

    #endregion

    #region Private Methods

    private static HashSet<string> Set(params string[] names) => new(names, StringComparer.Ordinal);

    private void CloseStyle()
    {
        if (_styleOpen is null) return;
        string? media = _styleOpen.Attributes.FirstOrDefault(a => a.Key == "media").Value;
        _styles.Add(new HtmlStyleSource(_styleText.ToString(), null, media, _styleOpen.Line));
        _styleOpen = null;
    }

    private void EndTag(HtmlToken token)
    {
        string name = token.Value;
        if (name is "html" or "body" or "head") return;
        if (name == "br")
        {
            StartTag(new HtmlToken(HtmlTokenKind.StartTag, "br", token.Line));
            return;
        }

        if (name == "p")
        {
            if (InScope("p")) PopThrough("p");
            return;
        }
        if (Headings.Contains(name))
        {
            int index = FindInScope(Headings);
            if (index >= 0) PopTo(index);
            return;
        }
        if (FormattingElements.Contains(name))
        {
            CloseFormatting(name);
            return;
        }
        if (name is "td" or "th" or "caption")
        {
            if (InScope(name, tableScope: true)) { PopThrough(name); ClearFormattingToMarker(); }
            return;
        }
        if (name is "table" or "tr" or "colgroup" || TableSections.Contains(name))
        {
            if (InScope(name, tableScope: true)) PopThrough(name);
            return;
        }

        // Any other end tag closes the nearest open element of that name, unless a scope boundary comes first.
        if (InScope(name)) PopThrough(name);
    }

    private void CloseFormatting(string name)
    {
        int entry = -1;
        for (int i = _formatting.Count - 1; i >= 0 && _formatting[i] is not null; i--)
        {
            if (_formatting[i]!.Name == name) { entry = i; break; }
        }
        if (entry < 0)
        {
            if (InScope(name)) PopThrough(name);
            return;
        }

        var element = _formatting[entry]!;
        int index = _stack.LastIndexOf(element);
        if (index < 0)
        {
            _formatting.RemoveAt(entry);
            return;
        }
        if (!InScope(name)) return;

        // Close everything opened inside it; formatting elements closed that way stay active and are reopened before
        // the next text (the simple form of the adoption agency).
        _formatting.RemoveAt(entry);
        PopTo(index);
    }

    private void ClearFormattingToMarker()
    {
        while (_formatting.Count > 0)
        {
            var last = _formatting[_formatting.Count - 1];
            _formatting.RemoveAt(_formatting.Count - 1);
            if (last is null) break;
        }
    }

    // Pops table structure until the current node is one of the given elements (or the html root).
    private void ClearTo(params string[] names)
    {
        while (_stack.Count > 1 && Array.IndexOf(names, Current.Name) < 0)
        {
            if (Current.Name is "td" or "th" or "caption") ClearFormattingToMarker();
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    private int FindInScope(HashSet<string> names)
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            if (names.Contains(_stack[i].Name)) return i;
            if (ScopeBoundaries.Contains(_stack[i].Name)) return -1;
        }
        return -1;
    }

    private bool InScope(string name, bool tableScope = false)
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            string current = _stack[i].Name;
            if (current == name) return true;
            if (tableScope ? current is "html" or "table" : ScopeBoundaries.Contains(current)) return false;
        }
        return false;
    }

    private HtmlElement Insert(HtmlToken token, string? name = null)
    {
        var element = new HtmlElement(name ?? token.Value, token.Attributes, token.Line);
        InsertNode(element);
        bool isVoid = VoidElements.Contains(element.Name) || (token.SelfClosing && !SupportedElements.Contains(element.Name));
        if (!isVoid) Push(element);
        return element;
    }

    // Inserts a node at the current position, moving it before the table when the current node is table structure.
    private void InsertNode(HtmlNode node)
    {
        if (TableContext.Contains(Current.Name) && !(node is HtmlElement e && IsTableStructure(e.Name)))
        {
            var table = _stack.LastOrDefault(element => element.Name == "table");
            if (table?.Parent is { } parent)
            {
                parent.InsertBefore(node, table);
                return;
            }
        }
        Current.Append(node);
    }

    private void InsertText(HtmlToken token)
    {
        string text = token.Value;
        bool whitespace = text.All(c => c is ' ' or '\t' or '\n' or '\f');
        if (!_inBody)
        {
            if (whitespace) return;
            EnterBody();
        }
        if (whitespace && TableContext.Contains(Current.Name))
        {
            Current.Append(new HtmlText(text, token.Line));
            return;
        }
        if (!whitespace) ReconstructFormatting();
        InsertNode(new HtmlText(text, token.Line));
    }

    private static bool IsTableStructure(string name) => name is "caption" or "colgroup" or "col" or "tbody" or "thead" or "tfoot" or "tr" or "td" or "th";

    private void EnterBody()
    {
        if (_inBody) return;
        _inBody = true;
        _stack.Add(_body);
    }

    private void PopThrough(string name)
    {
        for (int i = _stack.Count - 1; i > 0; i--)
        {
            if (_stack[i].Name == name)
            {
                PopTo(i);
                return;
            }
        }
    }

    // Removes the element at the index and everything above it, never the html root or the body.
    private void PopTo(int index)
    {
        int keep = _inBody ? 2 : 1;
        while (_stack.Count > Math.Max(index, keep)) _stack.RemoveAt(_stack.Count - 1);
    }

    private void Push(HtmlElement element)
    {
        if (_stack.Count >= _maxDepth)
            throw new PdfHtmlException(PdfHtmlErrorReason.NestingTooDeep,
                string.Format(CultureInfo.InvariantCulture, "Elements are nested deeper than the limit of {0} (line {1}).", _maxDepth, element.Line));
        _stack.Add(element);
    }

    // Reopens formatting elements that were closed implicitly but not by their own end tag, before new content.
    private void ReconstructFormatting()
    {
        int start = _formatting.Count;
        while (start > 0 && _formatting[start - 1] is { } entry && !_stack.Contains(entry)) start--;
        for (int i = start; i < _formatting.Count; i++)
        {
            var old = _formatting[i]!;
            var clone = new HtmlElement(old.Name, old.Attributes, old.Line);
            InsertNode(clone);
            Push(clone);
            _formatting[i] = clone;
        }
    }

    // A pre element's first newline is not content.
    private static void RemovePreLeadingNewlines(HtmlElement root)
    {
        var pending = new Stack<HtmlElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element.Name == "pre" && element.Children.FirstOrDefault() is HtmlText text && text.Text.StartsWith("\n", StringComparison.Ordinal))
                text.Text = text.Text.Substring(1);
            foreach (var child in element.Children.OfType<HtmlElement>()) pending.Push(child);
        }
    }

    private void StartTag(HtmlToken token)
    {
        string name = token.Value;

        if (name == "html")
        {
            Merge(_root, token);
            return;
        }
        if (name == "head") return;
        if (name == "body")
        {
            Merge(_body, token);
            EnterBody();
            return;
        }
        if (name == "link")
        {
            string rel = token.Attributes.FirstOrDefault(a => a.Key == "rel").Value ?? string.Empty;
            string? href = token.Attributes.FirstOrDefault(a => a.Key == "href").Value;
            if (rel.Split(' ', '\t', '\n').Any(r => r.Equals("stylesheet", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(href))
                _styles.Add(new HtmlStyleSource(null, href, token.Attributes.FirstOrDefault(a => a.Key == "media").Value, token.Line));
            return;
        }
        if (name is "meta" or "base")
        {
            if (name == "base") ReportUnknown(token);
            return;
        }
        if (!_inBody) EnterBody();

        if (!SupportedElements.Contains(name)) ReportUnknown(token);

        if (BlockClosesParagraph.Contains(name) && InButtonScope("p")) PopThrough("p");

        switch (name)
        {
            case "li":
                CloseListItem(new[] { "li" }, new[] { "ul", "ol" });
                break;
            case "dt":
            case "dd":
                CloseListItem(new[] { "dt", "dd" }, new[] { "dl" });
                break;
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                if (Headings.Contains(Current.Name)) _stack.RemoveAt(_stack.Count - 1);
                break;
            case "table":
                Insert(token);
                return;
            case "caption":
            case "colgroup":
                if (!InScope("table", tableScope: true)) return; // Table parts outside a table are ignored.
                ClearTo("table");
                Insert(token);
                if (name == "caption") _formatting.Add(null);
                return;
            case "col":
                if (!InScope("table", tableScope: true)) return; // Table parts outside a table are ignored.
                if (Current.Name != "colgroup") ClearTo("table", "colgroup");
                Insert(token);
                return;
            case "tbody":
            case "thead":
            case "tfoot":
                if (!InScope("table", tableScope: true)) return; // Table parts outside a table are ignored.
                ClearTo("table");
                Insert(token);
                return;
            case "tr":
                if (!InScope("table", tableScope: true)) return; // Table parts outside a table are ignored.
                ClearTo("table", "tbody", "thead", "tfoot");
                if (Current.Name == "table") Insert(new HtmlToken(HtmlTokenKind.StartTag, "tbody", token.Line));
                Insert(token);
                return;
            case "td":
            case "th":
                if (!InScope("table", tableScope: true)) return; // Table parts outside a table are ignored.
                ClearTo("table", "tbody", "thead", "tfoot", "tr");
                if (Current.Name == "table") Insert(new HtmlToken(HtmlTokenKind.StartTag, "tbody", token.Line));
                if (Current.Name != "tr") Insert(new HtmlToken(HtmlTokenKind.StartTag, "tr", token.Line));
                Insert(token);
                _formatting.Add(null);
                return;
            case "a":
                for (int i = _formatting.Count - 1; i >= 0 && _formatting[i] is not null; i--)
                {
                    if (_formatting[i]!.Name == "a") { CloseFormatting("a"); break; }
                }
                break;
        }

        if (!BlockClosesParagraph.Contains(name) && !IsTableStructure(name)) ReconstructFormatting();
        var element = Insert(token);
        if (FormattingElements.Contains(name)) _formatting.Add(element);
    }

    private bool InButtonScope(string name) => InScope(name);

    // Closes an open list item of the given names, stopping at its list or any scope boundary.
    private void CloseListItem(string[] items, string[] lists)
    {
        for (int i = _stack.Count - 1; i > 0; i--)
        {
            string current = _stack[i].Name;
            if (Array.IndexOf(items, current) >= 0)
            {
                PopTo(i);
                return;
            }
            if (Array.IndexOf(lists, current) >= 0 || ScopeBoundaries.Contains(current) || current == "body") return;
        }
    }

    private static void Merge(HtmlElement element, HtmlToken token)
    {
        foreach (var attribute in token.Attributes)
        {
            if (!element.Attributes.ContainsKey(attribute.Key)) element.Attributes[attribute.Key] = attribute.Value;
        }
    }

    private void ReportUnknown(HtmlToken token)
    {
        if (_unknownReported.Add(token.Value))
            _warnings.Add(PdfHtmlWarningKind.UnsupportedElement, token.Value, $"The <{token.Value}> element is not supported; its content is kept as inline text.", token.Line);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    // The innermost open element.
    private HtmlElement Current => _stack[_stack.Count - 1];

    #endregion
}
