namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// One item of the body flow (WR-033), in the order the caller added it.
/// </summary>
internal abstract class FlowItem
{
    #region Constructors and Finalizers

    protected FlowItem(double spacing, double x, bool keepWithNext)
    {
        Spacing = spacing;
        X = x;
        KeepWithNext = keepWithNext;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the item's 0-based position in the flow, set when it is added.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets whether the item stays on the same page as the next item.
    /// </summary>
    public bool KeepWithNext { get; }

    /// <summary>
    /// Gets a short name of the item's kind for error messages, for example "text" or "table".
    /// </summary>
    public abstract string Kind { get; }

    /// <summary>
    /// Gets the item's layout fingerprint recorded by the first <c>Body.RemainingHeight</c> query that measured it, or null.
    /// </summary>
    public string? MeasuredSignature { get; set; }

    /// <summary>
    /// Gets the space above the item, in points, dropped when the item is the first on a page.
    /// </summary>
    public double Spacing { get; }

    /// <summary>
    /// Gets the item's offset from the left edge of its area, in points.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Gets the item's description for error messages, for example "flow item 3 (table)".
    /// </summary>
    public string Description => string.Format(CultureInfo.InvariantCulture, "flow item {0} ({1})", Index + 1, Kind);

    #endregion
}

/// <summary>
/// A line of text in the flow or in a block.
/// </summary>
internal sealed class FlowText : FlowItem
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowText(string text, double fontSize, PdfTextAlignment alignment, double spacing, double x, bool keepWithNext, PdfFont? font = null)
        : base(spacing, x, keepWithNext)
    {
        Text = text;
        FontSize = fontSize;
        Alignment = alignment;
        Font = font;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the alignment across the area width.
    /// </summary>
    public PdfTextAlignment Alignment { get; }

    /// <summary>
    /// Gets the caller font, or null for the built-in font.
    /// </summary>
    public PdfFont? Font { get; }

    /// <summary>
    /// Gets the font size in points.
    /// </summary>
    public double FontSize { get; }

    /// <inheritdoc />
    public override string Kind => "text";

    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; }

    #endregion
}

/// <summary>
/// An image in the flow or in a block.
/// </summary>
internal sealed class FlowImage : FlowItem
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowImage(PdfImage image, double? width, double? height, PdfImagePlacementOptions? options, double spacing, double x, bool keepWithNext)
        : base(spacing, x, keepWithNext)
    {
        Image = image;
        Width = width;
        Height = height;
        Options = options;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the requested height in points, or null.
    /// </summary>
    public double? Height { get; }

    /// <summary>
    /// Gets the image.
    /// </summary>
    public PdfImage Image { get; }

    /// <inheritdoc />
    public override string Kind => "image";

    /// <summary>
    /// Gets the placement options, or null.
    /// </summary>
    public PdfImagePlacementOptions? Options { get; }

    /// <summary>
    /// Gets the requested width in points, or null.
    /// </summary>
    public double? Width { get; }

    #endregion
}

/// <summary>
/// A filled rectangle in the flow or in a block.
/// </summary>
internal sealed class FlowRectangle : FlowItem
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowRectangle(double width, double height, PdfColor color, double spacing, double x, bool keepWithNext)
        : base(spacing, x, keepWithNext)
    {
        Width = width;
        Height = height;
        Color = color;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the fill color.
    /// </summary>
    public PdfColor Color { get; }

    /// <summary>
    /// Gets the height in points.
    /// </summary>
    public double Height { get; }

    /// <inheritdoc />
    public override string Kind => "rectangle";

    /// <summary>
    /// Gets the width in points.
    /// </summary>
    public double Width { get; }

    #endregion
}

/// <summary>
/// A table in the flow (split by rows) or in a block (never split).
/// </summary>
internal sealed class FlowTable : FlowItem
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowTable(PdfTable table, double spacing, double x, bool keepWithNext)
        : base(spacing, x, keepWithNext)
    {
        Table = table;
    }

    #endregion

    #region Properties, Indexers

    /// <inheritdoc />
    public override string Kind => "table";

    /// <summary>
    /// Gets the table.
    /// </summary>
    public PdfTable Table { get; }

    #endregion
}

/// <summary>
/// A group of items that moves to the next page as one piece.
/// </summary>
internal sealed class FlowBlock : FlowItem
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowBlock(double spacing, double x, bool keepWithNext)
        : base(spacing, x, keepWithNext)
    {
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the block's items, in the order they were added.
    /// </summary>
    public List<FlowItem> Children { get; } = new();

    /// <inheritdoc />
    public override string Kind => "block";

    #endregion
}

/// <summary>
/// A page break: the next item starts on a new page.
/// </summary>
internal sealed class FlowBreak : FlowItem
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowBreak()
        : base(0, 0, false)
    {
    }

    #endregion

    #region Properties, Indexers

    /// <inheritdoc />
    public override string Kind => "page break";

    #endregion
}

/// <summary>
/// An item placed on a page: its area's side edges, its top edge, and for a table the part drawn on the page.
/// </summary>
/// <param name="Item">The item.</param>
/// <param name="Left">The left edge of the item's area (the body's left edge), in points.</param>
/// <param name="Right">The right edge of the item's area, in points.</param>
/// <param name="Top">The y-coordinate of the item's top edge, in points (unused for table parts).</param>
/// <param name="Part">The table part drawn on the page, for a table.</param>
/// <param name="Html">The part of laid-out HTML drawn on the page, for HTML.</param>
internal sealed record FlowPlacement(FlowItem Item, double Left, double Right, double Top, TablePart? Part, HtmlPageFragment? Html = null);

/// <summary>
/// HTML in the flow (WR-038): laid out at the body width of the page where it starts and split across pages at line,
/// row, and block boundaries.
/// </summary>
internal sealed class FlowHtml : FlowItem
{
    #region Fields and Constants

    private double? _measuredHeight;
    private double _measuredWidth = double.NaN;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the item.
    /// </summary>
    public FlowHtml(PdfHtml html, double spacing, bool headingBookmarks, bool keepWithNext)
        : base(spacing, 0, keepWithNext)
    {
        Html = html;
        HeadingBookmarks = headingBookmarks;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the height the HTML takes on one page of unlimited height at the layout width.
    /// </summary>
    /// <returns>The height in points.</returns>
    public double MeasuredHeight()
    {
        double width = LayoutWidth ?? 0;
        if (_measuredHeight is null || _measuredWidth != width)
        {
            _measuredHeight = width > 0 ? HtmlLayoutEngine.MeasureHeight(Html.Model, new BoxTreeBuilder(Html.Model).Build(), width) : 0;
            _measuredWidth = width;
        }
        return _measuredHeight.Value;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets whether h1 to h6 headings become bookmarks.
    /// </summary>
    public bool HeadingBookmarks { get; }

    /// <summary>
    /// Gets the HTML.
    /// </summary>
    public PdfHtml Html { get; }

    /// <inheritdoc />
    public override string Kind => "html";

    /// <summary>
    /// Gets or sets the width the HTML is laid out at, set by the planner from the page where it starts.
    /// </summary>
    public double? LayoutWidth { get; set; }

    #endregion
}
