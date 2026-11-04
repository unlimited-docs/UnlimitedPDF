namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The loaded images a sample uses, looked up by file name, for example <c>images["invoice-logo.png"]</c>.
/// </summary>
public sealed class SampleImages
{
    #region Fields and Constants

    private readonly IReadOnlyDictionary<string, PdfImage> _images;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleImages"/> class.
    /// </summary>
    /// <param name="images">The images by file name.</param>
    public SampleImages(IReadOnlyDictionary<string, PdfImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the image loaded from the given file.
    /// </summary>
    /// <param name="fileName">The image file name, for example "dock.jpg".</param>
    public PdfImage this[string fileName] =>
        _images.TryGetValue(fileName, out var image)
            ? image
            : throw new KeyNotFoundException($"The image '{fileName}' was not loaded for this sample.");

    #endregion
}
