namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents the document catalog, the root object of a PDF's object hierarchy.
/// </summary>
internal sealed class PdfCatalog
{
    public PdfCatalog(PdfIndirectReference pages)
    {
        Pages = pages;
    }

    /// <summary>
    /// Gets or sets the bookmark tree root, or null when the document has no bookmarks (WR-036).
    /// </summary>
    public PdfIndirectReference? Outlines { get; set; }

    /// <summary>
    /// Gets or sets the optional content (layers) dictionary as PDF syntax, or null when the document has no layers
    /// (WR-036).
    /// </summary>
    public string? OptionalContent { get; set; }

    /// <summary>
    /// Gets the indirect reference to the page tree root (<see cref="PdfPages"/> object).
    /// </summary>
    public PdfIndirectReference Pages { get; }

    /// <summary>
    /// Returns the string representation of the PDF catalog dictionary.
    /// </summary>
    /// <returns>A string representing the catalog dictionary.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Catalog");
        sb.AppendLine($"/Pages {Pages}");
        if (Outlines is not null) sb.AppendLine($"/Outlines {Outlines}");
        if (OptionalContent is not null) sb.AppendLine($"/OCProperties {OptionalContent}");
        sb.AppendLine(">>");
        return sb.ToString();
    }
}
