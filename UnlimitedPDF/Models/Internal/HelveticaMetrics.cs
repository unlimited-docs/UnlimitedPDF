namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Character widths of the document's font, Helvetica (WR-004), used to align header, footer, and flow text (WR-034).
/// Table cells keep their width approximation (WR-012) so existing documents keep their bytes.
/// </summary>
internal static class HelveticaMetrics
{
    #region Fields and Constants

    // Advance widths in 1/1000 em for character codes 32 to 126 in the font's standard encoding, from the font vendor's
    // published Helvetica font metrics. Codes 39 and 96 are the right and left single quotes in that encoding.
    private static readonly int[] Widths =
    {
        278, 278, 355, 556, 556, 889, 667, 222, 333, 333, 389, 584, 278, 333, 278, 278, // 32-47
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556, // 48-63
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, // 64-79
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, // 80-95
        222, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, // 96-111
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584       // 112-126
    };

    // Characters outside ASCII are written as '?' (WR-006), so they are measured as '?'.
    private const int QuestionMarkWidth = 556;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the width of <paramref name="text"/> drawn at <paramref name="fontSize"/>, in points.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns>The width in points.</returns>
    public static double Width(string text, double fontSize)
    {
        long units = 0;
        foreach (char c in text)
        {
            if (c >= 32 && c <= 126) units += Widths[c - 32];
            else if (c > 126) units += QuestionMarkWidth;
        }
        return units * fontSize / 1000.0;
    }

    #endregion

    #endregion
}
