// Generates the library's Unicode tables from the Unicode Character Database (D-020, A-03; WR-041):
//   UnlimitedPDF/Models/Internal/Text/IndicData.g.cs   from IndicSyllabicCategory.txt and IndicPositionalCategory.txt
//   UnlimitedPDF/Models/Internal/Text/BidiData.g.cs    from extracted/DerivedBidiClass.txt, BidiBrackets.txt, and
//                                                      BidiMirroring.txt
//   UnlimitedPDF/Models/Internal/Text/JoiningData.g.cs from ArabicShaping.txt
//
// Run from the repository root:
//     dotnet run tools/unicode-tables/Generate.cs
//     dotnet run tools/unicode-tables/Generate.cs -- --from <folder holding the .txt files>
//     dotnet run tools/unicode-tables/Generate.cs -- --conformance
//
// The tool reads the files of the pinned UCD version, checks their SHA-256 hashes, and writes compact tables. For the
// Indic table it keeps the characters the Indic shaper handles (Devanagari, Devanagari Extended, Bengali, Gurmukhi,
// Gujarati, Oriya, Tamil, Telugu, Kannada, Malayalam, and the joiners, dotted circle, and no-break space those scripts
// use). The files are read here only; they are not committed and never ship. Running it again gives the same output.
//
// --conformance downloads the pinned bidirectional conformance test files (BidiTest.txt, BidiCharacterTest.txt) into
// artifacts/unicode/ (git-ignored), where UnlimitedPDF.Tests reads them; they are not committed (PR-011).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

const string Version = "17.0.0";
var files = new Dictionary<string, string>
{
    ["IndicSyllabicCategory.txt"] = "3fc122f4cf58b0c19268d5f810263b04ab4e1e67743386ec0e0ada9c76aec5be",
    ["IndicPositionalCategory.txt"] = "68cedc29a7e57f984d90fe2c7712f2e6d0c717e253db219607daea8997d6c480"
};
const string Output = "UnlimitedPDF/Models/Internal/Text/IndicData.g.cs";

// The ranges the shaper looks up; every other character is "other".
var covered = new (int First, int Last)[]
{
    (0x00A0, 0x00A0), // NO-BREAK SPACE (a base for a lone vowel sign)
    (0x0900, 0x097F), // Devanagari
    (0x0980, 0x09FF), // Bengali (Bengali-Assamese)
    (0x0A00, 0x0A7F), // Gurmukhi
    (0x0A80, 0x0AFF), // Gujarati
    (0x0B00, 0x0B7F), // Oriya (Odia)
    (0x0B80, 0x0BFF), // Tamil
    (0x0C00, 0x0C7F), // Telugu
    (0x0C80, 0x0CFF), // Kannada
    (0x0D00, 0x0D7F), // Malayalam
    (0x200C, 0x200D), // ZERO WIDTH NON-JOINER, ZERO WIDTH JOINER
    (0x25CC, 0x25CC), // DOTTED CIRCLE
    (0xA8E0, 0xA8FF)  // Devanagari Extended
};

// The bidirectional and joining sources (WR-041), kept apart so the Indic table's header is unchanged.
var bidiFiles = new Dictionary<string, string>
{
    ["extracted/DerivedBidiClass.txt"] = "4867b4b7f0731ed1bfcd34cc6251211ff1542541fce0734b6fbda139ee80b3a4",
    ["BidiBrackets.txt"] = "dadbaf38a0d0246e5b805bf8725cb81b7c621f93d030595635f5ba2c2f179428",
    ["BidiMirroring.txt"] = "a2f16fb873ab4fcdf3221cb1a8a85a134ddd6ed03603181823ff5206af3741ce"
};
var joiningFiles = new Dictionary<string, string>
{
    ["ArabicShaping.txt"] = "39afa01e680e27d0fd10b67a9b27be13fbaa3d0efecfb5be45991de9a0d267d0"
};
var conformanceFiles = new Dictionary<string, string>
{
    ["BidiTest.txt"] = "888bdfc8090652272d1f859cdb00ae659e2dc6c26740be61ef1d03998a687620",
    ["BidiCharacterTest.txt"] = "a3e6e905ab5afbe318a96df5401d0372a04cd73ef139ab5e3cf0ae241c255488"
};
const string BidiOutput = "UnlimitedPDF/Models/Internal/Text/BidiData.g.cs";
const string JoiningOutput = "UnlimitedPDF/Models/Internal/Text/JoiningData.g.cs";
const string ConformanceFolder = "artifacts/unicode";

string? from = args.Length >= 2 && args[0] == "--from" ? args[1] : null;
bool conformance = args.Contains("--conformance");
var text = new Dictionary<string, string>();
using (var http = new HttpClient())
{
    var wanted = conformance ? conformanceFiles : files.Concat(bidiFiles).Concat(joiningFiles).ToDictionary(p => p.Key, p => p.Value);
    foreach (var (name, hash) in wanted)
    {
        byte[] bytes = from is null
            ? await http.GetByteArrayAsync($"https://www.unicode.org/Public/{Version}/ucd/{name}")
            : await File.ReadAllBytesAsync(Path.Combine(from, Path.GetFileName(name)));
        string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (actual != hash)
        {
            Console.Error.WriteLine($"{name}: SHA-256 {actual} differs from the pinned {hash}.");
            return 1;
        }
        if (conformance)
        {
            Directory.CreateDirectory(ConformanceFolder);
            await File.WriteAllBytesAsync(Path.Combine(ConformanceFolder, name), bytes);
            Console.WriteLine($"Wrote {ConformanceFolder}/{name} (UCD {Version}).");
        }
        text[name] = Encoding.UTF8.GetString(bytes);
    }
}
if (conformance) return 0;

var syllabic = Parse(text["IndicSyllabicCategory.txt"]);
var positional = Parse(text["IndicPositionalCategory.txt"]);
var syllabicNames = new[] { "Other" }.Concat(syllabic.Values.Distinct().Where(v => v != "Other").OrderBy(v => v, StringComparer.Ordinal)).ToList();
var positionalNames = new[] { "None" }.Concat(positional.Values.Distinct().Where(v => v != "NA").OrderBy(v => v, StringComparer.Ordinal)).ToList();

// Runs of consecutive covered characters with the same two values.
var runs = new List<(int First, int Last, int Syllabic, int Position)>();
foreach (var (first, last) in covered)
{
    for (int cp = first; cp <= last; cp++)
    {
        int s = syllabic.TryGetValue(cp, out var sv) ? syllabicNames.IndexOf(sv) : 0;
        int p = positional.TryGetValue(cp, out var pv) && pv != "NA" ? positionalNames.IndexOf(pv) : 0;
        if (s == 0 && p == 0) continue;
        if (runs.Count > 0 && runs[^1].Last == cp - 1 && runs[^1].Syllabic == s && runs[^1].Position == p)
            runs[^1] = (runs[^1].First, cp, s, p);
        else
            runs.Add((cp, cp, s, p));
    }
}

var sb = new StringBuilder();
sb.Append("// <auto-generated>\n");
sb.Append("// Generated by tools/unicode-tables/Generate.cs from the Unicode Character Database ").Append(Version).Append(":\n");
foreach (var (name, hash) in files) sb.Append("//   ").Append(name).Append(" (SHA-256 ").Append(hash).Append(")\n");
sb.Append("// Do not edit; run the tool again instead.\n// </auto-generated>\n//\n");
sb.Append("// The data is derived from Unicode Data Files, used under the Unicode License v3; its full copyright and\n");
sb.Append("// permission notice is in THIRD-PARTY-NOTICES.txt at the repository root and in the NuGet package.\n\n");
sb.Append("namespace UnlimitedPDF.Models.Internal.Text;\n\n");
sb.Append("/// <summary>\n/// The Unicode Indic_Syllabic_Category of a character (UCD ").Append(Version).Append(").\n/// </summary>\n");
sb.Append("internal enum IndicSyllabicCategory : byte\n{\n");
sb.Append(string.Join(",\n", syllabicNames.Select(n => "    " + Pascal(n)))).Append("\n}\n\n");
sb.Append("/// <summary>\n/// The Unicode Indic_Positional_Category of a character (UCD ").Append(Version).Append("); None for NA.\n/// </summary>\n");
sb.Append("internal enum IndicPositionalCategory : byte\n{\n");
sb.Append(string.Join(",\n", positionalNames.Select(n => "    " + Pascal(n)))).Append("\n}\n\n");
sb.Append("/// <summary>\n/// Looks up the Indic categories of Devanagari, Bengali, Gurmukhi, Gujarati, Oriya, Tamil, Telugu, Kannada, and\n");
sb.Append("/// Malayalam characters and the shared characters those scripts use; every other character is Other and None.\n/// </summary>\n");
sb.Append("internal static class IndicData\n{\n");
sb.Append("    #region Fields and Constants\n\n");
sb.Append("    // (first code point, last code point, syllabic category, positional category), sorted, non-overlapping.\n");
sb.Append("    private static readonly (int First, int Last, byte Syllabic, byte Position)[] Ranges =\n    {\n");
foreach (var r in runs)
{
    sb.Append(string.Format(CultureInfo.InvariantCulture, "        (0x{0:X4}, 0x{1:X4}, {2}, {3}), // {4}{5}\n",
        r.First, r.Last, r.Syllabic, r.Position, syllabicNames[r.Syllabic], r.Position == 0 ? string.Empty : ", " + positionalNames[r.Position]));
}
sb.Append("    };\n\n    #endregion\n\n    #region Methods\n\n    #region Public Methods\n\n");
sb.Append("    public static IndicPositionalCategory Positional(int codePoint) => (IndicPositionalCategory)(Find(codePoint)?.Position ?? 0);\n\n");
sb.Append("    public static IndicSyllabicCategory Syllabic(int codePoint) => (IndicSyllabicCategory)(Find(codePoint)?.Syllabic ?? 0);\n\n");
sb.Append("    #endregion\n\n    #region Private Methods\n\n");
sb.Append("    private static (int First, int Last, byte Syllabic, byte Position)? Find(int codePoint)\n    {\n");
sb.Append("        int low = 0, high = Ranges.Length - 1;\n        while (low <= high)\n        {\n");
sb.Append("            int mid = (low + high) >> 1;\n");
sb.Append("            if (Ranges[mid].Last < codePoint) low = mid + 1;\n");
sb.Append("            else if (Ranges[mid].First > codePoint) high = mid - 1;\n");
sb.Append("            else return Ranges[mid];\n        }\n        return null;\n    }\n\n");
sb.Append("    #endregion\n\n    #endregion\n}\n");

await File.WriteAllTextAsync(Output, sb.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Wrote {Output}: {runs.Count} ranges, {syllabicNames.Count} syllabic and {positionalNames.Count} positional values (UCD {Version}).");

// ---- BidiData.g.cs: bidirectional classes, paired brackets, mirrored characters (UAX #9) ----
var bidiNames = new[] { "L", "R", "AL", "EN", "ES", "ET", "AN", "CS", "NSM", "BN", "B", "S", "WS", "ON", "LRE", "LRO", "RLE", "RLO", "PDF", "LRI", "RLI", "FSI", "PDI" };
var longNames = new Dictionary<string, string>
{
    ["Left_To_Right"] = "L", ["Right_To_Left"] = "R", ["Arabic_Letter"] = "AL", ["European_Number"] = "EN",
    ["European_Separator"] = "ES", ["European_Terminator"] = "ET", ["Arabic_Number"] = "AN", ["Common_Separator"] = "CS",
    ["Nonspacing_Mark"] = "NSM", ["Boundary_Neutral"] = "BN", ["Paragraph_Separator"] = "B", ["Segment_Separator"] = "S",
    ["White_Space"] = "WS", ["Other_Neutral"] = "ON"
};
var bidiClass = new byte[0x110000];
// Defaults first, in file order (later @missing lines narrow earlier ones), then the listed values.
foreach (string raw in text["extracted/DerivedBidiClass.txt"].Split('\n'))
{
    if (!raw.StartsWith("# @missing:", StringComparison.Ordinal)) continue;
    var parts = raw["# @missing:".Length..].Split(';');
    var (first, last) = Range(parts[0]);
    string name = parts[1].Trim();
    byte value = (byte)Array.IndexOf(bidiNames, longNames.TryGetValue(name, out var shortName) ? shortName : name);
    for (int cp = first; cp <= last; cp++) bidiClass[cp] = value;
}
foreach (var (cp, value) in Parse(text["extracted/DerivedBidiClass.txt"])) bidiClass[cp] = (byte)Array.IndexOf(bidiNames, value);
if (bidiClass.Any(b => b == 255)) { Console.Error.WriteLine("Unknown bidirectional class."); return 1; }

var bidiRuns = new List<(int First, int Last, byte Class)>();
for (int cp = 0; cp < bidiClass.Length; cp++)
{
    if (bidiClass[cp] == 0) continue; // L is the default
    if (bidiRuns.Count > 0 && bidiRuns[^1].Last == cp - 1 && bidiRuns[^1].Class == bidiClass[cp]) bidiRuns[^1] = (bidiRuns[^1].First, cp, bidiClass[cp]);
    else bidiRuns.Add((cp, cp, bidiClass[cp]));
}

var brackets = new List<(int CodePoint, int Pair, bool Opening)>();
foreach (string raw in text["BidiBrackets.txt"].Split('\n'))
{
    string line = raw.Split('#')[0].Trim();
    if (line.Length == 0) continue;
    var parts = line.Split(';');
    brackets.Add((Hex(parts[0]), Hex(parts[1]), parts[2].Trim() == "o"));
}
var mirrors = new List<(int CodePoint, int Mirror)>();
foreach (string raw in text["BidiMirroring.txt"].Split('\n'))
{
    string line = raw.Split('#')[0].Trim();
    if (line.Length == 0) continue;
    var parts = line.Split(';');
    mirrors.Add((Hex(parts[0]), Hex(parts[1])));
}

sb.Clear();
AppendHeader(sb, bidiFiles);
sb.Append("namespace UnlimitedPDF.Models.Internal.Text;\n\n");
sb.Append("/// <summary>\n/// The Unicode Bidi_Class of a character (UAX #9, UCD ").Append(Version).Append(").\n/// </summary>\n");
sb.Append("internal enum BidiClass : byte\n{\n");
sb.Append(string.Join(",\n", bidiNames.Select(n => "    " + n))).Append("\n}\n\n");
sb.Append("/// <summary>\n/// Looks up the bidirectional class, paired bracket, and mirrored character of a code point.\n/// </summary>\n");
sb.Append("internal static class BidiData\n{\n");
sb.Append("    #region Fields and Constants\n\n");
sb.Append("    // (first code point, last code point, class) for every class but L, sorted, non-overlapping.\n");
sb.Append("    private static readonly (int First, int Last, BidiClass Class)[] Classes =\n    {\n");
foreach (var r in bidiRuns)
    sb.Append(string.Format(CultureInfo.InvariantCulture, "        (0x{0:X4}, 0x{1:X4}, BidiClass.{2}),\n", r.First, r.Last, bidiNames[r.Class]));
sb.Append("    };\n\n");
sb.Append("    // (code point, paired bracket, opening), sorted by code point (BidiBrackets.txt).\n");
sb.Append("    private static readonly (int CodePoint, int Pair, bool Opening)[] Brackets =\n    {\n");
foreach (var b in brackets.OrderBy(b => b.CodePoint))
    sb.Append(string.Format(CultureInfo.InvariantCulture, "        (0x{0:X4}, 0x{1:X4}, {2}),\n", b.CodePoint, b.Pair, b.Opening ? "true" : "false"));
sb.Append("    };\n\n");
sb.Append("    // (code point, mirrored glyph character), sorted by code point (BidiMirroring.txt).\n");
sb.Append("    private static readonly (int CodePoint, int Mirror)[] Mirrors =\n    {\n");
foreach (var m in mirrors.OrderBy(m => m.CodePoint))
    sb.Append(string.Format(CultureInfo.InvariantCulture, "        (0x{0:X4}, 0x{1:X4}),\n", m.CodePoint, m.Mirror));
sb.Append("    };\n\n    #endregion\n\n    #region Methods\n\n    #region Public Methods\n\n");
sb.Append("    /// <summary>\n    /// Returns the bidirectional class of <paramref name=\"codePoint\"/>.\n    /// </summary>\n");
sb.Append("    public static BidiClass ClassOf(int codePoint)\n    {\n");
sb.Append("        int low = 0, high = Classes.Length - 1;\n        while (low <= high)\n        {\n");
sb.Append("            int mid = (low + high) >> 1;\n");
sb.Append("            if (Classes[mid].Last < codePoint) low = mid + 1;\n");
sb.Append("            else if (Classes[mid].First > codePoint) high = mid - 1;\n");
sb.Append("            else return Classes[mid].Class;\n        }\n        return BidiClass.L;\n    }\n\n");
sb.Append("    /// <summary>\n    /// Returns whether <paramref name=\"codePoint\"/> is a paired bracket, with its pair and whether it opens.\n    /// </summary>\n");
sb.Append("    public static bool TryGetBracket(int codePoint, out int pair, out bool opening)\n    {\n");
sb.Append("        int low = 0, high = Brackets.Length - 1;\n        while (low <= high)\n        {\n");
sb.Append("            int mid = (low + high) >> 1;\n");
sb.Append("            if (Brackets[mid].CodePoint < codePoint) low = mid + 1;\n");
sb.Append("            else if (Brackets[mid].CodePoint > codePoint) high = mid - 1;\n");
sb.Append("            else { pair = Brackets[mid].Pair; opening = Brackets[mid].Opening; return true; }\n        }\n");
sb.Append("        pair = -1;\n        opening = false;\n        return false;\n    }\n\n");
sb.Append("    /// <summary>\n    /// Returns the character whose glyph mirrors <paramref name=\"codePoint\"/>, or -1 when it has none.\n    /// </summary>\n");
sb.Append("    public static int MirrorOf(int codePoint)\n    {\n");
sb.Append("        int low = 0, high = Mirrors.Length - 1;\n        while (low <= high)\n        {\n");
sb.Append("            int mid = (low + high) >> 1;\n");
sb.Append("            if (Mirrors[mid].CodePoint < codePoint) low = mid + 1;\n");
sb.Append("            else if (Mirrors[mid].CodePoint > codePoint) high = mid - 1;\n");
sb.Append("            else return Mirrors[mid].Mirror;\n        }\n        return -1;\n    }\n\n");
sb.Append("    #endregion\n\n    #endregion\n}\n");
await File.WriteAllTextAsync(BidiOutput, sb.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Wrote {BidiOutput}: {bidiRuns.Count} class ranges, {brackets.Count} brackets, {mirrors.Count} mirrors (UCD {Version}).");

// ---- JoiningData.g.cs: Arabic-script joining types listed in ArabicShaping.txt ----
// Unlisted characters are Transparent when their general category is Mn, Me, or Cf, else Non_Joining; the library
// derives that at run time (JoiningData.TypeOf), so only listed characters are stored.
var joiningNames = new[] { "U", "R", "L", "D", "C", "T" };
var joiningLong = new[] { "NonJoining", "RightJoining", "LeftJoining", "DualJoining", "JoinCausing", "Transparent" };
var joining = new List<(int First, int Last, int Type)>();
foreach (string raw in text["ArabicShaping.txt"].Split('\n'))
{
    string line = raw.Split('#')[0].Trim();
    if (line.Length == 0) continue;
    var parts = line.Split(';');
    int cp = Hex(parts[0]);
    int type = Array.IndexOf(joiningNames, parts[2].Trim());
    if (type < 0) { Console.Error.WriteLine($"Unknown joining type {parts[2]}."); return 1; }
    if (joining.Count > 0 && joining[^1].Last == cp - 1 && joining[^1].Type == type) joining[^1] = (joining[^1].First, cp, type);
    else joining.Add((cp, cp, type));
}
joining.Sort((a, b) => a.First.CompareTo(b.First));

sb.Clear();
AppendHeader(sb, joiningFiles);
sb.Append("namespace UnlimitedPDF.Models.Internal.Text;\n\n");
sb.Append("/// <summary>\n/// The Unicode Joining_Type of a character (UCD ").Append(Version).Append(").\n/// </summary>\n");
sb.Append("internal enum JoiningType : byte\n{\n");
sb.Append(string.Join(",\n", joiningLong.Select(n => "    " + n))).Append("\n}\n\n");
sb.Append("/// <summary>\n/// Looks up the joining type of a character: the value ArabicShaping.txt lists, else Transparent for a\n");
sb.Append("/// nonspacing or enclosing mark or a format character, else NonJoining.\n/// </summary>\n");
sb.Append("internal static class JoiningData\n{\n");
sb.Append("    #region Fields and Constants\n\n");
sb.Append("    // (first code point, last code point, type) for every listed character, sorted, non-overlapping.\n");
sb.Append("    private static readonly (int First, int Last, JoiningType Type)[] Types =\n    {\n");
foreach (var j in joining)
    sb.Append(string.Format(CultureInfo.InvariantCulture, "        (0x{0:X4}, 0x{1:X4}, JoiningType.{2}),\n", j.First, j.Last, joiningLong[j.Type]));
sb.Append("    };\n\n    #endregion\n\n    #region Methods\n\n    #region Public Methods\n\n");
sb.Append("    /// <summary>\n    /// Returns the joining type of <paramref name=\"codePoint\"/>.\n    /// </summary>\n");
sb.Append("    public static JoiningType TypeOf(int codePoint)\n    {\n");
sb.Append("        int low = 0, high = Types.Length - 1;\n        while (low <= high)\n        {\n");
sb.Append("            int mid = (low + high) >> 1;\n");
sb.Append("            if (Types[mid].Last < codePoint) low = mid + 1;\n");
sb.Append("            else if (Types[mid].First > codePoint) high = mid - 1;\n");
sb.Append("            else return Types[mid].Type;\n        }\n");
sb.Append("        if (codePoint is < 0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF)) return JoiningType.NonJoining;\n");
sb.Append("        return CharUnicodeInfo.GetUnicodeCategory(codePoint) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format\n");
sb.Append("            ? JoiningType.Transparent\n            : JoiningType.NonJoining;\n    }\n\n");
sb.Append("    #endregion\n\n    #endregion\n}\n");
await File.WriteAllTextAsync(JoiningOutput, sb.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Wrote {JoiningOutput}: {joining.Count} ranges (UCD {Version}).");
return 0;

static void AppendHeader(StringBuilder sb, Dictionary<string, string> sources)
{
    sb.Append("// <auto-generated>\n");
    sb.Append("// Generated by tools/unicode-tables/Generate.cs from the Unicode Character Database ").Append(Version).Append(":\n");
    foreach (var (name, hash) in sources) sb.Append("//   ").Append(name).Append(" (SHA-256 ").Append(hash).Append(")\n");
    sb.Append("// Do not edit; run the tool again instead.\n// </auto-generated>\n//\n");
    sb.Append("// The data is derived from Unicode Data Files, used under the Unicode License v3; its full copyright and\n");
    sb.Append("// permission notice is in THIRD-PARTY-NOTICES.txt at the repository root and in the NuGet package.\n\n");
}

static int Hex(string value) => int.Parse(value.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

static (int First, int Last) Range(string value)
{
    var range = value.Trim().Split("..");
    int first = Hex(range[0]);
    return (first, range.Length > 1 ? Hex(range[1]) : first);
}

// "0915..0939 ; Consonant # ..." lines to a code point map.
static Dictionary<int, string> Parse(string file)
{
    var map = new Dictionary<int, string>();
    foreach (string raw in file.Split('\n'))
    {
        string line = raw.Split('#')[0].Trim();
        if (line.Length == 0) continue;
        var parts = line.Split(';');
        string value = parts[1].Trim();
        var range = parts[0].Trim().Split("..");
        int first = int.Parse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int last = range.Length > 1 ? int.Parse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : first;
        for (int cp = first; cp <= last; cp++) map[cp] = value;
    }
    return map;
}

static string Pascal(string name) => string.Concat(name.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
