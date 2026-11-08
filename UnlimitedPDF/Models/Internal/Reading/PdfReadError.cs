namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Why reading an existing PDF failed (WR-035). Each value maps onto one public error reason.
/// </summary>
internal enum PdfReadError
{
    /// <summary>
    /// The data is not a readable PDF.
    /// </summary>
    Damaged,

    /// <summary>
    /// The data is empty or the document has no pages.
    /// </summary>
    Empty,

    /// <summary>
    /// The document is encrypted.
    /// </summary>
    Encrypted,

    /// <summary>
    /// The data is over a read limit.
    /// </summary>
    TooLarge,
}
