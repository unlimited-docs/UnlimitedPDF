namespace UnlimitedPDF.Models.Internal.Html.Drawing;

/// <summary>
/// Content-stream operators for HTML drawing (WR-038): text runs in the standard fonts, filled boxes, styled lines,
/// clipping, and images. Every operator group saves and restores the graphics state, so text spacing, colors, dash
/// patterns, and clips never leak into other content on the page. Numbers are invariant (PR-006) and text is escaped (A05).
/// </summary>
internal static class HtmlOperators
{
    #region Fields and Constants

    // The horizontal shear of a synthesized italic: tan(12 degrees), rounded.
    private const double ItalicSkew = 0.2126;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the operators that end a clip started by <see cref="ClipBegin"/>.
    /// </summary>
    /// <returns>The operator.</returns>
    public static string ClipEnd() => "Q";

    /// <summary>
    /// Returns the operators that clip the following drawing to a rectangle, until <see cref="ClipEnd"/>.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The operators.</returns>
    public static string ClipBegin(double x, double bottom, double width, double height) =>
        // q = save state, re = rectangle path, W n = use the path as the clip without painting it
        $"q {N(x)} {N(bottom)} {N(Math.Max(0, width))} {N(Math.Max(0, height))} re W n";

    /// <summary>
    /// Returns the operators that fill or stroke a circle, drawn as four Bézier curves.
    /// </summary>
    /// <param name="centerX">The center x.</param>
    /// <param name="centerY">The center y.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The color.</param>
    /// <param name="filled">True to fill, false to stroke a thin outline.</param>
    /// <returns>The operators.</returns>
    public static string Circle(double centerX, double centerY, double radius, PdfColor color, bool filled)
    {
        // k places the control points so the four curves approximate a circle.
        const double k = 0.5523;
        double r = radius, c = radius * k, x = centerX, y = centerY;
        var sb = new StringBuilder("q ");
        sb.Append(Color(color)).Append(filled ? " rg " : " RG ");
        if (!filled) sb.Append(N(Math.Max(0.5, radius / 4))).Append(" w ");
        // m = move, c = Bézier curve, f = fill, S = stroke
        sb.Append(N(x + r)).Append(' ').Append(N(y)).Append(" m ")
          .Append($"{N(x + r)} {N(y + c)} {N(x + c)} {N(y + r)} {N(x)} {N(y + r)} c ")
          .Append($"{N(x - c)} {N(y + r)} {N(x - r)} {N(y + c)} {N(x - r)} {N(y)} c ")
          .Append($"{N(x - r)} {N(y - c)} {N(x - c)} {N(y - r)} {N(x)} {N(y - r)} c ")
          .Append($"{N(x + c)} {N(y - r)} {N(x + r)} {N(y - c)} {N(x + r)} {N(y)} c ")
          .Append(filled ? "f Q" : "S Q");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the operators that fill a rectangle.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The fill color.</param>
    /// <returns>The operators.</returns>
    public static string Fill(double x, double bottom, double width, double height, PdfColor color) =>
        // rg = fill color, re f = fill a rectangle
        $"q {Color(color)} rg {N(x)} {N(bottom)} {N(width)} {N(height)} re f Q";

    /// <summary>
    /// Returns the operators that draw an image XObject into a box.
    /// </summary>
    /// <param name="resourceName">The image resource name, for example "/Im1".</param>
    /// <param name="x">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The operators.</returns>
    public static string Image(string resourceName, double x, double bottom, double width, double height) =>
        // cm = map the image's unit square onto the box, Do = draw the image
        $"q {N(width)} 0 0 {N(height)} {N(x)} {N(bottom)} cm {resourceName} Do Q";

    /// <summary>
    /// Returns the operators that stroke a straight line in a style. A double line is drawn by the caller as two lines.
    /// </summary>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <param name="width">The line width.</param>
    /// <param name="color">The stroke color.</param>
    /// <param name="style">The style; dashed lines use dashes three widths long, dotted lines round dots.</param>
    /// <returns>The operators, or an empty string for no line.</returns>
    public static string Line(double x1, double y1, double x2, double y2, double width, PdfColor color, HtmlBorderStyle style)
    {
        if (style == HtmlBorderStyle.None || width <= 0) return string.Empty;

        // RG = stroke color, w = line width, d = dash pattern, J = line cap (1 = round), m/l = path, S = stroke
        string dash = style switch
        {
            HtmlBorderStyle.Dashed => $"[{N(3 * width)} {N(3 * width)}] 0 d ",
            HtmlBorderStyle.Dotted => $"1 J [0 {N(2 * width)}] 0 d ",
            _ => string.Empty
        };
        return $"q {Color(color)} RG {N(width)} w {dash}{N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q";
    }

    /// <summary>
    /// Returns the operators that draw one run of text in a standard font.
    /// </summary>
    /// <param name="fontResource">The font resource name without the slash, for example "HF3".</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="color">The text color.</param>
    /// <param name="x">The x-coordinate of the run's start.</param>
    /// <param name="baseline">The y-coordinate of the baseline.</param>
    /// <param name="codes">The WinAnsi character codes.</param>
    /// <param name="wordSpacing">Extra space after each space character, for justified text.</param>
    /// <param name="characterSpacing">Extra space after each character, for <c>letter-spacing</c>.</param>
    /// <returns>The operators.</returns>
    public static string Text(string fontResource, double fontSize, PdfColor color, double x, double baseline, IReadOnlyList<byte> codes, double wordSpacing = 0, double characterSpacing = 0)
    {
        // BT/ET = text object, Tf = font and size, rg = fill color, Tw = word spacing, Tc = character spacing,
        // Tm = text position, Tj = show text
        var sb = new StringBuilder();
        sb.Append("q BT /").Append(fontResource).Append(' ').Append(N(fontSize)).Append(" Tf ").Append(Color(color)).Append(" rg ");
        if (wordSpacing != 0) sb.Append(N(wordSpacing)).Append(" Tw ");
        if (characterSpacing != 0) sb.Append(N(characterSpacing)).Append(" Tc ");
        sb.Append("1 0 0 1 ").Append(N(x)).Append(' ').Append(N(baseline)).Append(" Tm (")
          .Append(PdfLiteralString.EscapeBytes(codes)).Append(") Tj ET Q");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the operators that draw one run of text shaped in a caller font (WR-039), recording its glyphs in the
    /// page's sink.
    /// </summary>
    /// <param name="segment">The shaped segment.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="color">The text color.</param>
    /// <param name="x">The x-coordinate of the run's start.</param>
    /// <param name="baseline">The y-coordinate of the baseline.</param>
    /// <param name="sink">The page's draw sink.</param>
    /// <param name="wordSpacing">Extra space after each space, for justified text.</param>
    /// <param name="characterSpacing">Extra space after each cluster, for <c>letter-spacing</c>.</param>
    /// <param name="fakeBold">Whether to synthesize bold: fill and stroke the outlines, the stroke a thirtieth of the
    /// font size wide (D-012).</param>
    /// <param name="fakeItalic">Whether to synthesize italic: slant the text 12 degrees (D-012).</param>
    /// <returns>The operators.</returns>
    public static string ShapedText(FontSegment segment, double fontSize, PdfColor color, double x, double baseline, PageDrawSink sink, double wordSpacing = 0, double characterSpacing = 0, bool fakeBold = false, bool fakeItalic = false)
    {
        // q/Q = save and restore state, BT/ET = text object, rg/RG = fill and stroke color, w = line width, Tr = text
        // render mode (2 = fill, then stroke); the segment sets its font and position
        var sb = new StringBuilder("q ");
        if (fakeBold) sb.Append(Color(color)).Append(" RG ").Append(N(fontSize / 30)).Append(" w ");
        sb.Append("BT ").Append(Color(color)).Append(" rg");
        if (fakeBold) sb.Append(" 2 Tr");
        TextPipeline.DrawShaped(sb, segment, x, baseline, fontSize, sink, characterSpacing, wordSpacing, fakeItalic ? ItalicSkew : 0);
        return sb.Append(" ET Q").ToString();
    }

    #endregion

    #region Private Methods

    private static string Color(PdfColor color) => $"{N(color.R)} {N(color.G)} {N(color.B)}";

    private static string N(double value) => PdfNumber.Format(value);

    #endregion

    #endregion
}
