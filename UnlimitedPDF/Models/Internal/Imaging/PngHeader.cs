namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// The fields of a PNG IHDR chunk the reader uses.
/// </summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="BitDepth">Bits per sample (or per palette index).</param>
/// <param name="ColorType">The color type.</param>
/// <param name="Interlaced">Whether the image uses Adam7 interlacing.</param>
internal sealed record PngHeader(int Width, int Height, int BitDepth, PngColorType ColorType, bool Interlaced);
