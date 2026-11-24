using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Text;

namespace UnlimitedPDF.Tests.Text;

/// <summary>
/// The generated bidirectional and joining tables (WR-041, V-02): spot checks against the Unicode Character Database
/// 17.0.0 for bidirectional classes, paired brackets, mirrored characters, and joining types; the generator and the
/// generated files name the same pinned version and hashes; the Unicode notice names the new tables.
/// </summary>
public class BidiDataTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(0x0041, "L")] // LATIN CAPITAL LETTER A
    [InlineData(0x05D0, "R")] // HEBREW LETTER ALEF
    [InlineData(0x0627, "AL")] // ARABIC LETTER ALEF
    [InlineData(0x0780, "AL")] // THAANA LETTER HAA
    [InlineData(0x0710, "AL")] // SYRIAC LETTER ALAPH
    [InlineData(0x07CA, "R")] // NKO LETTER A
    [InlineData(0x0031, "EN")] // DIGIT ONE
    [InlineData(0x06F1, "EN")] // EXTENDED ARABIC-INDIC DIGIT ONE
    [InlineData(0x0661, "AN")] // ARABIC-INDIC DIGIT ONE
    [InlineData(0x002B, "ES")] // PLUS SIGN
    [InlineData(0x0024, "ET")] // DOLLAR SIGN
    [InlineData(0x20AA, "ET")] // NEW SHEQEL SIGN
    [InlineData(0x002C, "CS")] // COMMA
    [InlineData(0x064E, "NSM")] // ARABIC FATHA
    [InlineData(0x05B8, "NSM")] // HEBREW POINT QAMATS
    [InlineData(0x07A6, "NSM")] // THAANA ABAFILI
    [InlineData(0x200D, "BN")] // ZERO WIDTH JOINER
    [InlineData(0x000A, "B")] // LINE FEED
    [InlineData(0x0009, "S")] // CHARACTER TABULATION
    [InlineData(0x0020, "WS")] // SPACE
    [InlineData(0x0028, "ON")] // LEFT PARENTHESIS
    [InlineData(0x200E, "L")] // LEFT-TO-RIGHT MARK
    [InlineData(0x200F, "R")] // RIGHT-TO-LEFT MARK
    [InlineData(0x061C, "AL")] // ARABIC LETTER MARK
    [InlineData(0x202A, "LRE")]
    [InlineData(0x202D, "LRO")]
    [InlineData(0x202B, "RLE")]
    [InlineData(0x202E, "RLO")]
    [InlineData(0x202C, "PDF")]
    [InlineData(0x2066, "LRI")]
    [InlineData(0x2067, "RLI")]
    [InlineData(0x2068, "FSI")]
    [InlineData(0x2069, "PDI")]
    [InlineData(0x05FF, "R")] // unassigned in the Hebrew block: the block's default
    [InlineData(0x08A0, "AL")] // ARABIC LETTER BEH WITH SMALL V BELOW
    [InlineData(0xFEFC, "AL")] // ARABIC LIGATURE LAM WITH ALEF FINAL FORM
    [InlineData(0x1F600, "ON")] // GRINNING FACE
    public void Bidirectional_classes_match_the_database(int codePoint, string expected) =>
        Assert.Equal(expected, BidiData.ClassOf(codePoint).ToString());

    [Theory]
    [InlineData(0x0028, 0x0029, true)]
    [InlineData(0x0029, 0x0028, false)]
    [InlineData(0x005B, 0x005D, true)]
    [InlineData(0x007D, 0x007B, false)]
    [InlineData(0x2329, 0x232A, true)]
    [InlineData(0x3009, 0x3008, false)]
    public void Paired_brackets_match_the_database(int codePoint, int pair, bool opening)
    {
        Assert.True(BidiData.TryGetBracket(codePoint, out int actualPair, out bool actualOpening));
        Assert.Equal(pair, actualPair);
        Assert.Equal(opening, actualOpening);
    }

    [Fact]
    public void A_character_that_is_not_a_bracket_has_no_pair()
    {
        Assert.False(BidiData.TryGetBracket('<', out int pair, out _));
        Assert.Equal(-1, pair);
    }

    [Theory]
    [InlineData(0x0028, 0x0029)]
    [InlineData(0x003C, 0x003E)]
    [InlineData(0x00AB, 0x00BB)]
    [InlineData(0x2264, 0x2265)]
    [InlineData(0x0041, -1)]
    public void Mirrored_characters_match_the_database(int codePoint, int mirror) =>
        Assert.Equal(mirror, BidiData.MirrorOf(codePoint));

    [Theory]
    [InlineData(0x0627, "RightJoining")] // ARABIC LETTER ALEF
    [InlineData(0x0628, "DualJoining")] // ARABIC LETTER BEH
    [InlineData(0x0644, "DualJoining")] // ARABIC LETTER LAM
    [InlineData(0x0648, "RightJoining")] // ARABIC LETTER WAW
    [InlineData(0x06CC, "DualJoining")] // ARABIC LETTER FARSI YEH
    [InlineData(0x06D2, "RightJoining")] // ARABIC LETTER YEH BARREE (Urdu)
    [InlineData(0x0640, "JoinCausing")] // ARABIC TATWEEL
    [InlineData(0x200D, "JoinCausing")] // ZERO WIDTH JOINER
    [InlineData(0x0621, "NonJoining")] // ARABIC LETTER HAMZA
    [InlineData(0x064E, "Transparent")] // ARABIC FATHA (unlisted, nonspacing mark)
    [InlineData(0x0670, "Transparent")] // ARABIC LETTER SUPERSCRIPT ALEF
    [InlineData(0x200C, "NonJoining")] // ZERO WIDTH NON-JOINER (listed U)
    [InlineData(0x0041, "NonJoining")] // LATIN CAPITAL LETTER A
    [InlineData(0x0020, "NonJoining")] // SPACE
    public void Joining_types_match_the_database(int codePoint, string expected) =>
        Assert.Equal(expected, JoiningData.TypeOf(codePoint).ToString());

    [Fact]
    public void The_generator_and_the_generated_files_pin_the_same_data()
    {
        string repository = Repository();
        string generator = File.ReadAllText(Path.Combine(repository, "tools", "unicode-tables", "Generate.cs"));
        string bidi = File.ReadAllText(Path.Combine(repository, "UnlimitedPDF", "Models", "Internal", "Text", "BidiData.g.cs"));
        string joining = File.ReadAllText(Path.Combine(repository, "UnlimitedPDF", "Models", "Internal", "Text", "JoiningData.g.cs"));

        foreach (string name in new[] { "extracted/DerivedBidiClass.txt", "BidiBrackets.txt", "BidiMirroring.txt" })
            Assert.Contains($"//   {name} (SHA-256 {PinnedHash(generator, name)})", bidi);
        Assert.Contains($"//   ArabicShaping.txt (SHA-256 {PinnedHash(generator, "ArabicShaping.txt")})", joining);
        Assert.Contains("Unicode Character Database 17.0.0", bidi);
        Assert.Contains("Unicode Character Database 17.0.0", joining);
    }

    [Fact]
    public void The_unicode_notice_names_the_new_tables()
    {
        string notices = File.ReadAllText(Path.Combine(Repository(), "THIRD-PARTY-NOTICES.txt"));

        Assert.Contains("BidiData.g.cs", notices);
        Assert.Contains("JoiningData.g.cs", notices);
        Assert.Contains("ArabicShaping.txt", notices);
    }

    #endregion

    #region Internal Methods

    internal static string PinnedHash(string generator, string name) =>
        Regex.Match(generator, "\\[\"" + Regex.Escape(name) + "\"\\] = \"([0-9a-f]{64})\"").Groups[1].Value is { Length: 64 } hash
            ? hash
            : throw new InvalidOperationException($"{name} is not pinned in the generator.");

    internal static string Repository([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    #endregion

    #endregion
}
