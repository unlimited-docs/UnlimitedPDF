using System.Globalization;
using System.Security.Cryptography;
using UnlimitedPDF.Models.Internal.Text;
using UnlimitedPDF.Models.Internal.Text.Bidi;

namespace UnlimitedPDF.Tests.Text;

/// <summary>
/// The Unicode conformance tests for the bidirectional algorithm (UAX #9, Unicode 17.0.0; WR-041, V-03): every case of
/// <c>BidiTest.txt</c> (classes, all paragraph directions) and <c>BidiCharacterTest.txt</c> (code points, brackets)
/// gives the expected levels and visual order. The files are not committed (PR-011); run
/// <c>dotnet run tools/unicode-tables/Generate.cs -- --conformance</c> to download them, pinned, into
/// <c>artifacts/unicode/</c>. Without them these tests are skipped.
/// </summary>
public class BidiConformanceTests
{
    #region Fields and Constants

    private static readonly char[] Separators = { ' ', '	' };

    #endregion

    #region Methods

    #region Public Methods

    [ConformanceFact]
    public void Every_bidi_test_case_resolves_as_expected()
    {
        string path = ConformanceFactAttribute.File("BidiTest.txt");
        int cases = 0;
        var failures = new List<string>();
        string[] levels = Array.Empty<string>();
        int[] reorder = Array.Empty<int>();
        foreach (string raw in System.IO.File.ReadLines(path))
        {
            string line = raw.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                levels = line["@Levels:".Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                continue;
            }
            if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                reorder = line["@Reorder:".Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                continue;
            }
            var parts = line.Split(';');
            var types = parts[0].Split(Separators, StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<BidiClass>).ToArray();
            int bits = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            foreach (var (bit, baseLevel) in new (int, int?)[] { (1, null), (2, 0), (4, 1) })
            {
                if ((bits & bit) == 0) continue;
                cases++;
                var paragraph = BidiParagraph.Create(types, null, baseLevel);
                string? failure = Compare(paragraph, levels, reorder);
                if (failure is not null && failures.Count < 20) failures.Add($"{parts[0].Trim()} (base {baseLevel?.ToString(CultureInfo.InvariantCulture) ?? "auto"}): {failure}");
                else if (failure is not null) failures.Add(string.Empty);
            }
        }

        Assert.True(cases > 400_000, $"Only {cases} cases read.");
        Assert.True(failures.Count == 0, $"{failures.Count} of {cases} cases failed:\n" + string.Join("\n", failures.Where(f => f.Length > 0)));
    }

    [ConformanceFact]
    public void Every_bidi_character_test_case_resolves_as_expected()
    {
        string path = ConformanceFactAttribute.File("BidiCharacterTest.txt");
        int cases = 0;
        var failures = new List<string>();
        foreach (string raw in System.IO.File.ReadLines(path))
        {
            string line = raw.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split(';');
            var codePoints = fields[0].Split(Separators, StringSplitOptions.RemoveEmptyEntries).Select(h => int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
            int? baseLevel = fields[1].Trim() switch { "0" => 0, "1" => 1, _ => null };
            int expectedParagraph = int.Parse(fields[2].Trim(), CultureInfo.InvariantCulture);
            var levels = fields[3].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            var order = fields[4].Split(Separators, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            cases++;

            var paragraph = BidiParagraph.Create(codePoints.Select(BidiData.ClassOf).ToArray(), codePoints, baseLevel);
            string? failure = paragraph.BaseLevel != expectedParagraph
                ? $"paragraph level {paragraph.BaseLevel}, expected {expectedParagraph}"
                : Compare(paragraph, levels, order);
            if (failure is not null && failures.Count < 20) failures.Add($"{fields[0].Trim()} (base {fields[1].Trim()}): {failure}");
            else if (failure is not null) failures.Add(string.Empty);
        }

        Assert.True(cases > 90_000, $"Only {cases} cases read.");
        Assert.True(failures.Count == 0, $"{failures.Count} of {cases} cases failed:\n" + string.Join("\n", failures.Where(f => f.Length > 0)));
    }

    [ConformanceFact]
    public void The_downloaded_files_match_the_pinned_hashes()
    {
        string generator = System.IO.File.ReadAllText(Path.Combine(BidiDataTests.Repository(), "tools", "unicode-tables", "Generate.cs"));
        foreach (string name in new[] { "BidiTest.txt", "BidiCharacterTest.txt" })
        {
            byte[] bytes = System.IO.File.ReadAllBytes(ConformanceFactAttribute.File(name));
            Assert.Equal(BidiDataTests.PinnedHash(generator, name), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    #endregion

    #region Private Methods

    private static string? Compare(BidiParagraph paragraph, IReadOnlyList<string> expectedLevels, IReadOnlyList<int> expectedOrder)
    {
        var levels = paragraph.LineLevels(0, paragraph.Count);
        for (int i = 0; i < expectedLevels.Count; i++)
        {
            string expected = expectedLevels[i];
            string actual = paragraph.IsRemoved(i) ? "x" : levels[i].ToString(CultureInfo.InvariantCulture);
            if (expected != actual) return $"levels {string.Join(' ', Enumerable.Range(0, paragraph.Count).Select(k => paragraph.IsRemoved(k) ? "x" : levels[k].ToString(CultureInfo.InvariantCulture)))}, expected {string.Join(' ', expectedLevels)}";
        }
        var order = paragraph.VisualOrder(0, paragraph.Count);
        return order.SequenceEqual(expectedOrder) ? null : $"order {string.Join(' ', order)}, expected {string.Join(' ', expectedOrder)}";
    }

    #endregion

    #endregion
}

/// <summary>
/// A fact that runs only when the Unicode conformance test files are in <c>artifacts/unicode/</c> (PR-011: they are not
/// committed); otherwise it is reported as skipped.
/// </summary>
public sealed class ConformanceFactAttribute : FactAttribute
{
    #region Constructors and Finalizers

    public ConformanceFactAttribute()
    {
        if (!System.IO.File.Exists(File("BidiTest.txt")) || !System.IO.File.Exists(File("BidiCharacterTest.txt")))
            Skip = "Unicode conformance files not downloaded; run: dotnet run tools/unicode-tables/Generate.cs -- --conformance";
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the path of a conformance file in <c>artifacts/unicode/</c>.
    /// </summary>
    public static string File(string name) => Path.Combine(BidiDataTests.Repository(), "artifacts", "unicode", name);

    #endregion

    #endregion
}
