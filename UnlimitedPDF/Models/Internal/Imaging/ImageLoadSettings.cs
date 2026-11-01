namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// The load settings captured when a load starts.
/// </summary>
/// <param name="MaxPixels">The pixel limit (width x height) for this load.</param>
/// <param name="CmykInversion">How CMYK JPEG values are interpreted.</param>
/// <param name="ApplyExifOrientation">Whether a JPEG's EXIF orientation is honored.</param>
internal sealed record ImageLoadSettings(long MaxPixels, PdfCmykInversion CmykInversion, bool ApplyExifOrientation);
