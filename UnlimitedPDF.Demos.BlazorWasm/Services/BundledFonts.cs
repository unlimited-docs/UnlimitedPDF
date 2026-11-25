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
/// The free fonts the Fonts and languages demo puts in every slot when the page opens, so no shown invoice draws "?"
/// (CR-001, D-030; WR-039; QF-26112401). They are downloaded from the site itself, only by that page. One file, Noto
/// Sans, fills the Latin, Greek, and Cyrillic slots, so all Latin text, including French and English, is drawn with it.
/// The files and their license are in <c>wwwroot/fonts</c> (PR-011 exception, FD-016).
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
    /// Downloads and loads every included font, each file once even when it fills several slots. A font that can't be
    /// downloaded or loaded is left out, so its slots stay empty and the page asks for a font as it does without one.
    /// </summary>
    /// <param name="http">The site's HTTP client.</param>
    /// <returns>The loaded fonts by script.</returns>
    public static Task<IReadOnlyDictionary<FontScript, UploadedFont>> LoadAsync(HttpClient http) => LoadAsync(http, All.Select(f => f.Script).ToList());

    /// <summary>
    /// Downloads and loads the included fonts of some scripts only, each file once (for example the right-to-left
    /// scripts for HTML to PDF).
    /// </summary>
    /// <param name="http">The site's HTTP client.</param>
    /// <param name="scripts">The scripts whose fonts to load.</param>
    /// <returns>The loaded fonts by script.</returns>
    public static async Task<IReadOnlyDictionary<FontScript, UploadedFont>> LoadAsync(HttpClient http, IReadOnlyCollection<FontScript> scripts)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(scripts);
        var wanted = All.Where(font => scripts.Contains(font.Script)).ToList();
        var files = wanted.Select(font => font.FileName).Distinct().ToList();
        var loaded = await Task.WhenAll(files.Select(file => LoadOneAsync(http, file)));
        var byFile = files.Zip(loaded).Where(pair => pair.Second is not null).ToDictionary(pair => pair.First, pair => pair.Second!);
        return wanted.Where(font => byFile.ContainsKey(font.FileName)).ToDictionary(font => font.Script, font => byFile[font.FileName] with { Bundled = font });
    }

    #endregion

    #region Private Methods

    private static async Task<UploadedFont?> LoadOneAsync(HttpClient http, string fileName)
    {
        try
        {
            byte[] bytes = await http.GetByteArrayAsync("fonts/" + fileName);
            return UploadedFontLoader.Load(fileName, bytes).Font;
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
        new(FontScript.Latin, "Noto Sans", "NotoSans-Regular.ttf"),
        new(FontScript.Greek, "Noto Sans", "NotoSans-Regular.ttf"),
        new(FontScript.Cyrillic, "Noto Sans", "NotoSans-Regular.ttf"),
        new(FontScript.Armenian, "Noto Sans Armenian", "NotoSansArmenian-Regular.ttf"),
        new(FontScript.Georgian, "Noto Sans Georgian", "NotoSansGeorgian-Regular.ttf"),
        new(FontScript.Devanagari, "Noto Sans Devanagari", "NotoSansDevanagari-Regular.ttf"),
        new(FontScript.Telugu, "Noto Sans Telugu", "NotoSansTelugu-Regular.ttf"),
        new(FontScript.Tamil, "Noto Sans Tamil", "NotoSansTamil-Regular.ttf"),
        new(FontScript.Bengali, "Noto Sans Bengali", "NotoSansBengali-Regular.ttf"),
        new(FontScript.Gujarati, "Noto Sans Gujarati", "NotoSansGujarati-Regular.ttf"),
        new(FontScript.Gurmukhi, "Noto Sans Gurmukhi", "NotoSansGurmukhi-Regular.ttf"),
        new(FontScript.Odia, "Noto Sans Oriya", "NotoSansOriya-Regular.ttf"),
        new(FontScript.Kannada, "Noto Sans Kannada", "NotoSansKannada-Regular.ttf"),
        new(FontScript.Malayalam, "Noto Sans Malayalam", "NotoSansMalayalam-Regular.ttf"),
        new(FontScript.Arabic, "Noto Sans Arabic", "NotoSansArabic-Regular.ttf"),
        new(FontScript.Hebrew, "Noto Sans Hebrew", "NotoSansHebrew-Regular.ttf"),
        new(FontScript.Thaana, "Noto Sans Thaana", "NotoSansThaana-Regular.ttf")
    ];

    #endregion
}
