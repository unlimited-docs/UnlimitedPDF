namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents a page tree node, which contains a list of pages or other page tree nodes.
/// </summary>
internal class PdfPages
{
    /// <summary>
    /// Gets or sets the list of indirect references to child pages (<see cref="PdfPageDictionary"/> objects).
    /// </summary>
    public List<PdfIndirectReference> Kids { get; set; } = new List<PdfIndirectReference>();

    /// <summary>
    /// Gets the number of pages in this node.
    /// </summary>
    public int Count => Kids.Count;

    /// <summary>
    /// Returns the string representation of the PDF pages dictionary.
    /// </summary>
    /// <returns>A string representing the pages dictionary.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Pages");
        sb.AppendLine($"/Kids [{string.Join(" ", Kids)}]");
        sb.AppendLine($"/Count {Count}");
        sb.AppendLine(">>");
        return sb.ToString();
    }
}
