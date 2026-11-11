using UnlimitedPDF.Models.Internal.Html;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF;

/// <summary>
/// HTML loaded for conversion to PDF (WR-038): parsed, styled, and with its images and stylesheets read, ready to add to
/// a document's body with <see cref="PdfBodyFlow.AddHtml"/> or to convert with <see cref="PdfOperations.ConvertHtml(PdfHtml, string, PdfHtmlConvertOptions?)"/>.
/// Loading is like <see cref="PdfImage"/> and <see cref="PdfFile"/>: limit failures throw <see cref="PdfHtmlException"/>
/// here, and anything skipped is listed in <see cref="Warnings"/>.
/// </summary>
public sealed class PdfHtml
{
    #region Fields and Constants

    private const long BuiltInDefaultMaxInputSize = 50L * 1024 * 1024;
    private const int BuiltInDefaultMaxNestingDepth = 256;

    /// <summary>
    /// The highest nesting limit that can be set: deeper nesting could exhaust a 1 MB thread stack during layout (tables nested about 250 deep do).
    /// </summary>
    internal const int MaxSupportedNestingDepth = 512;

    internal const string NestingDepthRange = "The limit must be from 1 to 512.";
    private const long BuiltInDefaultMaxResourceSize = 50L * 1024 * 1024;
    private const long BuiltInDefaultMaxTotalResourceSize = 200L * 1024 * 1024;

    private static long _defaultMaxInputSize = BuiltInDefaultMaxInputSize;
    private static int _defaultMaxNestingDepth = BuiltInDefaultMaxNestingDepth;
    private static long _defaultMaxResourceSize = BuiltInDefaultMaxResourceSize;
    private static long _defaultMaxTotalResourceSize = BuiltInDefaultMaxTotalResourceSize;

    private readonly IReadOnlyList<PdfHtmlWarning> _loadWarnings;
    private IReadOnlyList<PdfHtmlWarning> _warnings;

    #endregion

    #region Constructors and Finalizers

    private PdfHtml(HtmlModel model, IReadOnlyList<PdfHtmlWarning> warnings, bool failOnWarning)
    {
        Model = model;
        _loadWarnings = warnings;
        _warnings = warnings;
        FailOnWarning = failOnWarning;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads HTML from a file. Relative paths in it are read from the file's folder unless the options give a base folder.
    /// The file is decoded by its byte-order mark, then its <c>&lt;meta charset&gt;</c>, else as UTF-8.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The load options, or null for the defaults.</param>
    /// <returns>The loaded HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A limit is exceeded, or a warning occurred in strict mode.</exception>
    /// <exception cref="InvalidOperationException">Only an asynchronous resolver is set.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads HTML from a file.")]
    public static PdfHtml FromFile(string path, PdfHtmlLoadOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        RequireSyncResolver(options);
        var limits = HtmlLimits.For(options);
        limits.CheckInput(new FileInfo(path).Length);
        var warnings = new HtmlWarnings(options?.FailOnWarning ?? false);
        byte[] data = File.ReadAllBytes(path);
        limits.CheckInput(data.LongLength);
        string text = HtmlDecoder.Decode(data, data.Length, warnings);
        return Create(HtmlModel.Load(text, BaseFolderFor(path, options), options, limits, warnings), warnings, options);
    }

    /// <summary>
    /// Loads HTML from a file asynchronously, reading resources asynchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The load options, or null for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A limit is exceeded, or a warning occurred in strict mode.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads HTML from a file asynchronously.")]
    public static async Task<PdfHtml> FromFileAsync(string path, PdfHtmlLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var limits = HtmlLimits.For(options);
        limits.CheckInput(new FileInfo(path).Length);
        byte[] data;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            data = await ReadAllAsync(stream, limits, cancellationToken).ConfigureAwait(false);
        var warnings = new HtmlWarnings(options?.FailOnWarning ?? false);
        string text = HtmlDecoder.Decode(data, data.Length, warnings);
        return Create(await HtmlModel.LoadAsync(text, BaseFolderFor(path, options), options, limits, warnings, cancellationToken).ConfigureAwait(false), warnings, options);
    }

    /// <summary>
    /// Loads HTML from a stream, reading from its current position to the end and leaving it open. Relative paths are
    /// read from <see cref="PdfHtmlLoadOptions.BaseFolder"/> when one is given.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The load options, or null for the defaults.</param>
    /// <returns>The loaded HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A limit is exceeded, or a warning occurred in strict mode.</exception>
    /// <exception cref="InvalidOperationException">Only an asynchronous resolver is set.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads HTML from a stream.")]
    public static PdfHtml FromStream(Stream stream, PdfHtmlLoadOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        RequireSyncResolver(options);
        var limits = HtmlLimits.For(options);
        byte[] data = ReadAll(stream, limits);
        var warnings = new HtmlWarnings(options?.FailOnWarning ?? false);
        string text = HtmlDecoder.Decode(data, data.Length, warnings);
        return Create(HtmlModel.Load(text, options?.BaseFolder, options, limits, warnings), warnings, options);
    }

    /// <summary>
    /// Loads HTML from a stream asynchronously, reading from its current position to the end and leaving it open.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The load options, or null for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A limit is exceeded, or a warning occurred in strict mode.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads HTML from a stream asynchronously.")]
    public static async Task<PdfHtml> FromStreamAsync(Stream stream, PdfHtmlLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var limits = HtmlLimits.For(options);
        byte[] data = await ReadAllAsync(stream, limits, cancellationToken).ConfigureAwait(false);
        var warnings = new HtmlWarnings(options?.FailOnWarning ?? false);
        string text = HtmlDecoder.Decode(data, data.Length, warnings);
        return Create(await HtmlModel.LoadAsync(text, options?.BaseFolder, options, limits, warnings, cancellationToken).ConfigureAwait(false), warnings, options);
    }

    /// <summary>
    /// Loads HTML from a string. Relative paths are read from <see cref="PdfHtmlLoadOptions.BaseFolder"/> when one is given.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="options">The load options, or null for the defaults.</param>
    /// <returns>The loaded HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A limit is exceeded, or a warning occurred in strict mode.</exception>
    /// <exception cref="InvalidOperationException">Only an asynchronous resolver is set.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads HTML from a string.")]
    public static PdfHtml FromString(string html, PdfHtmlLoadOptions? options = null)
    {
        if (html is null) throw new ArgumentNullException(nameof(html));
        RequireSyncResolver(options);
        var limits = HtmlLimits.For(options);
        limits.CheckInput(Encoding.UTF8.GetByteCount(html));
        var warnings = new HtmlWarnings(options?.FailOnWarning ?? false);
        return Create(HtmlModel.Load(html, options?.BaseFolder, options, limits, warnings), warnings, options);
    }

    /// <summary>
    /// Loads HTML from a string asynchronously, reading resources asynchronously (for example with an asynchronous
    /// resolver in a browser).
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="options">The load options, or null for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded HTML.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A limit is exceeded, or a warning occurred in strict mode.</exception>
    [AddedVersion("1.0.0")]
    [Description("Loads HTML from a string asynchronously.")]
    public static async Task<PdfHtml> FromStringAsync(string html, PdfHtmlLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (html is null) throw new ArgumentNullException(nameof(html));
        var limits = HtmlLimits.For(options);
        limits.CheckInput(Encoding.UTF8.GetByteCount(html));
        var warnings = new HtmlWarnings(options?.FailOnWarning ?? false);
        return Create(await HtmlModel.LoadAsync(html, options?.BaseFolder, options, limits, warnings, cancellationToken).ConfigureAwait(false), warnings, options);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Records the warnings of the latest layout of this HTML (WR-038): <see cref="Warnings"/> then lists the load
    /// warnings followed by these.
    /// </summary>
    /// <param name="layoutWarnings">The layout warnings.</param>
    internal void SetLayoutWarnings(IReadOnlyList<PdfHtmlWarning> layoutWarnings) =>
        _warnings = layoutWarnings.Count == 0 ? _loadWarnings : _loadWarnings.Concat(layoutWarnings).ToList();

    #endregion

    #region Private Methods

    private static string? BaseFolderFor(string path, PdfHtmlLoadOptions? options) =>
        options?.BaseFolder ?? Path.GetDirectoryName(Path.GetFullPath(path));

    private static PdfHtml Create(HtmlModel model, HtmlWarnings warnings, PdfHtmlLoadOptions? options) =>
        new(model, warnings.Items.ToList(), options?.FailOnWarning ?? false);

    // Reads to the end, failing as soon as the input passes the limit.
    private static byte[] ReadAll(Stream stream, HtmlLimits limits)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            limits.CheckInput(buffer.Length);
        }
        return buffer.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, HtmlLimits limits, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            limits.CheckInput(buffer.Length);
        }
        return buffer.ToArray();
    }

    private static void RequireSyncResolver(PdfHtmlLoadOptions? options)
    {
        if (options is { ResourceResolver: null, ResourceResolverAsync: not null })
            throw new InvalidOperationException("Only an asynchronous resource resolver is set; use an Async load method or set ResourceResolver.");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the process-wide largest HTML input in bytes; the default is 52,428,800 (50 MB).
    /// <see cref="PdfHtmlLoadOptions.MaxInputSize"/> overrides it for one load.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public static long DefaultMaxInputSize
    {
        get => _defaultMaxInputSize;
        set => _defaultMaxInputSize = value < 1 ? throw new ArgumentOutOfRangeException(nameof(value), value, "The limit must be at least 1.") : value;
    }

    /// <summary>
    /// Gets or sets the process-wide deepest element nesting, from 1 to 512; the default is 256.
    /// <see cref="PdfHtmlLoadOptions.MaxNestingDepth"/> overrides it for one load.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1 or more than 512.</exception>
    public static int DefaultMaxNestingDepth
    {
        get => _defaultMaxNestingDepth;
        set => _defaultMaxNestingDepth = value is < 1 or > MaxSupportedNestingDepth
            ? throw new ArgumentOutOfRangeException(nameof(value), value, NestingDepthRange)
            : value;
    }

    /// <summary>
    /// Gets or sets the process-wide largest single image or stylesheet in bytes; the default is 52,428,800 (50 MB).
    /// <see cref="PdfHtmlLoadOptions.MaxResourceSize"/> overrides it for one load.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public static long DefaultMaxResourceSize
    {
        get => _defaultMaxResourceSize;
        set => _defaultMaxResourceSize = value < 1 ? throw new ArgumentOutOfRangeException(nameof(value), value, "The limit must be at least 1.") : value;
    }

    /// <summary>
    /// Gets or sets the process-wide most bytes of images and stylesheets one HTML input may load; the default is
    /// 209,715,200 (200 MB). <see cref="PdfHtmlLoadOptions.MaxTotalResourceSize"/> overrides it for one load.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public static long DefaultMaxTotalResourceSize
    {
        get => _defaultMaxTotalResourceSize;
        set => _defaultMaxTotalResourceSize = value < 1 ? throw new ArgumentOutOfRangeException(nameof(value), value, "The limit must be at least 1.") : value;
    }

    /// <summary>
    /// Gets the warnings: what loading skipped, followed by what the latest layout of this HTML in a document clipped.
    /// </summary>
    public IReadOnlyList<PdfHtmlWarning> Warnings => _warnings;

    /// <summary>
    /// Gets whether a layout warning fails saving, from <see cref="PdfHtmlLoadOptions.FailOnWarning"/>.
    /// </summary>
    internal bool FailOnWarning { get; }

    /// <summary>
    /// Gets the loaded model.
    /// </summary>
    internal HtmlModel Model { get; }

    #endregion
}
