using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF;

/// <summary>
/// The body flow (WR-033): items stack below the previous one with a spacing, starting at the body top of the page they
/// land on. An item that does not fit moves whole to the next page, which the library adds with its header and footer;
/// a table splits by rows and repeats its header rows. Pages need not be added: the flow creates them as content needs
/// them, and <see cref="PdfDocument.AddPage"/> or <see cref="PageBreak"/> starts a new one.
/// </summary>
public sealed class PdfBodyFlow
{
    #region Fields and Constants

    private readonly PdfDocument _document;
    private readonly List<FlowItem> _items = new();
    private double _bottomGap;
    private int? _maxPages;
    private double _topGap;

    #endregion

    #region Constructors and Finalizers

    internal PdfBodyFlow(PdfDocument document)
    {
        _document = document;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a block: a group of items that moves to the next page as one piece. A block taller than the body makes
    /// saving fail.
    /// </summary>
    /// <param name="spacing">The space above the block, in points; dropped when it is the first item on a page.</param>
    /// <param name="keepWithNext">Whether the block stays on the same page as the next item.</param>
    /// <returns>The block, to add items to.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The spacing is negative or not finite.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds a block of items that moves to the next page as one piece.")]
    public PdfFlowBlock AddBlock(double spacing = 0, bool keepWithNext = false)
    {
        Guard.NonNegative(spacing, nameof(spacing));
        var block = new FlowBlock(spacing, 0, keepWithNext);
        Add(block);
        return new PdfFlowBlock(block);
    }

    /// <summary>
    /// Adds an image. Sizes follow <see cref="PdfPageBuilder.AddImage"/>.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="width">The width in points, or null.</param>
    /// <param name="height">The height in points, or null.</param>
    /// <param name="spacing">The space above the image, in points; dropped when it is the first item on a page.</param>
    /// <param name="x">The offset from the body's left edge, in points.</param>
    /// <param name="options">Placement options, or null for the defaults.</param>
    /// <param name="keepWithNext">Whether the image stays on the same page as the next item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, the spacing, or the offset is out of range.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds an image to the body flow.")]
    public void AddImage(PdfImage image, double? width = null, double? height = null, double spacing = 0, double x = 0, PdfImagePlacementOptions? options = null, bool keepWithNext = false) =>
        Add(CreateImage(image, width, height, spacing, x, options, keepWithNext));

    /// <summary>
    /// Adds a filled rectangle, for example a rule or a box.
    /// </summary>
    /// <param name="width">The width in points.</param>
    /// <param name="height">The height in points.</param>
    /// <param name="color">The fill color.</param>
    /// <param name="spacing">The space above the rectangle, in points; dropped when it is the first item on a page.</param>
    /// <param name="x">The offset from the body's left edge, in points.</param>
    /// <param name="keepWithNext">Whether the rectangle stays on the same page as the next item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, the spacing, or the offset is out of range.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds a filled rectangle to the body flow.")]
    public void AddRectangle(double width, double height, PdfColor color, double spacing = 0, double x = 0, bool keepWithNext = false) =>
        Add(CreateRectangle(width, height, color, spacing, x, keepWithNext));

    /// <summary>
    /// Adds a table. It continues row by row on the following pages and repeats its header rows (WR-027).
    /// </summary>
    /// <param name="width">The table width in points.</param>
    /// <param name="spacing">The space above the table, in points; dropped when it is the first item on a page.</param>
    /// <param name="x">The offset from the body's left edge, in points.</param>
    /// <param name="keepWithNext">Whether the table's last row stays on the same page as the next item.</param>
    /// <returns>The table, for configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The width, spacing, or offset is out of range.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds a table to the body flow.")]
    public PdfTable AddTable(double width, double spacing = 0, double x = 0, bool keepWithNext = false)
    {
        var item = CreateTable(width, spacing, x, keepWithNext);
        Add(item);
        return item.Table;
    }

    /// <summary>
    /// Adds a line of text. The library escapes the text. Text does not wrap.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="spacing">The space above the line, in points; dropped when it is the first item on a page.</param>
    /// <param name="fontSize">The font size in points. The default is 12.</param>
    /// <param name="alignment">The alignment across the body width. The default is left.</param>
    /// <param name="x">The offset: from the body's left edge for left alignment, before its right edge for right
    /// alignment, or from its middle for center alignment, in points.</param>
    /// <param name="keepWithNext">Whether the line stays on the same page as the next item, for example a heading.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The spacing, font size, or offset is out of range.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds a line of text to the body flow.")]
    public void AddText(string text, double spacing = 0, double fontSize = 12, PdfTextAlignment alignment = PdfTextAlignment.Left, double x = 0, bool keepWithNext = false) =>
        Add(CreateText(text, spacing, fontSize, alignment, x, keepWithNext));

    /// <summary>
    /// Starts a new page: the next item lands at the body top of a page the library adds.
    /// </summary>
    [AddedVersion("0.1.0")]
    [Description("Starts a new page in the body flow.")]
    public void PageBreak() => Add(new FlowBreak());

    #endregion

    #region Internal Methods

    internal static FlowImage CreateImage(PdfImage image, double? width, double? height, double spacing, double x, PdfImagePlacementOptions? options, bool keepWithNext)
    {
        if (image is null) throw new ArgumentNullException(nameof(image));
        if (width is double w) Guard.Positive(w, nameof(width));
        if (height is double h) Guard.Positive(h, nameof(height));
        Guard.NonNegative(spacing, nameof(spacing));
        Guard.Finite(x, nameof(x));
        return new FlowImage(image, width, height, options, spacing, x, keepWithNext);
    }

    internal static FlowRectangle CreateRectangle(double width, double height, PdfColor color, double spacing, double x, bool keepWithNext)
    {
        if (color is null) throw new ArgumentNullException(nameof(color));
        Guard.Positive(width, nameof(width));
        Guard.Positive(height, nameof(height));
        Guard.NonNegative(spacing, nameof(spacing));
        Guard.Finite(x, nameof(x));
        return new FlowRectangle(width, height, color, spacing, x, keepWithNext);
    }

    internal static FlowTable CreateTable(double width, double spacing, double x, bool keepWithNext)
    {
        Guard.Positive(width, nameof(width));
        Guard.NonNegative(spacing, nameof(spacing));
        Guard.Finite(x, nameof(x));
        return new FlowTable(new PdfTable { Width = width }, spacing, x, keepWithNext);
    }

    internal static FlowText CreateText(string text, double spacing, double fontSize, PdfTextAlignment alignment, double x, bool keepWithNext)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        Guard.NonNegative(spacing, nameof(spacing));
        Guard.Positive(fontSize, nameof(fontSize));
        Guard.Finite(x, nameof(x));
        return new FlowText(text, fontSize, alignment, spacing, x, keepWithNext);
    }

    #endregion

    #region Private Methods

    private void Add(FlowItem item)
    {
        item.Index = _items.Count;
        _document.FlowItemAdding();
        _items.Add(item);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the gap between the footer band and the body, in points. The default is 0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double BottomGap
    {
        get => _bottomGap;
        set
        {
            Guard.NonNegative(value, nameof(value));
            _bottomGap = value;
            _document.LayoutChanged();
        }
    }

    /// <summary>
    /// Gets or sets the most pages the document may have, or null (the default) for no limit. When the content needs
    /// more pages, saving fails before anything is written, naming the items that do not fit and by how much.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int? MaxPages
    {
        get => _maxPages;
        set
        {
            if (value is int v && v < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "The page limit must be at least 1.");
            _maxPages = value;
        }
    }

    /// <summary>
    /// Gets the height, in points, from where the next item would land to the bottom of that page's body. The value
    /// stays exact at save: if anything it depends on later changes the layout (a flow item's height, a table's rows,
    /// a block's items, a band height, a gap, a side margin, or a page rule), saving fails before anything is written.
    /// </summary>
    public double Remaining => _document.QueryRemaining();

    /// <summary>
    /// Gets or sets the gap between the header band and the body, in points. The default is 0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double TopGap
    {
        get => _topGap;
        set
        {
            Guard.NonNegative(value, nameof(value));
            _topGap = value;
            _document.LayoutChanged();
        }
    }

    /// <summary>
    /// Gets the flow items in the order they were added.
    /// </summary>
    internal IReadOnlyList<FlowItem> Items => _items;

    #endregion
}
