namespace UnlimitedPDF.Enums;

/// <summary>
/// How an image fills the box given by a width and a height.
/// </summary>
public enum PdfImageFit
{
    /// <summary>
    /// Stretch the image to fill the box exactly; the aspect ratio may change.
    /// </summary>
    Stretch,

    /// <summary>
    /// Fit the whole image inside the box, keeping its aspect ratio, centered in the box.
    /// </summary>
    Contain
}
