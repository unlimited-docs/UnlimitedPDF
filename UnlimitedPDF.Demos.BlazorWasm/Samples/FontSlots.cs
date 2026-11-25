using System.Runtime.CompilerServices;
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

    // Letters each script's check looks for; a font with none of them has no letters of the script. Latin is checked
    // another way (see LatinLetters).
    private static readonly Dictionary<FontScript, string> Letters = new()
    {
        [FontScript.Greek] = "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩαβγδεζηθικλμνξοπρστυφχψω",
        [FontScript.Cyrillic] = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЫЭЮЯабвгдежзийклмнопрстуфхцчшщыэюя",
        [FontScript.Armenian] = "ԱԲԳԴԵԶԷԸԹԺԻԼԽԾԿՀՁՂՃՄաբգդեզէըթժիլխծկհձղճմ",
        [FontScript.Georgian] = "აბგდევზთიკლმნოპჟრსტუფქღყშჩცძწჭხჯჰ",
        [FontScript.Devanagari] = "कखगचजटडतदनपबमयरलवसह",
        [FontScript.Telugu] = "కఖగచజటడతదనపబమయరలవసహ",
        [FontScript.Tamil] = "கஙசஞடணதநபமயரலவழளறன",
        [FontScript.Bengali] = "কখগঘচছজঝটঠডঢতথদধনপবম",
        [FontScript.Gujarati] = "કખગઘચછજઝટઠડઢતથદધનપબમ",
        [FontScript.Gurmukhi] = "ਕਖਗਘਚਛਜਝਟਠਡਢਤਥਦਧਨਪਬਮ",
        [FontScript.Odia] = "କଖଗଘଚଛଜଝଟଠଡଢତଥଦଧନପବମ",
        [FontScript.Kannada] = "ಕಖಗಘಚಛಜಝಟಠಡಢತಥದಧನಪಬಮ",
        [FontScript.Malayalam] = "കഖഗഘചഛജഝടഠഡഢതഥദധനപബമ",
        [FontScript.Arabic] = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي",
        [FontScript.Hebrew] = "אבגדהוזחטיכלמנסעפצקרשת",
        [FontScript.Thaana] = "ހށނރބޅކއވމފދތލގޏސޑޒޓޔޕޖޗ"
    };

    // The built-in font draws every Western European letter a font lacks, so the library never reports a Latin letter
    // as missing. Instead the check draws these letters with the font and saves the page: a font with none of them is
    // not used, so no font is embedded.
    private const string LatinLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly ConditionalWeakTable<PdfFont, StrongBox<bool>> LatinCovered = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether a font has any letter of a script. For Latin it draws the letters with the font and checks that
    /// the saved page embeds a font, once per font.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="script">The script.</param>
    /// <returns>False when the font has none of the script's letters.</returns>
    public static bool Covers(PdfFont font, FontScript script)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (script == FontScript.Latin) return LatinCovered.GetValue(font, f => new StrongBox<bool>(EmbedsForLatin(f))).Value;
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
        FontScript.Armenian => "https://fonts.google.com/noto/specimen/Noto+Sans+Armenian",
        FontScript.Georgian => "https://fonts.google.com/noto/specimen/Noto+Sans+Georgian",
        FontScript.Devanagari => "https://fonts.google.com/noto/specimen/Noto+Sans+Devanagari",
        FontScript.Telugu => "https://fonts.google.com/noto/specimen/Noto+Sans+Telugu",
        FontScript.Tamil => "https://fonts.google.com/noto/specimen/Noto+Sans+Tamil",
        FontScript.Bengali => "https://fonts.google.com/noto/specimen/Noto+Sans+Bengali",
        FontScript.Gujarati => "https://fonts.google.com/noto/specimen/Noto+Sans+Gujarati",
        FontScript.Gurmukhi => "https://fonts.google.com/noto/specimen/Noto+Sans+Gurmukhi",
        FontScript.Odia => "https://fonts.google.com/noto/specimen/Noto+Sans+Oriya",
        FontScript.Kannada => "https://fonts.google.com/noto/specimen/Noto+Sans+Kannada",
        FontScript.Malayalam => "https://fonts.google.com/noto/specimen/Noto+Sans+Malayalam",
        FontScript.Arabic => "https://fonts.google.com/noto/specimen/Noto+Sans+Arabic",
        FontScript.Hebrew => "https://fonts.google.com/noto/specimen/Noto+Sans+Hebrew",
        FontScript.Thaana => "https://fonts.google.com/noto/specimen/Noto+Sans+Thaana",
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
        >= 'Ͱ' and <= 'Ͽ' or >= 'ἀ' and <= '῿' => FontScript.Greek,
        >= 'Ѐ' and <= 'ԯ' or >= 'ᲀ' and <= '᲏' or >= 'ⷠ' and <= 'ⷿ' or >= 'Ꙁ' and <= 'ꚟ' => FontScript.Cyrillic,
        >= 'Ա' and <= '֏' or >= 'ﬓ' and <= 'ﬗ' => FontScript.Armenian,
        >= 'Ⴀ' and <= 'ჿ' or >= 'Ა' and <= 'Ჿ' or >= 'ⴀ' and <= '⴯' => FontScript.Georgian,
        '।' or '॥' => null, // danda and double danda: several scripts use them
        >= 'ऀ' and <= 'ॿ' => FontScript.Devanagari,
        >= 'ఀ' and <= '౿' => FontScript.Telugu,
        >= '஀' and <= '௿' => FontScript.Tamil,
        >= 'ঀ' and <= '৿' => FontScript.Bengali,
        >= '઀' and <= '૿' => FontScript.Gujarati,
        >= '਀' and <= '੿' => FontScript.Gurmukhi,
        >= '଀' and <= '୿' => FontScript.Odia,
        >= 'ಀ' and <= '೿' => FontScript.Kannada,
        >= 'ഀ' and <= 'ൿ' => FontScript.Malayalam,
        // Arabic comma, semicolon, question mark, tatweel, and Arabic-Indic digits are shared, as in the library.
        '\u060C' or '\u061B' or '\u061F' or '\u0640' or >= '\u0660' and <= '\u0669' => null,
        >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F' or >= '\u08A0' and <= '\u08FF' or >= '\uFB50' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFE' => FontScript.Arabic,
        >= '\u0591' and <= '\u05F4' or >= '\uFB1D' and <= '\uFB4F' => FontScript.Hebrew,
        >= '\u0780' and <= '\u07BF' => FontScript.Thaana,
        _ => null
    };

    #endregion

    #region Private Methods

    private static bool EmbedsForLatin(PdfFont font)
    {
        var document = new PdfDocument();
        document.AddPage().AddText(LatinLetters, font, 72, 720);
        using var output = new MemoryStream();
        document.Save(output);
        return output.GetBuffer().AsSpan(0, (int)output.Length).IndexOf("/Subtype /Type0"u8) >= 0;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the scripts in slot order, which is the order the fonts are tried for each character.
    /// </summary>
    public static IReadOnlyList<FontScript> Scripts { get; } =
    [
        FontScript.Latin, FontScript.Greek, FontScript.Cyrillic, FontScript.Armenian, FontScript.Georgian, FontScript.Devanagari, FontScript.Telugu, FontScript.Tamil, FontScript.Bengali, FontScript.Gujarati,
        FontScript.Gurmukhi, FontScript.Odia, FontScript.Kannada, FontScript.Malayalam, FontScript.Arabic, FontScript.Hebrew, FontScript.Thaana
    ];

    #endregion
}
