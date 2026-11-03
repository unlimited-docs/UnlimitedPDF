namespace UnlimitedPDF;

/// <summary>
/// A JPEG or PNG image, loaded and checked once, that can be placed any number of times in any number of
/// documents. A <see cref="PdfImage"/> is immutable and thread-safe.
/// </summary>
/// <remarks>
/// Loading copies the input: a stream is read from its current position to the end and left open, and a byte
/// array is copied, so later changes by the caller have no effect. Invalid input fails at load: null arguments
/// and file errors throw the standard .NET exceptions, and unsupported, damaged, or oversized images throw
/// <see cref="PdfImageException"/>.
/// </remarks>
public sealed class PdfImage
{
    #region Fields and Constants

    private const long BuiltInDefaultMaxPixels = 100_000_000;
    private static long _defaultMaxPixels = BuiltInDefaultMaxPixels;

    #endregion

    #region Constructors and Finalizers

    private PdfImage(PdfImageData data)
    {
        Data = data;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads an image from a byte array. The array is copied.
    /// </summary>
    /// <param name="data">The JPEG or PNG file bytes.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <returns>The loaded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="PdfImageException">The data is empty, unsupported, damaged, or over the pixel limit.</exception>
    [AddedVersion("0.1.0")]
    [Description("Loads an image from a byte array. The array is copied.")]
    public static PdfImage FromBytes(byte[] data, PdfImageLoadOptions? options = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        var settings = CaptureSettings(options);
        return Load(data.ToArray(), settings, CancellationToken.None);
    }

    /// <summary>
    /// Loads an image from a file.
    /// </summary>
    /// <param name="path">The path of a JPEG or PNG file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <returns>The loaded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="PdfImageException">The file is empty, unsupported, damaged, or over the pixel limit.</exception>
    [AddedVersion("0.1.0")]
    [Description("Loads an image from a file.")]
    public static PdfImage FromFile(string path, PdfImageLoadOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var settings = CaptureSettings(options);
        return Load(File.ReadAllBytes(path), settings, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously loads an image from a file.
    /// </summary>
    /// <param name="path">The path of a JPEG or PNG file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>The loaded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="PdfImageException">The file is empty, unsupported, damaged, or over the pixel limit.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled; no image is produced.</exception>
    [AddedVersion("0.1.0")]
    [Description("Asynchronously loads an image from a file.")]
    public static async Task<PdfImage> FromFileAsync(string path, PdfImageLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var settings = CaptureSettings(options);
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        return Load(data, settings, cancellationToken);
    }

    /// <summary>
    /// Loads an image from a stream, reading from its current position to the end. The stream is left open and
    /// does not need to be seekable.
    /// </summary>
    /// <param name="stream">A stream holding a JPEG or PNG file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <returns>The loaded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfImageException">The data is empty, unsupported, damaged, or over the pixel limit.</exception>
    [AddedVersion("0.1.0")]
    [Description("Loads an image from a stream, reading from its current position to the end.")]
    public static PdfImage FromStream(Stream stream, PdfImageLoadOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var settings = CaptureSettings(options);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Load(buffer.ToArray(), settings, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously loads an image from a stream, reading from its current position to the end. The stream is
    /// left open and does not need to be seekable.
    /// </summary>
    /// <param name="stream">A stream holding a JPEG or PNG file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>The loaded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfImageException">The data is empty, unsupported, damaged, or over the pixel limit.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled; no image is produced.</exception>
    [AddedVersion("0.1.0")]
    [Description("Asynchronously loads an image from a stream, reading from its current position to the end.")]
    public static async Task<PdfImage> FromStreamAsync(Stream stream, PdfImageLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var settings = CaptureSettings(options);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Load(buffer.ToArray(), settings, cancellationToken);
    }

    #endregion

    #region Private Methods

    // Settings are read once, when a load starts, so changing them later affects only later loads.
    private static ImageLoadSettings CaptureSettings(PdfImageLoadOptions? options) => new(
        MaxPixels: options?.MaxPixels ?? DefaultMaxPixels,
        CmykInversion: options?.CmykInversion ?? PdfCmykInversion.Auto,
        ApplyExifOrientation: options?.ApplyExifOrientation ?? true);

    private static PdfImage Load(byte[] data, ImageLoadSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var imageData = ImageSignature.Detect(data) switch
        {
            ImageFormat.Jpeg => JpegReader.Read(data, settings, cancellationToken),
            ImageFormat.Png => PngReader.Read(data, settings, cancellationToken),
            var format => throw new PdfImageException(PdfImageErrorReason.UnsupportedFormat, $"The {format} image format has no reader.")
        };

        return new PdfImage(imageData);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the PDF-ready image data shared by every placement.
    /// </summary>
    internal PdfImageData Data { get; }

    /// <summary>
    /// Gets or sets the process-wide pixel limit (width x height) used by loads that set no
    /// <see cref="PdfImageLoadOptions.MaxPixels"/>. The built-in default is 100,000,000 pixels. A change affects
    /// only loads that start afterwards.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public static long DefaultMaxPixels
    {
        get => Interlocked.Read(ref _defaultMaxPixels);
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The pixel limit must be greater than zero.");
            Interlocked.Exchange(ref _defaultMaxPixels, value);
        }
    }

    /// <summary>
    /// Gets the horizontal resolution stored in the file, in dots per inch, or null when the file has none. It
    /// does not affect the placed size.
    /// </summary>
    public double? DpiX => Data.DpiX;

    /// <summary>
    /// Gets the vertical resolution stored in the file, in dots per inch, or null when the file has none. It does
    /// not affect the placed size.
    /// </summary>
    public double? DpiY => Data.DpiY;

    /// <summary>
    /// Gets the image height in pixels, as displayed.
    /// </summary>
    public int PixelHeight => Data.PixelHeight;

    /// <summary>
    /// Gets the image width in pixels, as displayed.
    /// </summary>
    public int PixelWidth => Data.PixelWidth;

    #endregion
}
