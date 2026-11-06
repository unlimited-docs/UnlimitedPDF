using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Images demo, with working defaults.
/// </summary>
public sealed class ImagesInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the box height, in points.
    /// </summary>
    public double BoxHeight { get; set; } = 200;

    /// <summary>
    /// Gets or sets the box width, in points.
    /// </summary>
    public double BoxWidth { get; set; } = 300;

    /// <summary>
    /// Gets or sets the built-in image used when nothing is uploaded.
    /// </summary>
    public string BuiltInFile { get; set; } = "dock.jpg";

    /// <summary>
    /// Gets or sets how the image fills its box.
    /// </summary>
    public PdfImageFit Fit { get; set; } = PdfImageFit.Contain;

    /// <summary>
    /// Gets or sets a value indicating whether the image is also shown inside a table cell.
    /// </summary>
    public bool ShowInTableCell { get; set; } = true;

    /// <summary>
    /// Gets or sets the uploaded image, or <see langword="null"/> to use the built-in one.
    /// </summary>
    public PdfImage? Uploaded { get; set; }

    /// <summary>
    /// Gets or sets the box's left edge, in points.
    /// </summary>
    public double X { get; set; } = 50;

    /// <summary>
    /// Gets or sets the box's bottom edge, in points.
    /// </summary>
    public double Y { get; set; } = 500;

    #endregion
}
