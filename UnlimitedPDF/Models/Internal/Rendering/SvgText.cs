namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Writes numbers, colors, and escaped text into SVG markup. Every value taken from a PDF reaches the drawing through
/// these methods, so PDF content can never become markup (A05).
/// </summary>
internal static class SvgText
{
    #region Fields and Constants

    /// <summary>
    /// The largest number written to a drawing: a hundred million points, far beyond any real page.
    /// </summary>
    public const double MaxMagnitude = 1e8;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Appends <paramref name="text"/> escaped for an XML attribute value or element text, dropping characters XML
    /// cannot hold.
    /// </summary>
    /// <param name="builder">The markup being written.</param>
    /// <param name="text">The text, as taken from the PDF.</param>
    public static void AppendEscaped(StringBuilder builder, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&#39;"); break;
                default:
                    if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        builder.Append(c).Append(text[++i]);
                    }
                    else if (IsXmlCharacter(c))
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Appends an RGB color with components from 0 to 1 as <c>#rrggbb</c>.
    /// </summary>
    public static void AppendColor(StringBuilder builder, double red, double green, double blue)
    {
        builder.Append('#').Append(Channel(red).ToString("x2", CultureInfo.InvariantCulture))
            .Append(Channel(green).ToString("x2", CultureInfo.InvariantCulture))
            .Append(Channel(blue).ToString("x2", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Appends a matrix as an SVG <c>matrix(...)</c> transform.
    /// </summary>
    public static void AppendMatrix(StringBuilder builder, RenderMatrix m)
    {
        builder.Append("matrix(").Append(Number(m.A)).Append(' ').Append(Number(m.B)).Append(' ')
            .Append(Number(m.C)).Append(' ').Append(Number(m.D)).Append(' ')
            .Append(Number(m.E)).Append(' ').Append(Number(m.F)).Append(')');
    }

    /// <summary>
    /// Formats a number for SVG with up to ten decimals, without exponents: enough that scale terms as small as a
    /// hundred-thousandth keep their value, so a drawing is never collapsed or resized by rounding. Numbers beyond
    /// <see cref="MaxMagnitude"/> stop the page, so one number can't make the drawing huge.
    /// </summary>
    public static string Number(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > MaxMagnitude) throw new PageRenderException("The page has a number out of range.");
        string text = value.ToString("0.##########", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    #endregion

    #region Private Methods

    private static int Channel(double value) => (int)Math.Round(Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1) * 255);

    // XML 1.0 allows tab, line feed, carriage return, and characters from U+0020, except surrogates and U+FFFE/U+FFFF.
    private static bool IsXmlCharacter(char c) => c is '\t' or '\n' or '\r'
        || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD);

    #endregion

    #endregion
}
