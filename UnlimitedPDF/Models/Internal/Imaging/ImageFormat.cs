namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// The image formats the library can load. Formats are internal, so adding one needs no public API change.
/// </summary>
internal enum ImageFormat
{
    /// <summary>
    /// JPEG, embedded without decoding.
    /// </summary>
    Jpeg,

    /// <summary>
    /// PNG.
    /// </summary>
    Png
}
