namespace UnlimitedPDF.Models.Internal.Fonts;

/// <summary>
/// Chooses how text drawn without a caller font is written (WR-039). ASCII-only text keeps the document font /F1
/// (object 3), so documents with only ASCII text are written exactly as before. Text with any other character uses the
/// WinAnsi Helvetica resource, where WinAnsi characters (accents, the euro sign, dashes, curly quotes) draw and every
/// other character becomes '?'.
/// </summary>
internal static class StandardText
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether text takes the ASCII path, drawn with /F1.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>True when every character is ASCII.</returns>
    public static bool IsAscii(string text)
    {
        foreach (char c in text)
        {
            if (c > 0x7F) return false;
        }
        return true;
    }

    /// <summary>
    /// Returns the font resource name (without the slash) and the escaped string body for text, recording the WinAnsi
    /// Helvetica resource in the page's sink when the text needs it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sink">The page's draw sink.</param>
    /// <returns>The resource name and the string body to place between parentheses.</returns>
    public static (string Resource, string Literal) Prepare(string text, PageDrawSink sink)
    {
        if (IsAscii(text)) return ("F1", PdfLiteralString.Escape(text));
        return (sink.FontResource(StandardFont.Helvetica), PdfLiteralString.EscapeBytes(WinAnsiEncoding.Encode(text)));
    }

    /// <summary>
    /// Returns the width of text drawn without a caller font, in points.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns>The width.</returns>
    public static double Width(string text, double fontSize) =>
        IsAscii(text) ? HelveticaMetrics.Width(text, fontSize) : StandardFontMetrics.Measure(StandardFont.Helvetica, text, fontSize);

    #endregion

    #endregion
}
