using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// A box of the layout tree (WR-038): a block container, a table part, or an image.
/// </summary>
internal abstract class HtmlBox
{
    #region Constructors and Finalizers

    protected HtmlBox(ComputedStyle style, HtmlElement? element)
    {
        Style = style;
        Element = element;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the element the box comes from, or null for an anonymous box.
    /// </summary>
    public HtmlElement? Element { get; }

    public ComputedStyle Style { get; }

    #endregion
}

/// <summary>
/// A block container: either block-level children or one run of inline content, never both (anonymous blocks wrap
/// inline content that sits between blocks). A list item also has a marker.
/// </summary>
internal sealed class BlockBox : HtmlBox
{
    #region Constructors and Finalizers

    public BlockBox(ComputedStyle style, HtmlElement? element)
        : base(style, element)
    {
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the block-level children, in order.
    /// </summary>
    public List<HtmlBox> Children { get; } = new();

    /// <summary>
    /// Gets the inline content, in order; empty for a container of blocks.
    /// </summary>
    public List<InlineItem> Inlines { get; } = new();

    /// <summary>
    /// Gets or sets the list marker of a list item, or null.
    /// </summary>
    public ListMarker? Marker { get; set; }

    #endregion
}

/// <summary>
/// An image box: the loaded image, or null when it failed (its alt text is then shown as text).
/// </summary>
internal sealed class HtmlImageBox : HtmlBox
{
    #region Constructors and Finalizers

    public HtmlImageBox(ComputedStyle style, HtmlElement element, PdfImage image)
        : base(style, element)
    {
        Image = image;
    }

    #endregion

    #region Properties, Indexers

    public PdfImage Image { get; }

    #endregion
}

/// <summary>
/// A table and its rows, grouped as header, body, and footer rows, with its caption and column elements.
/// </summary>
internal sealed class TableBox : HtmlBox
{
    #region Constructors and Finalizers

    public TableBox(ComputedStyle style, HtmlElement? element)
        : base(style, element)
    {
    }

    #endregion

    #region Properties, Indexers

    public List<BlockBox> Captions { get; } = new();

    /// <summary>
    /// Gets the column styles in order, one entry per column a <c>col</c> spans.
    /// </summary>
    public List<ComputedStyle> Columns { get; } = new();

    /// <summary>
    /// Gets the rows in display order: header rows first, then body rows, then footer rows.
    /// </summary>
    public List<TableRowBox> Rows { get; } = new();

    /// <summary>
    /// Gets how many leading rows are header rows (from <c>thead</c>), which repeat on each page.
    /// </summary>
    public int HeaderRowCount { get; set; }

    #endregion
}

/// <summary>
/// A table row and its cells.
/// </summary>
internal sealed class TableRowBox : HtmlBox
{
    #region Constructors and Finalizers

    public TableRowBox(ComputedStyle style, HtmlElement? element, ComputedStyle? groupStyle)
        : base(style, element)
    {
        GroupStyle = groupStyle;
    }

    #endregion

    #region Properties, Indexers

    public List<TableCellBox> Cells { get; } = new();

    /// <summary>
    /// Gets the style of the row group (thead, tbody, tfoot) for its background, or null.
    /// </summary>
    public ComputedStyle? GroupStyle { get; }

    #endregion
}

/// <summary>
/// A table cell: its content block and its spans.
/// </summary>
internal sealed class TableCellBox : HtmlBox
{
    #region Constructors and Finalizers

    public TableCellBox(ComputedStyle style, HtmlElement? element, BlockBox content, int columnSpan, int rowSpan)
        : base(style, element)
    {
        Content = content;
        ColumnSpan = columnSpan;
        RowSpan = rowSpan;
    }

    #endregion

    #region Properties, Indexers

    public int ColumnSpan { get; }

    public BlockBox Content { get; }

    public int RowSpan { get; }

    #endregion
}

/// <summary>
/// A list item's marker: a shape (disc, circle, square) or text, drawn outside or inside the item.
/// </summary>
/// <param name="Type">The marker type.</param>
/// <param name="Text">The marker text for numbered types, for example "iv.", else null.</param>
/// <param name="Inside">Whether the marker is inside the item's content.</param>
internal sealed record ListMarker(CssListStyleType Type, string? Text, bool Inside);

/// <summary>
/// One piece of inline content.
/// </summary>
internal abstract class InlineItem
{
    #region Constructors and Finalizers

    protected InlineItem(ComputedStyle style)
    {
        Style = style;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the background of the nearest inline element with one inside the block, or null.
    /// </summary>
    public PdfColor? Background { get; set; }

    /// <summary>
    /// Gets the element ids and headings that start at this item.
    /// </summary>
    public List<HtmlElement> Starts { get; } = new();

    /// <summary>
    /// Gets the link the item is part of: the href as written, or null.
    /// </summary>
    public string? Link { get; set; }

    public ComputedStyle Style { get; }

    #endregion
}

/// <summary>
/// Text with its style; white space is processed during line layout.
/// </summary>
internal sealed class TextItem : InlineItem
{
    #region Constructors and Finalizers

    public TextItem(string text, ComputedStyle style)
        : base(style)
    {
        Text = text;
    }

    #endregion

    #region Properties, Indexers

    public string Text { get; }

    #endregion
}

/// <summary>
/// A forced line break (<c>br</c>).
/// </summary>
internal sealed class BreakItem : InlineItem
{
    #region Constructors and Finalizers

    public BreakItem(ComputedStyle style)
        : base(style)
    {
    }

    #endregion
}

/// <summary>
/// An atomic inline: an image or an inline-block, placed on a line as one piece.
/// </summary>
internal sealed class AtomicItem : InlineItem
{
    #region Constructors and Finalizers

    public AtomicItem(HtmlBox box)
        : base(box.Style)
    {
        Box = box;
    }

    #endregion

    #region Properties, Indexers

    public HtmlBox Box { get; }

    #endregion
}
