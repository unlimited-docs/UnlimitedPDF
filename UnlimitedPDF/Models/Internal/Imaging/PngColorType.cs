namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// PNG color types, with their values from the PNG specification.
/// </summary>
internal enum PngColorType
{
    /// <summary>
    /// Grayscale samples.
    /// </summary>
    Gray = 0,

    /// <summary>
    /// Red, green, and blue samples.
    /// </summary>
    Rgb = 2,

    /// <summary>
    /// Indexes into a palette.
    /// </summary>
    Palette = 3,

    /// <summary>
    /// Grayscale and alpha samples.
    /// </summary>
    GrayAlpha = 4,

    /// <summary>
    /// Red, green, blue, and alpha samples.
    /// </summary>
    RgbAlpha = 6
}
