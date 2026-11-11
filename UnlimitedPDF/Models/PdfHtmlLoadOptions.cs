namespace UnlimitedPDF;

/// <summary>
/// Options for loading HTML (WR-038): where relative paths start, how other references are resolved, whether a
/// warning fails the load, and per-call limits that override the <see cref="PdfHtml"/> defaults.
/// </summary>
public sealed class PdfHtmlLoadOptions
{
    #region Fields and Constants

    private long? _maxInputSize;
    private int? _maxNestingDepth;
    private long? _maxResourceSize;
    private long? _maxTotalResourceSize;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the folder relative paths in the HTML are read from, or null. A file path load uses the file's
    /// folder when this is null. Only files inside this folder are read (PR-018); without a base folder no file is read.
    /// </summary>
    public string? BaseFolder { get; set; }

    /// <summary>
    /// Gets or sets whether the first warning throws a <see cref="PdfHtmlException"/> instead of being added to
    /// <see cref="PdfHtml.Warnings"/>. Load warnings throw while loading; layout warnings throw when the document is
    /// saved, before anything is written. The default is false.
    /// </summary>
    public bool FailOnWarning { get; set; }

    /// <summary>
    /// Gets or sets the largest HTML input in bytes, or null for <see cref="PdfHtml.DefaultMaxInputSize"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long? MaxInputSize
    {
        get => _maxInputSize;
        set => _maxInputSize = Positive(value, nameof(MaxInputSize));
    }

    /// <summary>
    /// Gets or sets the deepest element nesting, from 1 to 512, or null for <see cref="PdfHtml.DefaultMaxNestingDepth"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1 or more than 512.</exception>
    public int? MaxNestingDepth
    {
        get => _maxNestingDepth;
        set => _maxNestingDepth = value is int v && (v < 1 || v > PdfHtml.MaxSupportedNestingDepth)
            ? throw new ArgumentOutOfRangeException(nameof(MaxNestingDepth), value, PdfHtml.NestingDepthRange)
            : value;
    }

    /// <summary>
    /// Gets or sets the largest single image or stylesheet in bytes, or null for <see cref="PdfHtml.DefaultMaxResourceSize"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long? MaxResourceSize
    {
        get => _maxResourceSize;
        set => _maxResourceSize = Positive(value, nameof(MaxResourceSize));
    }

    /// <summary>
    /// Gets or sets the most bytes of images and stylesheets one HTML input may load in total, or null for
    /// <see cref="PdfHtml.DefaultMaxTotalResourceSize"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long? MaxTotalResourceSize
    {
        get => _maxTotalResourceSize;
        set => _maxTotalResourceSize = Positive(value, nameof(MaxTotalResourceSize));
    }

    /// <summary>
    /// Gets or sets the resolver for references the library does not load itself (anything other than <c>data:</c>
    /// URIs and relative paths inside the base folder), or null. It receives the reference exactly as written and
    /// returns a stream with the resource, which the library reads and disposes, or null when it has none. Exceptions
    /// it throws are not caught.
    /// </summary>
    public Func<string, Stream?>? ResourceResolver { get; set; }

    /// <summary>
    /// Gets or sets the asynchronous resolver used by the <c>Async</c> load methods, or null to use
    /// <see cref="ResourceResolver"/>. A synchronous load with only this resolver set throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public Func<string, CancellationToken, Task<Stream?>>? ResourceResolverAsync { get; set; }

    #endregion

    #region Methods

    #region Private Methods

    private static long? Positive(long? value, string name) => value is long v && v < 1
        ? throw new ArgumentOutOfRangeException(name, value, "The limit must be at least 1.")
        : value;

    #endregion

    #endregion
}
