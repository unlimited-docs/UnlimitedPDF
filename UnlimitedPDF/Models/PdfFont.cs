using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;
using UnlimitedPDF.Models.Internal.Text;

namespace UnlimitedPDF;

/// <summary>
/// A TrueType or OpenType font loaded from a file, stream, or byte array, for drawing text in languages and characters
/// the built-in fonts lack (WR-039). The font is read and checked once, at load; a PDF embeds only the glyphs its text
/// uses. Font collections (<c>.ttc</c>) and web fonts (WOFF, WOFF2) are not supported.
/// </summary>
public sealed class PdfFont
{
    #region Fields and Constants

    private const long BuiltInDefaultMaxFileSize = 52_428_800; // 50 MB
    private const int CopyBufferSize = 81920;

    // OS/2 fsType bits (PR-022): the usage permission in bits 0-3, then the no-subsetting and bitmap-only flags.
    private const ushort UsagePermissionMask = 0x000F;
    private const ushort RestrictedLicense = 0x0002;
    private const ushort NoSubsetting = 0x0100;
    private const ushort BitmapOnly = 0x0200;

    private static long _defaultMaxFileSize = BuiltInDefaultMaxFileSize;

    private FontLayout? _layout;

    #endregion

    #region Constructors and Finalizers

    private PdfFont(OpenTypeFont font, FontInstance instance, int weight, VariationAxis? weightAxis)
    {
        Font = font;
        Instance = instance;
        Weight = weight;
        EmbedWholeFont = (font.FsType & NoSubsetting) != 0;
        IsVariable = weightAxis is not null;
        MinimumWeight = weightAxis is null ? weight : (int)Math.Round(weightAxis.Minimum);
        MaximumWeight = weightAxis is null ? weight : (int)Math.Round(weightAxis.Maximum);
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads a font from a byte array. The array is copied.
    /// </summary>
    /// <param name="data">The font file bytes (.ttf or .otf).</param>
    /// <param name="options">Load options, or null for the defaults.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="PdfFontException">The data is not a supported font, is damaged, is over the size limit, may not
    /// be embedded, or lacks the requested weight.</exception>
    [AddedVersion("1.1.0")]
    [Description("Loads a font from a byte array. The array is copied.")]
    public static PdfFont FromBytes(byte[] data, PdfFontLoadOptions? options = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        var settings = Capture(options);
        CheckSize(data.Length, settings.MaxFileSize);
        return Load(data.ToArray(), settings, CancellationToken.None);
    }

    /// <summary>
    /// Loads a font from a file.
    /// </summary>
    /// <param name="path">The path of the font file (.ttf or .otf).</param>
    /// <param name="options">Load options, or null for the defaults.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="PdfFontException">The file is not a supported font, is damaged, is over the size limit, may not
    /// be embedded, or lacks the requested weight.</exception>
    [AddedVersion("1.1.0")]
    [Description("Loads a font from a file.")]
    public static PdfFont FromFile(string path, PdfFontLoadOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var settings = Capture(options);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        CheckSize(stream.Length, settings.MaxFileSize);
        return Load(ReadAll(stream, settings.MaxFileSize), settings, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously loads a font from a file.
    /// </summary>
    /// <param name="path">The path of the font file (.ttf or .otf).</param>
    /// <param name="options">Load options, or null for the defaults.</param>
    /// <param name="cancellationToken">A token that cancels the load.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="PdfFontException">The file is not a supported font, is damaged, is over the size limit, may not
    /// be embedded, or lacks the requested weight.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled.</exception>
    [AddedVersion("1.1.0")]
    [Description("Asynchronously loads a font from a file.")]
    public static async Task<PdfFont> FromFileAsync(string path, PdfFontLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var settings = Capture(options);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
        CheckSize(stream.Length, settings.MaxFileSize);
        var data = await ReadAllAsync(stream, settings.MaxFileSize, cancellationToken);
        return Load(data, settings, cancellationToken);
    }

    /// <summary>
    /// Loads a font from a stream, reading from its current position to the end. The stream is left open.
    /// </summary>
    /// <param name="stream">The stream holding the font file (.ttf or .otf).</param>
    /// <param name="options">Load options, or null for the defaults.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfFontException">The data is not a supported font, is damaged, is over the size limit, may not
    /// be embedded, or lacks the requested weight.</exception>
    [AddedVersion("1.1.0")]
    [Description("Loads a font from a stream, reading from its current position to the end.")]
    public static PdfFont FromStream(Stream stream, PdfFontLoadOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var settings = Capture(options);
        return Load(ReadAll(stream, settings.MaxFileSize), settings, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously loads a font from a stream, reading from its current position to the end. The stream is left
    /// open.
    /// </summary>
    /// <param name="stream">The stream holding the font file (.ttf or .otf).</param>
    /// <param name="options">Load options, or null for the defaults.</param>
    /// <param name="cancellationToken">A token that cancels the load.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="PdfFontException">The data is not a supported font, is damaged, is over the size limit, may not
    /// be embedded, or lacks the requested weight.</exception>
    /// <exception cref="OperationCanceledException">The load was cancelled.</exception>
    [AddedVersion("1.1.0")]
    [Description("Asynchronously loads a font from a stream, reading from its current position to the end.")]
    public static async Task<PdfFont> FromStreamAsync(Stream stream, PdfFontLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        var settings = Capture(options);
        var data = await ReadAllAsync(stream, settings.MaxFileSize, cancellationToken);
        return Load(data, settings, cancellationToken);
    }

    #endregion

    #region Private Methods

    // Options are read once, when a load starts, so changing them later affects only later loads.
    private static LoadSettings Capture(PdfFontLoadOptions? options) =>
        new(options?.MaxFileSize ?? DefaultMaxFileSize, options?.IgnoreEmbeddingRestrictions ?? false, options?.Weight);

    private static void CheckSize(long length, long limit)
    {
        if (length > limit) throw TooLarge(limit);
    }

    private static PdfFont Load(byte[] data, LoadSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var font = OpenTypeFont.Parse(data);
        CheckEmbedding(font, settings.IgnoreEmbeddingRestrictions);

        // A variable font with a weight axis draws the requested weight, limited to the axis range (WR-039); any
        // other font draws its own weight only.
        var axes = VariationAxes.Read(font);
        if (font.Outlines == FontOutlines.Cff2 && axes is null) throw FontBytes.Corrupt("has CFF2 outlines but no fvar table");
        var weightAxis = axes?.WeightAxis;
        if (weightAxis is not null)
        {
            double chosen = Math.Max(weightAxis.Minimum, Math.Min(weightAxis.Maximum, settings.Weight ?? weightAxis.Default));
            var coordinates = axes!.Normalize(chosen);
            return new PdfFont(font, FontInstance.Create(font, axes, coordinates), (int)Math.Round(chosen), weightAxis);
        }

        int weight = font.WeightClass is >= 1 and <= 1000 ? font.WeightClass : 400;
        if (settings.Weight is int requested && requested != weight)
        {
            throw new PdfFontException(
                PdfFontErrorReason.WeightNotAvailable,
                string.Format(CultureInfo.InvariantCulture, "The font \"{0}\" has only weight {1}; weight {2} was requested.", Describe(font), weight, requested));
        }
        var defaults = axes?.Normalize(null) ?? Array.Empty<double>();
        return new PdfFont(font, FontInstance.Create(font, axes, defaults), weight, null);
    }

    // Honors the font's embedding permissions unless the caller asserts the rights (PR-022). Bits 0-3 hold one usage
    // permission; when an older font sets several, the least restrictive one applies.
    private static void CheckEmbedding(OpenTypeFont font, bool ignoreRestrictions)
    {
        if (ignoreRestrictions) return;
        if ((font.FsType & UsagePermissionMask) == RestrictedLicense)
            throw new PdfFontException(PdfFontErrorReason.EmbeddingRestricted, $"The font \"{Describe(font)}\" does not allow embedding (restricted license).");
        if ((font.FsType & BitmapOnly) != 0)
            throw new PdfFontException(PdfFontErrorReason.EmbeddingRestricted, $"The font \"{Describe(font)}\" allows only bitmap embedding.");
    }

    private static string Describe(OpenTypeFont font) => font.FullName.Length > 0 ? font.FullName : font.FamilyName;

    private static byte[] ReadAll(Stream stream, long limit)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[CopyBufferSize];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit) throw TooLarge(limit);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, long limit, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[CopyBufferSize];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit) throw TooLarge(limit);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static PdfFontException TooLarge(long limit) => new(
        PdfFontErrorReason.TooLarge,
        $"The font is over the size limit of {limit.ToString("N0", CultureInfo.InvariantCulture)} bytes.");

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the process-wide file size limit, in bytes, used by loads that set no
    /// <see cref="PdfFontLoadOptions.MaxFileSize"/>. The built-in default is 52,428,800 bytes (50 MB). A change affects
    /// only loads that start afterwards.
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
    /// Gets the font's family name, for example "Noto Sans Telugu".
    /// </summary>
    public string FamilyName => Font.FamilyName;

    /// <summary>
    /// Gets whether the font is italic or oblique.
    /// </summary>
    public bool IsItalic => (Font.FsSelection & 0x0001) != 0 || (Font.MacStyle & 0x0002) != 0;

    /// <summary>
    /// Gets whether the font is a variable font that holds a range of weights.
    /// </summary>
    public bool IsVariable { get; }

    /// <summary>
    /// Gets the heaviest weight the font can draw; equal to <see cref="Weight"/> for a font with one weight.
    /// </summary>
    public int MaximumWeight { get; }

    /// <summary>
    /// Gets the lightest weight the font can draw; equal to <see cref="Weight"/> for a font with one weight.
    /// </summary>
    public int MinimumWeight { get; }

    /// <summary>
    /// Gets the weight this font draws with, from 1 to 1000 (400 is regular, 700 is bold).
    /// </summary>
    public int Weight { get; }

    // True when the font forbids subsetting, so the PDF embeds the whole font (PR-022).
    internal bool EmbedWholeFont { get; }

    internal OpenTypeFont Font { get; }

    // The font's GSUB, GPOS, and GDEF tables, read on first use.
    internal FontLayout Layout => _layout ??= new FontLayout(Font);

    // The advances, outlines, and metrics at this font's weight.
    internal FontInstance Instance { get; }

    #endregion

    #region Nested Types

    private sealed record LoadSettings(long MaxFileSize, bool IgnoreEmbeddingRestrictions, int? Weight);

    #endregion
}
