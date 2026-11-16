namespace UnlimitedPDF;

/// <summary>
/// The reason a <see cref="PdfFontException"/> was thrown while loading a font.
/// </summary>
public enum PdfFontErrorReason
{
    /// <summary>
    /// The data is empty, is not a font, or is a font format the library does not support, such as a font collection
    /// (<c>.ttc</c>) or a web font (WOFF, WOFF2).
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The data looks like a supported font but is damaged or truncated.
    /// </summary>
    CorruptData,

    /// <summary>
    /// The font data is larger than the file size limit allows.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The font's embedding permissions do not allow it to be embedded in a document (PR-022).
    /// </summary>
    EmbeddingRestricted,

    /// <summary>
    /// The requested weight is not available: the font is not a variable font and has a different weight.
    /// </summary>
    WeightNotAvailable
}
