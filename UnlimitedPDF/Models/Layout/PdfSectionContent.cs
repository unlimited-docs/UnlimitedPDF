namespace UnlimitedPDF;

/// <summary>
/// Content of a header or footer band: text, images, tables, and filled rectangles placed by offsets from the band's
/// top-left corner, with y increasing downward (WR-031). Content past a band edge is clipped at that edge, and a table
/// in a band never continues onto another page. Every <c>Add*</c> method here takes the position first (<c>x</c>,
/// <c>y</c>), then the size, then the style (PR-016).
/// </summary>
public class PdfSectionContent
{
    #region Fields and Constants

    private readonly List<SectionItem> _items = new();

    #endregion

    #region Constructors and Finalizers

    internal PdfSectionContent()
    {
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a filled rectangle with its top-left corner at the given offsets from the band's top-left corner.
    /// </summary>
    /// <param name="x">The offset from the band's left edge, in points.</param>
    /// <param name="y">The offset of the rectangle's top edge below the band's top edge, in points.</param>
    /// <param name="width">The width in points.</param>
    /// <param name="height">The height in points.</param>
    /// <param name="color">The fill color.</param>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An offset is not finite, or a size is zero, negative, or not finite.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds a filled rectangle to the band at the given offsets from its top-left corner.")]
    public void AddRectangle(double x, double y, double width, double height, PdfColor color)
    {
        if (color is null) throw new ArgumentNullException(nameof(color));
        Guard.Finite(x, nameof(x));
        Guard.Finite(y, nameof(y));
        Guard.Positive(width, nameof(width));
        Guard.Positive(height, nameof(height));
        _items.Add(new SectionRectangle(x, y, width, height, color));
    }

    /// <summary>
    /// Adds an image with its top-left corner at the given offsets from the band's top-left corner. Sizes follow
    /// <see cref="PdfPage.AddImage"/>.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The offset from the band's left edge, in points.</param>
    /// <param name="y">The offset of the image's top edge below the band's top edge, in points.</param>
    /// <param name="width">The width in points, or null.</param>
    /// <param name="height">The height in points, or null.</param>
    /// <param name="options">Placement options, or null for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An offset is not finite, or a size is zero, negative, or not finite.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds an image to the band at the given offsets from its top-left corner.")]
    public void AddImage(PdfImage image, double x, double y, double? width = null, double? height = null, PdfImagePlacementOptions? options = null)
    {
        if (image is null) throw new ArgumentNullException(nameof(image));
        Guard.Finite(x, nameof(x));
        Guard.Finite(y, nameof(y));
        if (width is double w) Guard.Positive(w, nameof(width));
        if (height is double h) Guard.Positive(h, nameof(height));
        _items.Add(new SectionImage(image, x, y, width, height, options));
    }

    /// <summary>
    /// Adds a table with its top-left corner at the given offsets from the band's top-left corner. The table is drawn
    /// whole and clipped to the band; it never continues onto another page.
    /// </summary>
    /// <param name="x">The offset from the band's left edge, in points.</param>
    /// <param name="y">The offset of the table's top edge below the band's top edge, in points.</param>
    /// <param name="width">The table width in points.</param>
    /// <returns>The table, for configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An offset is not finite, or the width is zero, negative, or not finite.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds a table to the band at the given offsets from its top-left corner.")]
    public PdfTable AddTable(double x, double y, double width)
    {
        Guard.Finite(x, nameof(x));
        Guard.Finite(y, nameof(y));
        Guard.Positive(width, nameof(width));
        var table = new PdfTable { X = x, Y = y, Width = width };
        _items.Add(new SectionTable(table));
        return table;
    }

    /// <summary>
    /// Adds a line of text whose top edge is <paramref name="y"/> below the band's top edge. The text may contain
    /// <c>{page}</c> and <c>{pages}</c>, filled at save with the page number and the number of saved pages; <c>{{</c>
    /// and <c>}}</c> write a literal brace. The library escapes the text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="x">The offset: from the band's left edge for left alignment, before its right edge for right
    /// alignment, or from its middle for center alignment, in points.</param>
    /// <param name="y">The offset of the line's top edge below the band's top edge, in points.</param>
    /// <param name="fontSize">The font size in points. The default is 12.</param>
    /// <param name="alignment">The alignment across the band width. The default is left.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An offset is not finite, or the font size is zero, negative, or not finite.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds a line of text to the band at the given offsets from its top-left corner.")]
    public void AddText(string text, double x, double y, double fontSize = 12, PdfTextAlignment alignment = PdfTextAlignment.Left)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        Guard.Finite(x, nameof(x));
        Guard.Finite(y, nameof(y));
        Guard.Positive(fontSize, nameof(fontSize));
        _items.Add(new SectionText(text, x, y, fontSize, alignment));
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the content added so far, in the order it was added.
    /// </summary>
    internal IReadOnlyList<SectionItem> Items => _items;

    #endregion
}
