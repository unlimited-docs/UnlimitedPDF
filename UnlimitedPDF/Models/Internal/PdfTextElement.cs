namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents a text element to be drawn on a PDF page.
/// </summary>
internal class PdfTextElement
{
    /// <summary>
    /// Gets or sets the font resource name (e.g., "F1").
    /// </summary>
    public string FontName { get; set; } = "F1";

    /// <summary>
    /// Gets or sets the caller font, or null for the built-in font (with the document's fallback fonts, if any).
    /// </summary>
    public PdfFont? Font { get; set; }

    /// <summary>
    /// Gets or sets the font size in points.
    /// </summary>
    public double FontSize { get; set; } = 12;

    /// <summary>
    /// Gets or sets the X coordinate for the text's starting position.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate for the text's starting position.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the text string to be displayed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Returns the PDF content stream command for drawing this text element.
    /// </summary>
    /// <returns>A string representing the text drawing command.</returns>
    /// <summary>
    /// Returns the operators that draw the text, recording in <paramref name="sink"/> the font the text uses.
    /// </summary>
    /// <param name="sink">The page's draw sink.</param>
    /// <returns>The operators.</returns>
    public string ToContent(PageDrawSink sink)
    {
        // BT = Begin Text, ET = End Text
        // Tf = Set font and size
        // Td = Move to start of next line
        // Tj = Show text
        // The text is escaped like every other string the library writes (WR-005); text without a backslash or an
        // unbalanced parenthesis is written unchanged.
        // Caller fonts (the element's, or the document's fallback list) go through the text pipeline (WR-039).
        if (TextPipeline.Layout(Text, Font, sink.EmbeddedFonts.Fallback, FontSize) is { } layout)
            return TextPipeline.Draw(layout, X, Y, FontSize, sink);

        // Text with characters outside ASCII uses the WinAnsi Helvetica resource instead of /F1 (WR-039).
        var (resource, literal) = StandardText.IsAscii(Text) ? (FontName, PdfLiteralString.Escape(Text)) : StandardText.Prepare(Text, sink);
        return $"BT /{resource} {PdfNumber.Format(FontSize)} Tf {PdfNumber.Format(X)} {PdfNumber.Format(Y)} Td ({literal}) Tj ET";
    }
}
