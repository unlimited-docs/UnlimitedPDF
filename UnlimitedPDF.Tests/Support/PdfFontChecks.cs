using System.Buffers.Binary;
using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Structural checks of embedded composite fonts (A05, A08, V-08), run by <see cref="PdfStructureChecker"/> on
/// every Type0 font in a file.
/// </summary>
internal static class PdfFontChecks
{
    #region Fields and Constants

    // A font name the library writes: an optional six-letter subset tag, then letters, digits, and hyphens (A05).
    private static readonly Regex SafeFontName = new(@"^([A-Z]{6}\+)?[A-Za-z0-9-]{1,63}$", RegexOptions.CultureInvariant);

    private static readonly string[] RequiredTrueTypeTables = { "glyf", "head", "hhea", "hmtx", "loca", "maxp" };
    private static readonly string[] RequiredDescriptorKeys = { "Flags", "FontBBox", "ItalicAngle", "Ascent", "Descent", "CapHeight", "StemV" };

    #endregion

    #region Methods

    #region Public Methods

    public static void Check(byte[] pdf, List<string> errors)
    {
        var inspector = PdfInspector.Read(pdf);
        foreach (int number in inspector.ObjectNumbers)
        {
            var font = inspector.Dictionary(number);
            if (Name(font, "Type") != "Font" || Name(font, "Subtype") != "Type0") continue;
            CheckFont(inspector, number, font, errors);
        }
    }

    #endregion

    #region Private Methods

    private static string? Name(Dictionary<string, object?> d, string key) => d.GetValueOrDefault(key) is PdfName n ? n.Value : null;

    private static void CheckFont(PdfInspector inspector, int number, Dictionary<string, object?> font, List<string> errors)
    {
        string where = $"Type0 font object {number}";
        if (Name(font, "Encoding") != "Identity-H") errors.Add($"{where} does not use /Encoding /Identity-H.");
        string baseFont = Name(font, "BaseFont") ?? string.Empty;
        if (!SafeFontName.IsMatch(baseFont)) errors.Add($"{where} has an unsafe or malformed /BaseFont '{baseFont}'.");

        if (font.GetValueOrDefault("ToUnicode") is not PdfRef toUnicode || !inspector.Exists(toUnicode.Number))
        {
            errors.Add($"{where} has no /ToUnicode map.");
        }
        else
        {
            try
            {
                if (PdfInspector.ParseToUnicode(inspector.StreamData(toUnicode.Number)).Count == 0) errors.Add($"{where} has an empty /ToUnicode map.");
            }
            catch (Exception e) when (e is FormatException or InvalidOperationException or InvalidCastException)
            {
                errors.Add($"{where} has a damaged /ToUnicode map: {e.Message}");
            }
        }

        if (font.GetValueOrDefault("DescendantFonts") is not List<object?> { Count: 1 } descendants || descendants[0] is not PdfRef cidRef || !inspector.Exists(cidRef.Number))
        {
            errors.Add($"{where} does not have exactly one descendant CIDFont.");
            return;
        }

        var cid = inspector.Dictionary(cidRef.Number);
        string? subtype = Name(cid, "Subtype");
        if (Name(cid, "Type") != "Font" || subtype is not ("CIDFontType2" or "CIDFontType0")) errors.Add($"CIDFont object {cidRef.Number} is not a CIDFontType2 or CIDFontType0 font.");
        if (cid.GetValueOrDefault("CIDSystemInfo") is not Dictionary<string, object?> info || info.GetValueOrDefault("Registry") is not byte[] || info.GetValueOrDefault("Ordering") is not byte[] || info.GetValueOrDefault("Supplement") is not double)
            errors.Add($"CIDFont object {cidRef.Number} has no valid /CIDSystemInfo.");
        if (subtype == "CIDFontType2" && Name(cid, "CIDToGIDMap") != "Identity" && cid.GetValueOrDefault("CIDToGIDMap") is not PdfRef)
            errors.Add($"CIDFont object {cidRef.Number} has no /CIDToGIDMap.");
        if (Name(cid, "BaseFont") is not { } cidBase || !baseFont.StartsWith(cidBase, StringComparison.Ordinal))
            errors.Add($"CIDFont object {cidRef.Number} has a /BaseFont that does not match its Type0 font.");

        if (cid.GetValueOrDefault("FontDescriptor") is not PdfRef descriptorRef || !inspector.Exists(descriptorRef.Number))
        {
            errors.Add($"CIDFont object {cidRef.Number} has no /FontDescriptor.");
            return;
        }
        var descriptor = inspector.Dictionary(descriptorRef.Number);
        if (Name(descriptor, "Type") != "FontDescriptor") errors.Add($"Font descriptor object {descriptorRef.Number} is not a /FontDescriptor.");
        if (Name(descriptor, "FontName") != Name(cid, "BaseFont")) errors.Add($"Font descriptor object {descriptorRef.Number} has a /FontName that differs from its CIDFont.");
        foreach (var key in RequiredDescriptorKeys.Where(k => !descriptor.ContainsKey(k))) errors.Add($"Font descriptor object {descriptorRef.Number} has no /{key}.");

        string expectedProgram = subtype == "CIDFontType2" ? "FontFile2" : "FontFile3";
        if (descriptor.GetValueOrDefault(expectedProgram) is not PdfRef programRef || !inspector.Exists(programRef.Number))
        {
            errors.Add($"Font descriptor object {descriptorRef.Number} has no /{expectedProgram} font program.");
            return;
        }

        byte[] program;
        try
        {
            program = inspector.StreamData(programRef.Number);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            errors.Add($"Font program object {programRef.Number} cannot be read: {e.Message}");
            return;
        }

        if (subtype == "CIDFontType2") CheckTrueType(programRef.Number, program, inspector, cid, errors);
        else CheckCff(programRef.Number, program, inspector.Dictionary(programRef.Number), errors);
    }

    // A TrueType program: the required tables present, every table checksum and the whole-font checksum right, and
    // each /W width equal to the program's advance in 1/1000 em.
    private static void CheckTrueType(int number, byte[] program, PdfInspector inspector, Dictionary<string, object?> cid, List<string> errors)
    {
        string where = $"Font program object {number}";
        int before = errors.Count;
        Dictionary<string, (int Offset, int Length)> tables;
        try
        {
            tables = EmbeddedFont.SfntTables(program);
            foreach (var (tag, (offset, length)) in tables)
            {
                if (offset + length > program.Length) { errors.Add($"{where} has a {tag} table outside the program."); return; }
                var data = program.AsSpan(offset, length).ToArray();
                if (tag == "head" && data.Length >= 12) data.AsSpan(8, 4).Clear();
                uint stored = BinaryPrimitives.ReadUInt32BigEndian(program.AsSpan(12 + 16 * tables.Keys.ToList().IndexOf(tag) + 4));
                if (TestFontBuilder.Checksum(data) != stored) errors.Add($"{where} has a wrong checksum for its {tag} table.");
            }
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            errors.Add($"{where} has a damaged table directory.");
            return;
        }

        foreach (var tag in RequiredTrueTypeTables.Where(t => !tables.ContainsKey(t))) errors.Add($"{where} has no {tag} table.");
        if (errors.Count > before) return;
        if (TestFontBuilder.Checksum(program) != 0xB1B0AFBA) errors.Add($"{where} has a wrong whole-font checksum (head.checkSumAdjustment).");

        int unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(program.AsSpan(tables["head"].Offset + 18));
        int metrics = BinaryPrimitives.ReadUInt16BigEndian(program.AsSpan(tables["hhea"].Offset + 34));
        int glyphs = EmbeddedFont.TrueTypeGlyphCount(program);
        var offsets = EmbeddedFont.TrueTypeGlyphOffsets(program);
        if (offsets.Zip(offsets.Skip(1)).Any(p => p.Second < p.First) || offsets[^1] > tables["glyf"].Length) errors.Add($"{where} has invalid glyph offsets.");

        var widths = Widths(inspector, cid);
        foreach (var (glyph, width) in widths)
        {
            if (glyph >= glyphs) { errors.Add($"{where}: /W names glyph {glyph}, past the program's {glyphs} glyphs."); continue; }
            int advance = BinaryPrimitives.ReadUInt16BigEndian(program.AsSpan(tables["hmtx"].Offset + 4 * Math.Min(glyph, metrics - 1)));
            double expected = Math.Round(advance * 1000.0 / unitsPerEm);
            if (Math.Abs(expected - width) > 1) errors.Add($"{where}: /W width {width} of glyph {glyph} differs from the program's advance ({expected}).");
        }
    }

    // A CFF program: CID-keyed CFF version 1 whose every charstring the library's own CFF reader accepts.
    private static void CheckCff(int number, byte[] program, Dictionary<string, object?> stream, List<string> errors)
    {
        string where = $"Font program object {number}";
        if (Name(stream, "Subtype") != "CIDFontType0C") errors.Add($"{where} is not marked /Subtype /CIDFontType0C.");
        try
        {
            int glyphs = EmbeddedFont.CffCharstrings(program).Count;
            var cff = CffFont.Read(new FontBytes(program), 0, program.Length, glyphs);
            if (!cff.IsCidKeyed) errors.Add($"{where} is not a CID-keyed CFF font.");
        }
        catch (Exception e) when (e is PdfFontException or FormatException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            errors.Add($"{where} is not a valid CFF program: {e.Message}");
        }
    }

    private static Dictionary<int, double> Widths(PdfInspector inspector, Dictionary<string, object?> cid)
    {
        var widths = new Dictionary<int, double>();
        var w = cid.GetValueOrDefault("W") is PdfRef r ? PdfSyntax.ParseObject(Encoding.Latin1.GetString(inspector.StreamData(r.Number))) as List<object?> : cid.GetValueOrDefault("W") as List<object?>;
        if (w is null) return widths;
        for (int i = 0; i + 1 < w.Count;)
        {
            int first = (int)(double)w[i]!;
            if (w[i + 1] is List<object?> list) { for (int j = 0; j < list.Count; j++) widths[first + j] = (double)list[j]!; i += 2; }
            else { int last = (int)(double)w[i + 1]!; for (int c = first; c <= last; c++) widths[c] = (double)w[i + 2]!; i += 3; }
        }
        return widths;
    }

    #endregion

    #endregion
}
