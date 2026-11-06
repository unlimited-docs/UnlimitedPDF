namespace UnlimitedPDF;

/// <summary>
/// A group of body items that moves to the next page as one piece (WR-033). Items stack below each other with their
/// spacing; x offsets are measured from the block's own offset. A table in a block is drawn whole and never splits.
/// Every <c>Add*</c> method here takes the content and its size first, then <c>spacing</c>, then the optional
/// <c>x</c> offset (PR-016).
/// </summary>
public sealed class PdfFlowBlock
{
    #region Fields and Constants

    private readonly FlowBlock _block;

    #endregion

    #region Constructors and Finalizers

    internal PdfFlowBlock(FlowBlock block)
    {
        _block = block;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds an image to the block. Sizes follow <see cref="PdfPage.AddImage"/>.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="width">The width in points, or null.</param>
    /// <param name="height">The height in points, or null.</param>
    /// <param name="spacing">The space above the image, in points; ignored for the block's first item.</param>
    /// <param name="x">The offset from the block's left edge, in points.</param>
    /// <param name="options">Placement options, or null for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size or offset is out of range.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds an image to the block.")]
    public void AddImage(PdfImage image, double? width = null, double? height = null, double spacing = 0, double x = 0, PdfImagePlacementOptions? options = null) =>
        _block.Children.Add(PdfBodyFlow.CreateImage(image, width, height, spacing, x, options, keepWithNext: false));

    /// <summary>
    /// Adds a filled rectangle to the block.
    /// </summary>
    /// <param name="width">The width in points.</param>
    /// <param name="height">The height in points.</param>
    /// <param name="color">The fill color.</param>
    /// <param name="spacing">The space above the rectangle, in points; ignored for the block's first item.</param>
    /// <param name="x">The offset from the block's left edge, in points.</param>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size or offset is out of range.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds a filled rectangle to the block.")]
    public void AddRectangle(double width, double height, PdfColor color, double spacing = 0, double x = 0) =>
        _block.Children.Add(PdfBodyFlow.CreateRectangle(width, height, color, spacing, x, keepWithNext: false));

    /// <summary>
    /// Adds a table to the block. It is drawn whole and never splits.
    /// </summary>
    /// <param name="width">The table width in points.</param>
    /// <param name="spacing">The space above the table, in points; ignored for the block's first item.</param>
    /// <param name="x">The offset from the block's left edge, in points.</param>
    /// <returns>The table, for configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The width, spacing, or offset is out of range.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds a table to the block.")]
    public PdfTable AddTable(double width, double spacing = 0, double x = 0)
    {
        var item = PdfBodyFlow.CreateTable(width, spacing, x, keepWithNext: false);
        _block.Children.Add(item);
        return item.Table;
    }

    /// <summary>
    /// Adds a line of text to the block. The library escapes the text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="spacing">The space above the line, in points; ignored for the block's first item.</param>
    /// <param name="fontSize">The font size in points. The default is 12.</param>
    /// <param name="alignment">The alignment across the body width. The default is left.</param>
    /// <param name="x">The offset used by the alignment, in points.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The spacing, font size, or offset is out of range.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds a line of text to the block.")]
    public void AddText(string text, double spacing = 0, double fontSize = 12, PdfTextAlignment alignment = PdfTextAlignment.Left, double x = 0) =>
        _block.Children.Add(PdfBodyFlow.CreateText(text, spacing, fontSize, alignment, x, keepWithNext: false));

    #endregion

    #endregion
}
