namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// Draws the content of headers, footers, and the body flow: aligned text lines, filled rectangles, and images, at
/// positions given from an area's top edge (WR-031, WR-033, WR-034).
/// </summary>
internal static class LayoutDrawing
{
    #region Fields and Constants

    // The height of a text line relative to its font size, matching the default line height of table cells.
    private const double LineHeightFactor = 1.2;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the operators that fill a rectangle, keeping the fill color inside a saved graphics state.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The fill color.</param>
    /// <returns>The operators.</returns>
    public static string Rectangle(double x, double bottom, double width, double height, PdfColor color) =>
        // q = save state, rg = fill color, re f = fill a rectangle, Q = restore state
        $"q {PdfNumber.Format(color.R)} {PdfNumber.Format(color.G)} {PdfNumber.Format(color.B)} rg " +
        $"{PdfNumber.Format(x)} {PdfNumber.Format(bottom)} {PdfNumber.Format(width)} {PdfNumber.Format(height)} re f Q";

    /// <summary>
    /// Returns the height of a text line, in points.
    /// </summary>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns>The line height.</returns>
    public static double LineHeight(double fontSize) => LineHeightFactor * fontSize;

    /// <summary>
    /// Returns the operators that draw one line of text in an area, aligned within it, with the line's top edge at
    /// <paramref name="top"/>. The text is escaped (WR-031).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="alignment">The alignment within the area.</param>
    /// <param name="left">The area's left edge.</param>
    /// <param name="right">The area's right edge.</param>
    /// <param name="x">The offset: from the left edge (Left), before the right edge (Right), or from the middle (Center).</param>
    /// <param name="top">The y-coordinate of the line's top edge.</param>
    /// <param name="sink">The page's draw sink, which records the font the text uses.</param>
    /// <param name="font">The caller font, or null for the built-in font (with the document's fallback fonts).</param>
    /// <returns>The operators.</returns>
    public static string Text(string text, double fontSize, PdfTextAlignment alignment, double left, double right, double x, double top, PageDrawSink sink, PdfFont? font = null)
    {
        var layout = TextPipeline.Layout(text, font, sink.EmbeddedFonts.Fallback, fontSize);
        double width = layout?.Width ?? StandardText.Width(text, fontSize);
        double textX = alignment switch
        {
            PdfTextAlignment.Right => right - x - width,
            PdfTextAlignment.Center => left + (right - left - width) / 2 + x,
            _ => left + x
        };

        // The line box is centered on its baseline the same way table cells draw text (center - size / 3).
        double baseline = top - LineHeight(fontSize) / 2 - fontSize / 3;

        // BT/ET = text object, Tf = font and size, rg = black fill, Tm = text position, Tj = show text
        if (layout is not null) return TextPipeline.Draw(layout, textX, baseline, fontSize, sink);

        var (resource, literal) = StandardText.Prepare(text, sink);
        return $"BT /{resource} {PdfNumber.Format(fontSize)} Tf 0 0 0 rg 1 0 0 1 {PdfNumber.Format(textX)} {PdfNumber.Format(baseline)} Tm " +
               $"({literal}) Tj ET";
    }

    #endregion

    #endregion
}
