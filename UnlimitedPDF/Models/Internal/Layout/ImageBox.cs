namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// Sizes an image placement: with a width and a height the image fills that box (or fits inside it, centered, with
/// <see cref="PdfImageFit.Contain"/>); with one of them the other follows the aspect ratio; with neither the image is
/// drawn at 96 pixels per inch (WR-023).
/// </summary>
internal static class ImageBox
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Computes the box an image placement takes and where the image is drawn inside it.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="width">The requested width, or null.</param>
    /// <param name="height">The requested height, or null.</param>
    /// <param name="options">Placement options, or null.</param>
    /// <returns>The box size, and the drawn size with its offset from the box's bottom-left corner.</returns>
    public static (double BoxWidth, double BoxHeight, double OffsetX, double OffsetY, double DrawWidth, double DrawHeight) Compute(
        PdfImage image,
        double? width,
        double? height,
        PdfImagePlacementOptions? options)
    {
        double pixelWidth = image.PixelWidth;
        double pixelHeight = image.PixelHeight;

        if (width is null && height is null)
        {
            double w = PdfUnits.FromPixels(pixelWidth);
            double h = PdfUnits.FromPixels(pixelHeight);
            return (w, h, 0, 0, w, h);
        }

        if (height is null)
        {
            double h = width!.Value * pixelHeight / pixelWidth;
            return (width.Value, h, 0, 0, width.Value, h);
        }

        if (width is null)
        {
            double w = height.Value * pixelWidth / pixelHeight;
            return (w, height.Value, 0, 0, w, height.Value);
        }

        if ((options?.Fit ?? PdfImageFit.Stretch) == PdfImageFit.Contain)
        {
            // Fit inside the box keeping the aspect ratio, centered.
            double drawWidth = width.Value;
            double drawHeight = drawWidth * pixelHeight / pixelWidth;
            if (drawHeight > height.Value)
            {
                drawHeight = height.Value;
                drawWidth = drawHeight * pixelWidth / pixelHeight;
            }
            return (width.Value, height.Value, (width.Value - drawWidth) / 2, (height.Value - drawHeight) / 2, drawWidth, drawHeight);
        }

        return (width.Value, height.Value, 0, 0, width.Value, height.Value);
    }

    #endregion

    #endregion
}
