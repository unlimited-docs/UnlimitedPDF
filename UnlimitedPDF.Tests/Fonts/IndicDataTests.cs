using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Text;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The generated Indic character tables (T-15, D-020, A-03): spot checks against the Unicode Character Database
/// 17.0.0 for Devanagari, Bengali, Gurmukhi, Gujarati, Oriya, Tamil, Telugu, Kannada, Malayalam, and the shared
/// characters those scripts use; the generator and the generated file name the same pinned version and hashes; the
/// Unicode notice ships with the package.
/// </summary>
public class IndicDataTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(0x0915, "Consonant", "None")] // DEVANAGARI KA
    [InlineData(0x0930, "Consonant", "None")] // DEVANAGARI RA (reph source)
    [InlineData(0x093F, "VowelDependent", "Left")] // DEVANAGARI VOWEL SIGN I (pre-base)
    [InlineData(0x0940, "VowelDependent", "Right")] // DEVANAGARI VOWEL SIGN II
    [InlineData(0x0941, "VowelDependent", "Bottom")] // DEVANAGARI VOWEL SIGN U
    [InlineData(0x0947, "VowelDependent", "Top")] // DEVANAGARI VOWEL SIGN E
    [InlineData(0x094D, "Virama", "Bottom")] // DEVANAGARI VIRAMA
    [InlineData(0x0902, "Bindu", "Top")] // DEVANAGARI ANUSVARA
    [InlineData(0x093C, "Nukta", "Bottom")] // DEVANAGARI NUKTA
    [InlineData(0x0905, "VowelIndependent", "None")] // DEVANAGARI LETTER A
    [InlineData(0x0C15, "Consonant", "None")] // TELUGU KA
    [InlineData(0x0C3F, "VowelDependent", "Top")] // TELUGU VOWEL SIGN I
    [InlineData(0x0C4D, "Virama", "Top")] // TELUGU VIRAMA
    [InlineData(0x0B95, "Consonant", "None")] // TAMIL KA
    [InlineData(0x0BC6, "VowelDependent", "Left")] // TAMIL VOWEL SIGN E
    [InlineData(0x0BCA, "VowelDependent", "LeftAndRight")] // TAMIL VOWEL SIGN O
    [InlineData(0x0BCD, "Virama", "Top")] // TAMIL VIRAMA (pulli)
    [InlineData(0x0995, "Consonant", "None")] // BENGALI KA
    [InlineData(0x09B0, "Consonant", "None")] // BENGALI RA (reph source)
    [InlineData(0x09F0, "Consonant", "None")] // BENGALI LETTER RA WITH MIDDLE DIAGONAL (Assamese Ra)
    [InlineData(0x09BF, "VowelDependent", "Left")] // BENGALI VOWEL SIGN I (pre-base)
    [InlineData(0x09CB, "VowelDependent", "LeftAndRight")] // BENGALI VOWEL SIGN O (split)
    [InlineData(0x09CE, "ConsonantDead", "None")] // BENGALI LETTER KHANDA TA
    [InlineData(0x09CD, "Virama", "Bottom")] // BENGALI VIRAMA
    [InlineData(0x0A15, "Consonant", "None")] // GURMUKHI KA
    [InlineData(0x0A3F, "VowelDependent", "Left")] // GURMUKHI VOWEL SIGN I (pre-base)
    [InlineData(0x0A70, "Bindu", "Top")] // GURMUKHI TIPPI
    [InlineData(0x0A71, "GeminationMark", "Top")] // GURMUKHI ADDAK
    [InlineData(0x0A95, "Consonant", "None")] // GUJARATI KA
    [InlineData(0x0ABF, "VowelDependent", "Left")] // GUJARATI VOWEL SIGN I (pre-base)
    [InlineData(0x0ACD, "Virama", "Bottom")] // GUJARATI VIRAMA
    [InlineData(0x0B15, "Consonant", "None")] // ORIYA KA
    [InlineData(0x0B47, "VowelDependent", "Left")] // ORIYA VOWEL SIGN E (pre-base)
    [InlineData(0x0B4B, "VowelDependent", "LeftAndRight")] // ORIYA VOWEL SIGN O (split)
    [InlineData(0x0C95, "Consonant", "None")] // KANNADA KA
    [InlineData(0x0CC0, "VowelDependent", "TopAndRight")] // KANNADA VOWEL SIGN II (split)
    [InlineData(0x0CCD, "Virama", "Top")] // KANNADA VIRAMA
    [InlineData(0x0D15, "Consonant", "None")] // MALAYALAM KA
    [InlineData(0x0D46, "VowelDependent", "Left")] // MALAYALAM VOWEL SIGN E (pre-base)
    [InlineData(0x0D4A, "VowelDependent", "LeftAndRight")] // MALAYALAM VOWEL SIGN O (split)
    [InlineData(0x0D4D, "Virama", "Top")] // MALAYALAM VIRAMA
    [InlineData(0x0D4E, "ConsonantPrecedingRepha", "Top")] // MALAYALAM LETTER DOT REPH
    [InlineData(0x0D7D, "ConsonantDead", "None")] // MALAYALAM LETTER CHILLU L
    [InlineData(0x200C, "NonJoiner", "None")] // ZERO WIDTH NON-JOINER
    [InlineData(0x200D, "Joiner", "None")] // ZERO WIDTH JOINER
    [InlineData(0x25CC, "ConsonantPlaceholder", "None")] // DOTTED CIRCLE
    [InlineData(0x00A0, "ConsonantPlaceholder", "None")] // NO-BREAK SPACE
    [InlineData('A', "Other", "None")]
    [InlineData(0x0964, "Other", "None")] // DEVANAGARI DANDA (punctuation)
    [InlineData(0x1F642, "Other", "None")]
    public void Characters_have_their_unicode_categories(int codePoint, string syllabic, string positional)
    {
        Assert.Equal(Enum.Parse<IndicSyllabicCategory>(syllabic), IndicData.Syllabic(codePoint));
        Assert.Equal(Enum.Parse<IndicPositionalCategory>(positional), IndicData.Positional(codePoint));
    }

    [Fact]
    public void Every_devanagari_telugu_and_tamil_consonant_is_a_consonant()
    {
        foreach (var (first, last) in new[] { (0x0915, 0x0939), (0x0C15, 0x0C28), (0x0B95, 0x0B95), (0x0BA9, 0x0BAA) })
        {
            for (int cp = first; cp <= last; cp++) Assert.Equal(IndicSyllabicCategory.Consonant, IndicData.Syllabic(cp));
        }
    }

    [Fact]
    public void The_generator_and_the_generated_file_pin_the_same_data()
    {
        string repository = Repository();
        string generator = File.ReadAllText(Path.Combine(repository, "tools", "unicode-tables", "Generate.cs"));
        string generated = File.ReadAllText(Path.Combine(repository, "UnlimitedPDF", "Models", "Internal", "Text", "IndicData.g.cs"));

        var hashes = Regex.Matches(generator, "\"([0-9a-f]{64})\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, hashes.Count);
        Assert.All(hashes, hash => Assert.Contains(hash, generated));
        Assert.Contains("const string Version = \"17.0.0\";", generator);
        Assert.Contains("Unicode Character Database 17.0.0", generated);
    }

    [Fact]
    public void The_unicode_notice_ships_with_the_package()
    {
        string repository = Repository();
        string notices = File.ReadAllText(Path.Combine(repository, "THIRD-PARTY-NOTICES.txt"));
        string project = File.ReadAllText(Path.Combine(repository, "UnlimitedPDF", "UnlimitedPDF.csproj"));

        Assert.Contains("UNICODE LICENSE V3", notices);
        Assert.Contains("COPYRIGHT AND PERMISSION NOTICE", notices);
        Assert.Contains("IndicData.g.cs", notices);
        Assert.Matches(@"<None Include=""\.\.\\THIRD-PARTY-NOTICES\.txt"">\s*<Pack>True</Pack>", project);
    }

    #endregion

    #region Private Methods

    // The repository root: two folders above this source file (UnlimitedPDF.Tests/Fonts).
    private static string Repository([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    #endregion

    #endregion
}
