namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// Detects an image format from the first bytes of the data.
/// </summary>
internal static class ImageSignature
{
    #region Fields and Constants

    private const string SupportedFormats = "Only JPEG and PNG images are supported.";

    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the format of <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The complete image file bytes.</param>
    /// <returns>The detected format.</returns>
    /// <exception cref="PdfImageException">The data is empty or is not a supported image format.</exception>
    public static ImageFormat Detect(byte[] data)
    {
        if (data.Length == 0)
            throw Unsupported("The image data is empty.");

        if (StartsWith(data, JpegSignature)) return ImageFormat.Jpeg;
        if (StartsWith(data, PngSignature)) return ImageFormat.Png;

        var known = NameUnsupportedFormat(data);
        throw Unsupported(known is null
            ? $"The data is not a recognized image format. {SupportedFormats}"
            : $"The data is a {known} image. {SupportedFormats}");
    }

    #endregion

    #region Private Methods

    private static bool HasAscii(byte[] data, int offset, string text)
    {
        if (data.Length < offset + text.Length) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (data[offset + i] != text[i]) return false;
        }
        return true;
    }

    private static string? NameUnsupportedFormat(byte[] data)
    {
        if (HasAscii(data, 0, "GIF87a") || HasAscii(data, 0, "GIF89a")) return "GIF";
        if (HasAscii(data, 0, "RIFF") && HasAscii(data, 8, "WEBP")) return "WebP";
        if (HasAscii(data, 0, "II*\0") || HasAscii(data, 0, "MM\0*")) return "TIFF";
        if (HasAscii(data, 0, "BM")) return "BMP";
        return null;
    }

    private static bool StartsWith(byte[] data, byte[] signature)
    {
        if (data.Length < signature.Length) return false;
        for (int i = 0; i < signature.Length; i++)
        {
            if (data[i] != signature[i]) return false;
        }
        return true;
    }

    private static PdfImageException Unsupported(string message) =>
        new(PdfImageErrorReason.UnsupportedFormat, message);

    #endregion

    #endregion
}
