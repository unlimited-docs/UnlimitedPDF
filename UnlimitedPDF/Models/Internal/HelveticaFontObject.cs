namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// The document's first font object (object 3, resource /F1): Helvetica without an encoding entry, used by ASCII text.
/// </summary>
internal sealed class HelveticaFontObject
{
    public HelveticaFontObject(string fontName = "Helvetica")
    {
        FontName = fontName;
    }

    /// <summary>
    /// Gets the name of the font, such as "Helvetica".
    /// </summary>
    public string FontName { get; }

    /// <summary>
    /// Returns the string representation of the PDF font dictionary.
    /// </summary>
    /// <returns>A string representing the font dictionary.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Font");
        sb.AppendLine("/Subtype /Type1");
        sb.AppendLine($"/BaseFont /{FontName}");
        sb.AppendLine(">>");
        return sb.ToString();
    }
}
