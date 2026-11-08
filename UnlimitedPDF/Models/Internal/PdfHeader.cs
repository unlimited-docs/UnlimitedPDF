namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents the header of a PDF file, specifying the PDF version.
/// </summary>
internal sealed class PdfHeader
{
    public PdfHeader(string version = "1.7")
    {
        Version = version;
    }

    /// <summary>
    /// Gets or sets the PDF version the file declares, for example "1.7". A document with imported pages declares the
    /// highest version among its inputs, at least 1.7 (WR-036).
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Returns the formatted PDF header string.
    /// </summary>
    /// <returns>A string in the format "%PDF-{Version}".</returns>
    public override string ToString()
    {
        return $"%PDF-{Version}";
    }
}
