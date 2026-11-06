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
    /// Gets or sets the font size in points.
    /// </summary>
    public int FontSize { get; set; } = 12;

    /// <summary>
    /// Gets or sets the X coordinate for the text's starting position.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate for the text's starting position.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the text string to be displayed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Returns the PDF content stream command for drawing this text element.
    /// </summary>
    /// <returns>A string representing the text drawing command.</returns>
    public override string ToString()
    {
        // BT = Begin Text, ET = End Text
        // Tf = Set font and size
        // Td = Move to start of next line
        // Tj = Show text
        // The text is escaped like every other string the library writes (WR-005); text without a backslash or an
        // unbalanced parenthesis is written unchanged.
        return $"BT /{FontName} {FontSize} Tf {X} {Y} Td ({PdfLiteralString.Escape(Text)}) Tj ET";
    }
}
