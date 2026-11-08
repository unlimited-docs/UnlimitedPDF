namespace UnlimitedPDF;

/// <summary>
/// An existing PDF, loaded and checked once, to take pages from (WR-035). It is read-only: to create a PDF, use
/// <see cref="PdfDocument"/> and add this file's pages with <see cref="PdfDocument.AddPages"/>, or join whole files
/// with <see cref="PdfOperations"/>. Encrypted files are not opened.
/// </summary>
public sealed class PdfFile
{
    #region Fields and Constants

    private const long BuiltInDefaultMaxFileSize = 524_288_000;
    private const int BuiltInDefaultMaxObjectCount = 5_000_000;
    private const long BuiltInDefaultMaxStreamSize = 104_857_600;
    private const int CopyBufferSize = 81920;
    private static long _defaultMaxFileSize = BuiltInDefaultMaxFileSize;
    private static int _defaultMaxObjectCount = BuiltInDefaultMaxObjectCount;
    private static long _defaultMaxStreamSize = BuiltInDefaultMaxStreamSize;

    #endregion

    #region Constructors and Finalizers

    private PdfFile(PdfSourceFile source, List<PdfSourcePage> pages, string? title, string? fileName)
    {
        Source = source;
        Pages = pages;
        Title = title;
        FileName = fileName;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads an existing PDF from a byte array. The array is copied.
    /// </summary>
    /// <param name="data">The PDF file bytes.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <returns>The loaded file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="PdfFileException">The PDF is encrypted, damaged, empty, or over a limit.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads an existing PDF from a byte array. The array is copied.")]
    public static PdfFile FromBytes(byte[] data, PdfFileLoadOptions? options = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        var limits = CaptureLimits(options);
        return Load(data.ToArray(), limits, fileName: null, CancellationToken.None);
    }

    /// <summary>
    /// Loads an existing PDF from a file.
    /// </summary>
    /// <param name="path">The path of the PDF file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <returns>The loaded file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="PdfFileException">The PDF is encrypted, damaged, empty, or over a limit.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads an existing PDF from a file.")]
    public static PdfFile FromFile(string path, PdfFileLoadOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var limits = CaptureLimits(options);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        CheckSize(stream.Length, limits);
        return Load(ReadAll(stream, limits), limits, Path.GetFileName(path), CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously loads an existing PDF from a file.
    /// </summary>
    /// <param name="path">The path of the PDF file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>The loaded file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="PdfFileException">The PDF is encrypted, damaged, empty, or over a limit.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled; no file is produced.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously loads an existing PDF from a file.")]
    public static async Task<PdfFile> FromFileAsync(string path, PdfFileLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var limits = CaptureLimits(options);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
        CheckSize(stream.Length, limits);
        var data = await ReadAllAsync(stream, limits, cancellationToken);
        return Load(data, limits, Path.GetFileName(path), cancellationToken);
    }

    /// <summary>
    /// Loads an existing PDF from a stream, reading from its current position to the end. The stream is left open
    /// and does not need to be seekable.
    /// </summary>
    /// <param name="stream">A stream holding a PDF file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <returns>The loaded file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfFileException">The PDF is encrypted, damaged, empty, or over a limit.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads an existing PDF from a stream, reading from its current position to the end.")]
    public static PdfFile FromStream(Stream stream, PdfFileLoadOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var limits = CaptureLimits(options);
        if (stream.CanSeek) CheckSize(stream.Length - stream.Position, limits);
        return Load(ReadAll(stream, limits), limits, fileName: null, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously loads an existing PDF from a stream, reading from its current position to the end. The stream
    /// is left open and does not need to be seekable.
    /// </summary>
    /// <param name="stream">A stream holding a PDF file.</param>
    /// <param name="options">Options for this load, or null for the defaults.</param>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>The loaded file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfFileException">The PDF is encrypted, damaged, empty, or over a limit.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled; no file is produced.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously loads an existing PDF from a stream, reading from its current position to the end.")]
    public static async Task<PdfFile> FromStreamAsync(Stream stream, PdfFileLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var limits = CaptureLimits(options);
        if (stream.CanSeek) CheckSize(stream.Length - stream.Position, limits);
        var data = await ReadAllAsync(stream, limits, cancellationToken);
        return Load(data, limits, fileName: null, cancellationToken);
    }

    #endregion

    #region Private Methods

    // Limits are read once, when a load starts, so changing them later affects only later loads.
    private static PdfReadLimits CaptureLimits(PdfFileLoadOptions? options) => new(
        options?.MaxFileSize ?? DefaultMaxFileSize,
        options?.MaxStreamSize ?? DefaultMaxStreamSize,
        options?.MaxObjectCount ?? DefaultMaxObjectCount);

    private static void CheckSize(long length, PdfReadLimits limits)
    {
        if (length > limits.MaxFileSize) throw TooLarge(limits);
    }

    // Reads the file and its pages. When the file's own cross-reference leads to a damaged page object, the
    // cross-reference is rebuilt by scanning and the pages are read once more (WR-035).
    private static PdfFile Load(byte[] data, PdfReadLimits limits, string? fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var source = PdfFileReader.Read(data, limits);
            List<PdfSourcePage> pages;
            try
            {
                pages = PdfPageTreeReader.ReadPages(source);
            }
            catch (PdfReadException exception) when (exception.Error == PdfReadError.Damaged && !source.Recovered)
            {
                source = PdfFileReader.Read(data, limits, recover: true);
                pages = PdfPageTreeReader.ReadPages(source);
            }
            return new PdfFile(source, pages, ReadTitle(source), fileName);
        }
        catch (PdfReadException exception)
        {
            throw PdfFileReader.ToPublic(exception);
        }
    }

    private static byte[] ReadAll(Stream stream, PdfReadLimits limits)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[CopyBufferSize];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limits.MaxFileSize) throw TooLarge(limits);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, PdfReadLimits limits, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[CopyBufferSize];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            if (buffer.Length + read > limits.MaxFileSize) throw TooLarge(limits);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    // The Info dictionary's /Title; damage there is ignored because the title is only a bookmark fallback.
    private static string? ReadTitle(PdfSourceFile source)
    {
        try
        {
            var table = source.Objects;
            if (table.Resolve(table.Trailer["Info"]) is PdfReadDictionary info && table.Resolve(info["Title"]) is PdfReadString title)
            {
                string text = PdfTextString.Decode(title.Bytes).Trim();
                return text.Length == 0 ? null : text;
            }
        }
        catch (PdfReadException exception) when (exception.Error == PdfReadError.Damaged)
        {
        }
        return null;
    }

    private static PdfFileException TooLarge(PdfReadLimits limits) => PdfFileReader.ToPublic(new PdfReadException(
        PdfReadError.TooLarge,
        $"The file is over the size limit of {limits.MaxFileSize.ToString("N0", CultureInfo.InvariantCulture)} bytes."));

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the process-wide file size limit, in bytes, used by loads that set no
    /// <see cref="PdfFileLoadOptions.MaxFileSize"/>. The built-in default is 524,288,000 bytes (500 MB). A change
    /// affects only loads that start afterwards.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public static long DefaultMaxFileSize
    {
        get => Interlocked.Read(ref _defaultMaxFileSize);
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The file size limit must be greater than zero.");
            Interlocked.Exchange(ref _defaultMaxFileSize, value);
        }
    }

    /// <summary>
    /// Gets or sets the process-wide object limit used by loads that set no
    /// <see cref="PdfFileLoadOptions.MaxObjectCount"/>. The built-in default is 5,000,000 objects. A change affects
    /// only loads that start afterwards.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public static int DefaultMaxObjectCount
    {
        get => Volatile.Read(ref _defaultMaxObjectCount);
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The object limit must be greater than zero.");
            Interlocked.Exchange(ref _defaultMaxObjectCount, value);
        }
    }

    /// <summary>
    /// Gets or sets the process-wide limit, in bytes, on what one compressed stream may decompress to, used by loads
    /// that set no <see cref="PdfFileLoadOptions.MaxStreamSize"/>. The built-in default is 104,857,600 bytes
    /// (100 MB). A change affects only loads that start afterwards.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public static long DefaultMaxStreamSize
    {
        get => Interlocked.Read(ref _defaultMaxStreamSize);
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The stream size limit must be greater than zero.");
            Interlocked.Exchange(ref _defaultMaxStreamSize, value);
        }
    }

    /// <summary>
    /// Gets the number of pages.
    /// </summary>
    public int PageCount => Pages.Count;

    /// <summary>
    /// Gets the document title from the file's information dictionary, or null when it has none.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the file name for a file loaded from a path, or null.
    /// </summary>
    internal string? FileName { get; }

    /// <summary>
    /// Gets the pages in reading order.
    /// </summary>
    internal IReadOnlyList<PdfSourcePage> Pages { get; }

    /// <summary>
    /// Gets the file that was read.
    /// </summary>
    internal PdfSourceFile Source { get; }

    #endregion
}
