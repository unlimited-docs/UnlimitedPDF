using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The font slots of the Fonts and languages demo: one per script, in the order the fonts are tried for each
/// character (the document's fallback order), which scripts a text needs, whether a font has a script's letters, and
/// where to find a free font for each script.
/// </summary>
public static class FontSlots
{
    #region Fields and Constants

    // Letters each script's check looks for; a font with none of them has no letters of the script. Latin is not
    // checked: the built-in font draws every Western European letter a font lacks, so the library reports no Latin
    // letter as missing.
    private static readonly Dictionary<FontScript, string> Letters = new()
    {
        [FontScript.Devanagari] = "कखगचजटडतदनपबमयरलवसह",
        [FontScript.Telugu] = "కఖగచజటడతదనపబమయరలవసహ",
        [FontScript.Tamil] = "கஙசஞடணதநபமயரலவழளறன"
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether a font has any letter of a script. Latin always counts as covered (see <see cref="Letters"/>).
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="script">The script.</param>
    /// <returns>False when the font has none of the script's letters.</returns>
    public static bool Covers(PdfFont font, FontScript script)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!Letters.TryGetValue(script, out var letters)) return true;
        var missing = new PdfDocument().FindMissingCharacters(letters, font);
        return missing.Count < letters.Length;
    }

    /// <summary>
    /// Returns the page of a free font family for a script, on a public font site.
    /// </summary>
    /// <param name="script">The script.</param>
    /// <returns>The link.</returns>
    public static string FreeFontLink(FontScript script) => script switch
    {
        FontScript.Devanagari => "https://fonts.google.com/noto/specimen/Noto+Sans+Devanagari",
        FontScript.Telugu => "https://fonts.google.com/noto/specimen/Noto+Sans+Telugu",
        FontScript.Tamil => "https://fonts.google.com/noto/specimen/Noto+Sans+Tamil",
        _ => "https://fonts.google.com/noto/specimen/Noto+Sans"
    };

    /// <summary>
    /// Returns the scripts a text uses, in slot order.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The scripts.</returns>
    public static IReadOnlyList<FontScript> Needed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new HashSet<FontScript>();
        foreach (char c in text)
        {
            if (ScriptOf(c) is FontScript script) found.Add(script);
        }
        return Scripts.Where(found.Contains).ToList();
    }

    /// <summary>
    /// Returns the script a character belongs to, or null for characters every script shares (digits, spaces,
    /// punctuation, currency signs).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The script, or null.</returns>
    public static FontScript? ScriptOf(char c) => c switch
    {
        >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= 'À' and <= 'ɏ' when c is not ('×' or '÷') => FontScript.Latin,
        >= 'ऀ' and <= 'ॿ' => FontScript.Devanagari,
        >= 'ఀ' and <= '౿' => FontScript.Telugu,
        >= '஀' and <= '௿' => FontScript.Tamil,
        _ => null
    };

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the scripts in slot order, which is the order the fonts are tried for each character.
    /// </summary>
    public static IReadOnlyList<FontScript> Scripts { get; } = [FontScript.Latin, FontScript.Devanagari, FontScript.Telugu, FontScript.Tamil];

    #endregion
}
