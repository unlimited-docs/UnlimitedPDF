using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Parsing;
using UnlimitedPDF.Models.Internal.Html.Resources;

namespace UnlimitedPDF.Models.Internal.Html;

/// <summary>
/// Loaded HTML ready for layout (WR-038): the element tree, every element's style, and the images that loaded.
/// </summary>
internal sealed class HtmlModel
{
    #region Constructors and Finalizers

    private HtmlModel(HtmlDocumentTree tree, Dictionary<HtmlElement, ComputedStyle> styles, Dictionary<HtmlElement, PdfImage> images, IReadOnlyDictionary<string, IReadOnlyList<PdfFont>>? fonts, bool synthesizeMissingStyles)
    {
        Fonts = fonts ?? new Dictionary<string, IReadOnlyList<PdfFont>>();
        SynthesizeMissingStyles = synthesizeMissingStyles;
        Tree = tree;
        Styles = styles;
        Images = images;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads HTML text synchronously.
    /// </summary>
    /// <param name="text">The HTML text.</param>
    /// <param name="baseFolder">The base folder, or null.</param>
    /// <param name="options">The options, or null.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="warnings">The warnings.</param>
    /// <returns>The model.</returns>
    public static HtmlModel Load(string text, string? baseFolder, PdfHtmlLoadOptions? options, HtmlLimits limits, HtmlWarnings warnings)
    {
        var tree = new HtmlTreeBuilder(limits.MaxNestingDepth, warnings).Build(new HtmlTokenizer(text).Tokens());
        var loader = new HtmlResourceLoader(baseFolder, options, limits, warnings);
        var rules = new List<CssRule>();
        int order = 0;
        foreach (var source in tree.StyleSources)
        {
            if (!CssParser.AppliesToPrint(source.Media)) continue;
            string? css = source.Text ?? (loader.Load(source.Href!, source.Line) is { } bytes ? DecodeCss(bytes) : null);
            if (css is not null) CssParser.ParseStyleSheet(css, source.Line, ref order, rules, warnings, limits.MaxNestingDepth);
        }
        var styles = CssCascade.Apply(tree, rules, warnings);
        var images = new Dictionary<HtmlElement, PdfImage>();
        var cache = new Dictionary<string, PdfImage?>(StringComparer.Ordinal);
        foreach (var (element, src) in ImageElements(tree, styles))
        {
            if (!cache.TryGetValue(src, out var image)) cache[src] = image = ToImage(loader.Load(src, element.Line), src, element.Line, warnings);
            if (image is not null) images[element] = image;
        }
        return new HtmlModel(tree, styles, images, options?.Fonts.Snapshot(), options?.SynthesizeMissingStyles ?? false);
    }

    /// <summary>
    /// Loads HTML text asynchronously, reading resources asynchronously.
    /// </summary>
    /// <param name="text">The HTML text.</param>
    /// <param name="baseFolder">The base folder, or null.</param>
    /// <param name="options">The options, or null.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="warnings">The warnings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model.</returns>
    public static async Task<HtmlModel> LoadAsync(string text, string? baseFolder, PdfHtmlLoadOptions? options, HtmlLimits limits, HtmlWarnings warnings, CancellationToken cancellationToken)
    {
        var tree = new HtmlTreeBuilder(limits.MaxNestingDepth, warnings).Build(new HtmlTokenizer(text).Tokens());
        cancellationToken.ThrowIfCancellationRequested();
        var loader = new HtmlResourceLoader(baseFolder, options, limits, warnings);
        var rules = new List<CssRule>();
        int order = 0;
        foreach (var source in tree.StyleSources)
        {
            if (!CssParser.AppliesToPrint(source.Media)) continue;
            string? css = source.Text ?? (await loader.LoadAsync(source.Href!, source.Line, cancellationToken).ConfigureAwait(false) is { } bytes ? DecodeCss(bytes) : null);
            if (css is not null) CssParser.ParseStyleSheet(css, source.Line, ref order, rules, warnings, limits.MaxNestingDepth);
        }
        var styles = CssCascade.Apply(tree, rules, warnings);
        cancellationToken.ThrowIfCancellationRequested();
        var images = new Dictionary<HtmlElement, PdfImage>();
        var cache = new Dictionary<string, PdfImage?>(StringComparer.Ordinal);
        foreach (var (element, src) in ImageElements(tree, styles))
        {
            if (!cache.TryGetValue(src, out var image))
                cache[src] = image = ToImage(await loader.LoadAsync(src, element.Line, cancellationToken).ConfigureAwait(false), src, element.Line, warnings);
            if (image is not null) images[element] = image;
        }
        return new HtmlModel(tree, styles, images, options?.Fonts.Snapshot(), options?.SynthesizeMissingStyles ?? false);
    }

    #endregion

    #region Private Methods

    private static string DecodeCss(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
            : Encoding.UTF8.GetString(bytes);

    // Image elements with a source that are displayed, in document order.
    private static IEnumerable<(HtmlElement Element, string Src)> ImageElements(HtmlDocumentTree tree, Dictionary<HtmlElement, ComputedStyle> styles)
    {
        var pending = new Stack<HtmlElement>();
        pending.Push(tree.Body);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (styles.TryGetValue(element, out var style) && style.Display == CssDisplay.None) continue;
            if (element.Name == "img" && element.Attribute("src") is { } src && src.Trim().Length > 0) yield return (element, src);
            for (int i = element.Children.Count - 1; i >= 0; i--)
                if (element.Children[i] is HtmlElement child) pending.Push(child);
        }
    }

    private static PdfImage? ToImage(byte[]? bytes, string src, int line, HtmlWarnings warnings)
    {
        if (bytes is null) return null;
        try
        {
            return PdfImage.FromBytes(bytes);
        }
        catch (PdfImageException ex)
        {
            string shown = src.Length <= 80 ? src : src.Substring(0, 77) + "...";
            if (ex.Reason == PdfImageErrorReason.UnsupportedFormat)
                warnings.Add(PdfHtmlWarningKind.UnsupportedImageFormat, shown, $"\"{shown}\" is not a PNG or JPEG image; its alt text is shown instead.", line);
            else
                warnings.Add(PdfHtmlWarningKind.InvalidImage, shown, $"\"{shown}\" could not be used: {ex.Message}", line);
            return null;
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the images that loaded, by element.
    /// </summary>
    public Dictionary<HtmlElement, PdfImage> Images { get; }

    /// <summary>
    /// Gets the caller fonts registered for the load, by lowercase CSS family name (WR-039).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PdfFont>> Fonts { get; }

    /// <summary>
    /// Gets whether a missing bold or italic face is synthesized (D-012).
    /// </summary>
    public bool SynthesizeMissingStyles { get; }

    /// <summary>
    /// Gets every element's computed style.
    /// </summary>
    public Dictionary<HtmlElement, ComputedStyle> Styles { get; }

    /// <summary>
    /// Gets the element tree.
    /// </summary>
    public HtmlDocumentTree Tree { get; }

    #endregion
}
