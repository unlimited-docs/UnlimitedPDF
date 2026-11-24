using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// A free font the demos site includes for one script of the Fonts and languages demo.
/// </summary>
/// <param name="Script">The slot the font fills.</param>
/// <param name="Name">The font's family name, as its credit shows it.</param>
/// <param name="FileName">The font file in <c>wwwroot/fonts</c>.</param>
public sealed record BundledFont(FontScript Script, string Name, string FileName);

/// <summary>
/// The free fonts the Fonts and languages demo puts in every slot except Latin when the page opens, so no shown invoice
/// draws "?" (CR-001, D-030; WR-039). They are downloaded from the site itself, only by that page; Latin text
/// keeps the built-in font. The files and their license are in <c>wwwroot/fonts</c> (PR-011 exception, FD-016).
/// </summary>
public static class BundledFonts
{
    #region Fields and Constants

    /// <summary>
    /// The license of the included fonts, as their credit names it.
    /// </summary>
    public const string LicenseName = "SIL Open Font License 1.1";

    /// <summary>
    /// The site path of the license text that goes with the fonts.
    /// </summary>
    public const string LicensePath = "fonts/OFL.txt";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Downloads and loads every included font. A font that can't be downloaded or loaded is left out, so its slot stays
    /// empty and the page asks for a font as it does without one.
    /// </summary>
    /// <param name="http">The site's HTTP client.</param>
    /// <returns>The loaded fonts by script.</returns>
    public static async Task<IReadOnlyDictionary<FontScript, UploadedFont>> LoadAsync(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var loaded = await Task.WhenAll(All.Select(font => LoadOneAsync(http, font)));
        return loaded.OfType<UploadedFont>().ToDictionary(font => font.Bundled!.Script);
    }

    #endregion

    #region Private Methods

    private static async Task<UploadedFont?> LoadOneAsync(HttpClient http, BundledFont font)
    {
        try
        {
            byte[] bytes = await http.GetByteArrayAsync("fonts/" + font.FileName);
            return UploadedFontLoader.Load(font.FileName, bytes).Font is { } loaded ? loaded with { Bundled = font } : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the included fonts, in slot order.
    /// </summary>
    public static IReadOnlyList<BundledFont> All { get; } =
    [
        new(FontScript.Devanagari, "Noto Sans Devanagari", "NotoSansDevanagari-Regular.ttf"),
        new(FontScript.Telugu, "Noto Sans Telugu", "NotoSansTelugu-Regular.ttf"),
        new(FontScript.Tamil, "Noto Sans Tamil", "NotoSansTamil-Regular.ttf"),
        new(FontScript.Bengali, "Noto Sans Bengali", "NotoSansBengali-Regular.ttf"),
        new(FontScript.Gujarati, "Noto Sans Gujarati", "NotoSansGujarati-Regular.ttf"),
        new(FontScript.Gurmukhi, "Noto Sans Gurmukhi", "NotoSansGurmukhi-Regular.ttf"),
        new(FontScript.Odia, "Noto Sans Oriya", "NotoSansOriya-Regular.ttf"),
        new(FontScript.Kannada, "Noto Sans Kannada", "NotoSansKannada-Regular.ttf"),
        new(FontScript.Malayalam, "Noto Sans Malayalam", "NotoSansMalayalam-Regular.ttf")
    ];

    #endregion
}
