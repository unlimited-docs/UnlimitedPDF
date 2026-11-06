namespace UnlimitedPDF;

/// <summary>
/// The reason a <see cref="PdfImageException"/> was thrown while loading an image.
/// </summary>
public enum PdfImageErrorReason
{
    /// <summary>
    /// The data is empty, is not an image, or is an image format or variant the library does not support.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The data looks like a supported format but is damaged or truncated.
    /// </summary>
    CorruptData,

    /// <summary>
    /// The image has more pixels (width x height) than the pixel limit allows.
    /// </summary>
    TooLarge
}
