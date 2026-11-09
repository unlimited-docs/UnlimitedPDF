namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents a single page dictionary in a PDF document.
/// </summary>
internal class PdfPageDictionary
{
    /// <summary>
    /// Gets or sets the indirect reference to the parent page tree node (<see cref="PdfPages"/> object).
    /// </summary>
    public PdfIndirectReference Parent { get; set; } = default!;

    /// <summary>
    /// Gets or sets the page's dimensions, represented as a PDF MediaBox string (e.g., "[0 0 595 842]"):
    /// </summary>
    public string MediaBox { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the indirect reference to the page's content stream (<see cref="PdfStreamObject"/>).
    /// </summary>
    public PdfIndirectReference Contents { get; set; } = default!;

    /// <summary>
    /// Gets or sets the dictionary of resources for the page, such as fonts.
    /// </summary>
    public PdfDictionary Resources { get; set; } = new PdfDictionary();

    /// <summary>
    /// Gets or sets the syntax of the page's <c>/Annots</c> array for HTML links (WR-038), or null for none. Pages
    /// without links write no entry, exactly as before.
    /// </summary>
    public string? Annots { get; set; }

    /// <summary>
    /// Returns the string representation of the PDF page dictionary.
    /// </summary>
    /// <returns>A string representing the page dictionary.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Page");
        sb.AppendLine($"/Parent {Parent}");

        if (!string.IsNullOrWhiteSpace(MediaBox))
            sb.AppendLine($"/MediaBox {MediaBox}");

        if (Contents is not null)
            sb.AppendLine($"/Contents {Contents}");

        if (Resources is not null)
            sb.AppendLine($"/Resources {Resources}");

        if (Annots is not null)
            sb.AppendLine($"/Annots {Annots}");

        sb.AppendLine(">>");
        return sb.ToString();
    }
}
