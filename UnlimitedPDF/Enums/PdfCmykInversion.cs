namespace UnlimitedPDF;

/// <summary>
/// How the values of a CMYK JPEG are interpreted. Some encoders store CMYK values inverted and mark the file
/// with an Adobe segment.
/// </summary>
public enum PdfCmykInversion
{
    /// <summary>
    /// Treat the values as inverted when the JPEG has an Adobe segment, and as normal otherwise.
    /// </summary>
    Auto,

    /// <summary>
    /// Always treat the values as inverted.
    /// </summary>
    Inverted,

    /// <summary>
    /// Never treat the values as inverted.
    /// </summary>
    NotInverted
}
