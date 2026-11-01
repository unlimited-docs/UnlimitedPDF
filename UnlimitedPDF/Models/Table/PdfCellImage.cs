namespace UnlimitedPDF.Models.Table;

/// <summary>
/// An image in a table cell's content. The image is scaled down to the cell's padded width, keeping its aspect ratio,
/// and is never enlarged.
/// </summary>
public sealed class PdfCellImage : PdfCellContent
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCellImage"/> class.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    [AddedVersion("0.1.0")]
    [Description("Initializes a new instance of the PdfCellImage class.")]
    public PdfCellImage(PdfImage image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the image.
    /// </summary>
    public PdfImage Image { get; }

    #endregion
}
