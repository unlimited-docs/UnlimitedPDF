namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// The outcome of loading an uploaded image: the image, or a message the visitor can act on.
/// </summary>
public sealed class UploadResult
{
    #region Constructors and Finalizers

    private UploadResult(PdfImage? image, byte[]? bytes, string? error)
    {
        Image = image;
        Bytes = bytes;
        Error = error;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The message to show.</param>
    /// <returns>The result.</returns>
    public static UploadResult Failed(string error) => new(null, null, error);

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="image">The loaded image.</param>
    /// <param name="bytes">The file bytes, used once for a thumbnail and then dropped.</param>
    /// <returns>The result.</returns>
    public static UploadResult Loaded(PdfImage image, byte[] bytes) => new(image, bytes, null);

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the file bytes when loading succeeded.
    /// </summary>
    public byte[]? Bytes { get; }

    /// <summary>
    /// Gets the message to show when loading failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the loaded image when loading succeeded.
    /// </summary>
    public PdfImage? Image { get; }

    #endregion
}
