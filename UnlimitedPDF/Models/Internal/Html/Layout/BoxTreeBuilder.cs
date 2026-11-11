using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// Builds the layout tree from the styled element tree (WR-038): block containers with anonymous blocks around inline
/// runs between blocks, inline content (text, line breaks, images, inline-blocks), list items with their markers and
/// numbers (<c>ol start</c>, <c>reversed</c>, <c>li value</c>), and tables with their row groups, rows, cells, spans,
/// captions, and columns. A failed image becomes its <c>alt</c> text. Elements with <c>display: none</c> are skipped.
/// </summary>
internal sealed class BoxTreeBuilder
{
    #region Fields and Constants

    /// <summary>
    /// The deepest inline-block nesting laid out as inline-blocks. Each level lays its content out on its own, which
    /// takes far more stack than other nesting, so deeper ones are laid out inline (A06): 250 levels overflow a
    /// 1 MB stack.
    /// </summary>
    public const int MaxInlineBlockNesting = 64;

    private readonly HtmlModel _model;
    private int _inlineBlockDepth;

    #endregion

    #region Constructors and Finalizers

    public BoxTreeBuilder(HtmlModel model)
    {
        _model = model;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the first inline-block laid out inline because it is nested deeper than
    /// <see cref="MaxInlineBlockNesting"/>, or null when there is none.
    /// </summary>
    public HtmlElement? FlattenedInlineBlock { get; private set; }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the root block for the document's body.
    /// </summary>
    /// <returns>The root block (the html element, holding the body).</returns>
    public BlockBox Build()
    {
        var root = new BlockBox(Style(_model.Tree.Root), _model.Tree.Root);
        BuildBlockChildren(root, new[] { (HtmlNode)_model.Tree.Body });
        return root;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the marker text of a numbered list type, for example "iv." for 4 in lower-roman.
    /// </summary>
    /// <param name="type">The list type.</param>
    /// <param name="number">The number.</param>
    /// <returns>The text, or null for a shape or none.</returns>
    internal static string? MarkerText(CssListStyleType type, int number) => type switch
    {
        CssListStyleType.Decimal => number.ToString(CultureInfo.InvariantCulture) + ".",
        CssListStyleType.DecimalLeadingZero => (number is >= 0 and < 10 ? "0" : string.Empty) + number.ToString(CultureInfo.InvariantCulture) + ".",
        CssListStyleType.LowerAlpha => Alpha(number, 'a') + ".",
        CssListStyleType.UpperAlpha => Alpha(number, 'A') + ".",
        CssListStyleType.LowerRoman => Roman(number).ToLowerInvariant() + ".",
        CssListStyleType.UpperRoman => Roman(number) + ".",
        _ => null
    };

    #endregion

    #region Private Methods

    private static string Alpha(int number, char first)
    {
        if (number <= 0) return number.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        while (number > 0)
        {
            number--;
            sb.Insert(0, (char)(first + number % 26));
            number /= 26;
        }
        return sb.ToString();
    }

    private static string Roman(int number)
    {
        if (number <= 0 || number >= 4000) return number.ToString(CultureInfo.InvariantCulture);
        var values = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        var symbols = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            while (number >= values[i])
            {
                sb.Append(symbols[i]);
                number -= values[i];
            }
        }
        return sb.ToString();
    }

    private static bool IsBlockLevel(CssDisplay display) => display is CssDisplay.Block or CssDisplay.ListItem or CssDisplay.Table
        or CssDisplay.TableRowGroup or CssDisplay.TableHeaderGroup or CssDisplay.TableFooterGroup or CssDisplay.TableRow
        or CssDisplay.TableCell or CssDisplay.TableCaption;

    // Adds the block-level boxes for a sequence of nodes to a block container, wrapping inline runs in anonymous blocks.
    private void BuildBlockChildren(BlockBox parent, IEnumerable<HtmlNode> nodes)
    {
        BlockBox? anonymous = null;
        var counter = new ListCounter(parent.Element);

        void Flush()
        {
            if (anonymous is not null && anonymous.Inlines.Any(i => i is not TextItem t || t.Text.Any(c => !IsCollapsible(c)) || t.Style.WhiteSpace is CssWhiteSpace.Pre or CssWhiteSpace.PreWrap))
                parent.Children.Add(anonymous);
            anonymous = null;
        }

        foreach (var node in nodes)
        {
            if (node is HtmlElement element && Style(element) is { } style && style.Display != CssDisplay.None && IsBlockLevel(style.Display))
            {
                Flush();
                parent.Children.Add(BuildBlockLevel(element, style, counter));
                continue;
            }

            anonymous ??= new BlockBox(Anonymous(parent.Style), null);
            var promoted = new List<HtmlElement>();
            AddInline(anonymous, node, parent.Style, null, null, promoted);
            foreach (var block in promoted)
            {
                // A block inside an inline element breaks the inline run around it.
                Flush();
                parent.Children.Add(BuildBlockLevel(block, Style(block), counter));
            }
        }
        Flush();
    }

    private HtmlBox BuildBlockLevel(HtmlElement element, ComputedStyle style, ListCounter counter)
    {
        if (style.Display == CssDisplay.Table) return BuildTable(element, style);
        if (style.Display is CssDisplay.TableRowGroup or CssDisplay.TableHeaderGroup or CssDisplay.TableFooterGroup or CssDisplay.TableRow or CssDisplay.TableCell)
        {
            // Table parts outside a table: an anonymous table wraps them.
            var table = new TableBox(Anonymous(style), null);
            AddTablePart(table, element, style, null);
            return table;
        }

        var block = new BlockBox(style, element);
        if (style.Display == CssDisplay.ListItem)
        {
            int number = counter.Next(element);
            string? text = MarkerText(style.ListStyleType, number);
            if (style.ListStyleType != CssListStyleType.None) block.Marker = new ListMarker(style.ListStyleType, text, style.ListStyleInside);
        }

        // An image displayed as a block is a block holding the image on its own line.
        if (element.Name == "img")
        {
            if (_model.Images.TryGetValue(element, out var image)) block.Inlines.Add(new AtomicItem(new HtmlImageBox(style, element, image)));
            else if (element.Attribute("alt") is { Length: > 0 } alt) block.Inlines.Add(new TextItem(alt, style));
            return block;
        }
        BuildBlockChildren(block, element.Children);

        // An inside marker is text at the start of the item's first line, added once here so layout passes and
        // measuring never add it again (F-13).
        if (block.Marker is { Inside: true } inside)
        {
            var target = block;
            while (target.Inlines.Count == 0 && target.Children.FirstOrDefault() is BlockBox first) target = first;
            var marker = new TextItem(inside.Text is { } text ? text + " " : "• ", style);
            if (target.Inlines.Count == 0 && target.Children.Count > 0)
            {
                // The first content is a table or another box without lines: the marker gets a line of its own before
                // it, since a block draws either its lines or its children (F-36).
                var line = new BlockBox(ComputedStyle.Compute(new Dictionary<string, string> { ["display"] = "block" }, target.Style), null);
                line.Inlines.Add(marker);
                target.Children.Insert(0, line);
            }
            else
            {
                target.Inlines.Insert(0, marker);
            }
            block.Marker = null;
        }
        return block;
    }

    private void AddInline(BlockBox block, HtmlNode node, ComputedStyle parentStyle, string? link, PdfColor? background, List<HtmlElement> promoted)
    {
        if (node is HtmlText text)
        {
            if (text.Text.Length > 0) block.Inlines.Add(new TextItem(text.Text, parentStyle) { Link = link, Background = background });
            return;
        }
        if (node is not HtmlElement element) return;
        var style = Style(element);
        if (style.Display == CssDisplay.None) return;
        if (IsBlockLevel(style.Display))
        {
            promoted.Add(element);
            return;
        }

        int start = block.Inlines.Count;
        string? ownLink = element.Name == "a" && element.Attribute("href") is { } href ? href : link;
        var ownBackground = style.BackgroundColor ?? background;

        if (element.Name == "br")
        {
            block.Inlines.Add(new BreakItem(style) { Link = ownLink });
        }
        else if (element.Name == "img")
        {
            if (_model.Images.TryGetValue(element, out var image))
                block.Inlines.Add(new AtomicItem(new HtmlImageBox(style, element, image)) { Link = ownLink });
            else if (element.Attribute("alt") is { Length: > 0 } alt)
                block.Inlines.Add(new TextItem(alt, style) { Link = ownLink, Background = ownBackground });
        }
        else if (style.Display == CssDisplay.InlineBlock && _inlineBlockDepth < MaxInlineBlockNesting)
        {
            var inner = new BlockBox(style, element);
            _inlineBlockDepth++;
            BuildBlockChildren(inner, element.Children);
            _inlineBlockDepth--;
            block.Inlines.Add(new AtomicItem(inner) { Link = ownLink });
        }
        else
        {
            if (style.Display == CssDisplay.InlineBlock) FlattenedInlineBlock ??= element;
            foreach (var child in element.Children) AddInline(block, child, style, ownLink, ownBackground, promoted);
        }

        // Ids and headings start at the element's first inline item; an empty element gets an empty text item.
        if (element.Attribute("id") is not null)
        {
            if (block.Inlines.Count == start) block.Inlines.Add(new TextItem(string.Empty, style));
            block.Inlines[start].Starts.Add(element);
        }
    }

    private static ComputedStyle Anonymous(ComputedStyle parent) => ComputedStyle.Compute(new Dictionary<string, string> { ["display"] = "block" }, parent);

    // Adds a row group, row, or cell to a table, wrapping rows and cells as needed.
    private void AddTablePart(TableBox table, HtmlElement element, ComputedStyle style, ComputedStyle? groupStyle)
    {
        switch (style.Display)
        {
            case CssDisplay.TableHeaderGroup:
            case CssDisplay.TableRowGroup:
            case CssDisplay.TableFooterGroup:
                foreach (var child in element.Children.OfType<HtmlElement>())
                {
                    var childStyle = Style(child);
                    if (childStyle.Display == CssDisplay.None) continue;
                    AddTablePart(table, child, childStyle.Display is CssDisplay.TableCell ? childStyle : childStyle, style);
                }
                break;
            case CssDisplay.TableRow:
                var row = new TableRowBox(style, element, groupStyle);
                foreach (var child in element.Children.OfType<HtmlElement>())
                {
                    var childStyle = Style(child);
                    if (childStyle.Display == CssDisplay.None) continue;
                    row.Cells.Add(BuildCell(child, childStyle));
                }
                table.Rows.Add(row);
                break;
            default:
                var anonymousRow = new TableRowBox(Anonymous(table.Style), null, groupStyle);
                anonymousRow.Cells.Add(BuildCell(element, style));
                table.Rows.Add(anonymousRow);
                break;
        }
    }

    private TableCellBox BuildCell(HtmlElement element, ComputedStyle style)
    {
        var content = new BlockBox(style, element);
        BuildBlockChildren(content, element.Children);
        int Span(string name) => int.TryParse(element.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? Math.Min(n, 1000) : 1;
        return new TableCellBox(style, element, content, Span("colspan"), Span("rowspan"));
    }

    private TableBox BuildTable(HtmlElement element, ComputedStyle style)
    {
        var table = new TableBox(style, element);
        var headers = new List<TableRowBox>();
        var bodies = new List<TableRowBox>();
        var footers = new List<TableRowBox>();

        foreach (var child in element.Children.OfType<HtmlElement>())
        {
            var childStyle = Style(child);
            if (childStyle.Display == CssDisplay.None) continue;
            switch (childStyle.Display)
            {
                case CssDisplay.TableCaption:
                    var caption = new BlockBox(childStyle, child);
                    BuildBlockChildren(caption, child.Children);
                    table.Captions.Add(caption);
                    break;
                case CssDisplay.TableColumnGroup:
                case CssDisplay.TableColumn:
                    AddColumns(table, child, childStyle);
                    break;
                default:
                    var target = new TableBox(style, null);
                    AddTablePart(target, child, childStyle, null);
                    var list = childStyle.Display == CssDisplay.TableHeaderGroup ? headers : childStyle.Display == CssDisplay.TableFooterGroup ? footers : bodies;
                    list.AddRange(target.Rows);
                    break;
            }
        }
        table.Rows.AddRange(headers);
        table.Rows.AddRange(bodies);
        table.Rows.AddRange(footers);
        table.HeaderRowCount = headers.Count;
        return table;
    }

    private void AddColumns(TableBox table, HtmlElement element, ComputedStyle style)
    {
        var columns = element.Children.OfType<HtmlElement>().Where(c => c.Name == "col").ToList();
        if (style.Display == CssDisplay.TableColumnGroup && columns.Count > 0)
        {
            foreach (var column in columns) AddColumns(table, column, Style(column));
            return;
        }
        int span = int.TryParse(element.Attribute("span"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? Math.Min(n, 1000) : 1;
        for (int i = 0; i < span; i++) table.Columns.Add(style);
    }

    private static bool IsCollapsible(char c) => c is ' ' or '\t' or '\n' or '\f' or '\r';

    private ComputedStyle Style(HtmlElement element) => _model.Styles[element];

    #endregion

    #endregion

    #region Nested Types

    // Numbers the list items of one list: ol start and reversed, li value.
    private sealed class ListCounter
    {
        private readonly HtmlElement? _list;
        private readonly bool _reversed;
        private int _next;
        private bool _started;

        public ListCounter(HtmlElement? list)
        {
            _list = list;
            _reversed = list?.Name == "ol" && list.Attribute("reversed") is not null;
        }

        public int Next(HtmlElement item)
        {
            if (!_started)
            {
                _started = true;
                if (_list?.Name == "ol" && int.TryParse(_list.Attribute("start"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)) _next = start;
                else _next = _reversed ? _list!.Children.OfType<HtmlElement>().Count(c => c.Name == "li") : 1;
            }
            if (int.TryParse(item.Attribute("value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) _next = value;
            int number = _next;
            _next += _reversed ? -1 : 1;
            return number;
        }
    }

    #endregion
}
