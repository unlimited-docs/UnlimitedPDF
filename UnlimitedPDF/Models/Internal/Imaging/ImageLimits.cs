namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// Enforces the pixel limit as soon as an image's dimensions are known, before any decoding (A06).
/// </summary>
internal static class ImageLimits
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Throws when <paramref name="width"/> x <paramref name="height"/> exceeds <paramref name="maxPixels"/>.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="maxPixels">The pixel limit for this load.</param>
    /// <exception cref="PdfImageException">The image is over the limit.</exception>
    public static void EnsureWithinLimit(int width, int height, long maxPixels)
    {
        long pixels = (long)width * height;
        if (pixels <= maxPixels) return;

        throw new PdfImageException(
            PdfImageErrorReason.TooLarge,
            string.Format(
                CultureInfo.InvariantCulture,
                "The image is {0} x {1} pixels ({2:N0} pixels), above the limit of {3:N0} pixels. Raise the limit for this load with PdfImageLoadOptions.MaxPixels, or for all loads with PdfImage.DefaultMaxPixels.",
                width,
                height,
                pixels,
                maxPixels));
    }

    #endregion

    #endregion
}
