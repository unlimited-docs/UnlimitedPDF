namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents a text element to be drawn on a PDF page.
/// </summary>
internal class PdfTextElement
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Appends to <paramref name="sb"/> the operators that draw the text, recording in <paramref name="sink"/> the
    /// font the text uses. No line break follows them.
    /// </summary>
    /// <param name="sb">The page's content.</param>
    /// <param name="sink">The page's draw sink.</param>
    public void ToContent(StringBuilder sb, PageDrawSink sink)
    {
        // BT = Begin Text, ET = End Text
        // Tf = Set font and size
        // Td = Move to start of next line
        // Tj = Show text
        // The text is escaped like every other string the library writes (WR-005); text without a backslash or an
        // unbalanced parenthesis is written unchanged.
        // Caller fonts (the element's, or the document's fallback list) go through the text pipeline (WR-039).
        if (TextPipeline.Layout(Text, Font, sink.EmbeddedFonts.Fallback, FontSize, TextDirections.BaseLevel(Direction, sink.EmbeddedFonts.TextDirection)) is { } layout)
        {
            TextPipeline.Draw(sb, layout, X, Y, FontSize, sink);
            return;
        }

        // Text with characters outside ASCII uses the WinAnsi Helvetica resource instead of /F1 (WR-039).
        var (resource, literal) = StandardText.IsAscii(Text) ? (FontName, PdfLiteralString.Escape(Text)) : StandardText.Prepare(Text, sink);
        sb.Append("BT /").Append(resource).Append(' ').Append(PdfNumber.Format(FontSize)).Append(" Tf ")
          .Append(PdfNumber.Format(X)).Append(' ').Append(PdfNumber.Format(Y)).Append(" Td (").Append(literal).Append(") Tj ET");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the text's own direction, or null for the document's (WR-041).
    /// </summary>
    public PdfTextDirection? Direction { get; set; }

    /// <summary>
    /// Gets or sets the caller font, or null for the built-in font (with the document's fallback fonts, if any).
    /// </summary>
    public PdfFont? Font { get; set; }

    /// <summary>
    /// Gets or sets the font resource name (e.g., "F1").
    /// </summary>
    public string FontName { get; set; } = "F1";

    /// <summary>
    /// Gets or sets the font size in points.
    /// </summary>
    public double FontSize { get; set; } = 12;

    /// <summary>
    /// Gets or sets the text string to be displayed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the X coordinate for the text's starting position.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate for the text's starting position.
    /// </summary>
    public double Y { get; set; }

    #endregion
}
