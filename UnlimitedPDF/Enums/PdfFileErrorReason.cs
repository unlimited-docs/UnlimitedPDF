namespace UnlimitedPDF;

/// <summary>
/// The reason a <see cref="PdfFileException"/> was thrown while loading an existing PDF.
/// </summary>
public enum PdfFileErrorReason
{
    /// <summary>
    /// The PDF is encrypted, including one that opens without a password but restricts printing or copying. Encrypted
    /// files are not opened.
    /// </summary>
    Encrypted,

    /// <summary>
    /// The data is not a readable PDF: it is truncated, corrupt, or uses a structure the library does not read.
    /// </summary>
    Damaged,

    /// <summary>
    /// The data is empty, or the document has no pages.
    /// </summary>
    Empty,

    /// <summary>
    /// The file is over the size limit, a stream decompresses past the stream size limit, or the file lists more
    /// objects than the object limit.
    /// </summary>
    TooLarge,
}
