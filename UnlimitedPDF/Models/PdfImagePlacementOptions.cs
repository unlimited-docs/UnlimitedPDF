namespace UnlimitedPDF;

/// <summary>
/// Options for placing an image on a page. New options are added as properties, so existing code keeps compiling.
/// </summary>
public sealed class PdfImagePlacementOptions
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets how the image fills the box when both a width and a height are given. The default is
    /// <see cref="PdfImageFit.Stretch"/>. It has no effect when only one dimension or none is given.
    /// </summary>
    public PdfImageFit Fit { get; set; } = PdfImageFit.Stretch;

    #endregion
}
