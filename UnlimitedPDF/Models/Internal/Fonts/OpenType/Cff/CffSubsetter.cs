namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// Writes the CFF font program a PDF embeds for a font with CFF or CFF2 outlines (D-010, D-013): a CID-keyed CFF
/// version 1 font (registry Adobe, ordering Identity) whose CID equals the source glyph ID, with one Font DICT, no
/// subroutines, and the flattened charstrings of the font's instance for the kept glyphs; every other glyph is a bare
/// <c>endchar</c>. Name-keyed CFF fonts and CFF2 variable fonts both come out in this one form.
/// </summary>
internal static class CffSubsetter
{
    #region Fields and Constants

    private const byte EndChar = 14;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the CFF program keeping the outlines of <paramref name="kept"/>.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="kept">The glyphs whose outlines to keep.</param>
    /// <param name="name">The font name (already sanitized) for the Name INDEX.</param>
    public static byte[] Build(PdfFont font, IReadOnlySet<int> kept, string name)
    {
        int glyphCount = font.Font.GlyphCount;
        int unitsPerEm = font.Font.UnitsPerEm;
        var charStrings = new List<byte[]>(glyphCount);
        for (int g = 0; g < glyphCount; g++) charStrings.Add(kept.Contains(g) ? font.Instance.Charstring(g) : new[] { EndChar });

        var header = new byte[] { 1, 0, 4, 4 };
        var nameIndex = Index(new[] { Encoding.ASCII.GetBytes(name) });
        var stringIndex = Index(new[] { Encoding.ASCII.GetBytes("Adobe"), Encoding.ASCII.GetBytes("Identity") });
        var globalSubrs = Index(Array.Empty<byte[]>());
        var charset = Charset(glyphCount);
        var fdSelect = new List<byte> { 3 };
        AppendU16(fdSelect, 1);
        AppendU16(fdSelect, 0);
        fdSelect.Add(0);
        AppendU16(fdSelect, glyphCount);
        var charStringIndex = Index(charStrings);

        // A font whose em is not 1000 units states its scale; the Font DICT then uses the identity matrix.
        bool customMatrix = unitsPerEm != 1000;
        var topMatrix = customMatrix ? FontMatrix(1.0 / unitsPerEm) : Array.Empty<byte>();
        var fontDictMatrix = customMatrix ? new byte[] { 140, 139, 139, 140, 139, 139, 12, 7 } : Array.Empty<byte>(); // [1 0 0 1 0 0] FontMatrix

        // Fixed-size DICT entries (five-byte integers), so the layout is known before the offsets are filled in.
        int topSize = 7 + 7 + topMatrix.Length + 6 + 7 + 7 + 6;
        int topIndexSize = 2 + 1 + 8 + topSize;
        int charsetOffset = header.Length + nameIndex.Length + topIndexSize + stringIndex.Length + globalSubrs.Length;
        int fdSelectOffset = charsetOffset + charset.Length;
        int fontDictSize = 11 + fontDictMatrix.Length;
        int fdArrayOffset = fdSelectOffset + fdSelect.Count;
        int charStringsOffset = fdArrayOffset + 2 + 1 + 8 + fontDictSize;
        int privateOffset = charStringsOffset + charStringIndex.Length; // an empty Private DICT at the end

        var top = new List<byte> { 248, 27, 248, 28, 139, 12, 30 }; // ROS: "Adobe" (SID 391), "Identity" (392), 0
        top.AddRange(Int(glyphCount)); top.AddRange(new byte[] { 12, 34 }); // CIDCount
        top.AddRange(topMatrix);
        top.AddRange(Int(charsetOffset)); top.Add(15); // charset
        top.AddRange(Int(fdArrayOffset)); top.AddRange(new byte[] { 12, 36 }); // FDArray
        top.AddRange(Int(fdSelectOffset)); top.AddRange(new byte[] { 12, 37 }); // FDSelect
        top.AddRange(Int(charStringsOffset)); top.Add(17); // CharStrings
        if (top.Count != topSize) throw new InvalidOperationException("CFF Top DICT layout mismatch.");

        var fontDict = new List<byte>(fontDictMatrix);
        fontDict.AddRange(Int(0)); fontDict.AddRange(Int(privateOffset)); fontDict.Add(18); // Private: size 0

        var output = new List<byte>();
        output.AddRange(header);
        output.AddRange(nameIndex);
        output.AddRange(Index(new[] { top.ToArray() }));
        output.AddRange(stringIndex);
        output.AddRange(globalSubrs);
        output.AddRange(charset);
        output.AddRange(fdSelect);
        output.AddRange(Index(new[] { fontDict.ToArray() }));
        output.AddRange(charStringIndex);
        if (output.Count != privateOffset) throw new InvalidOperationException("CFF layout mismatch.");
        return output.ToArray();
    }

    #endregion

    #region Private Methods

    private static void AppendU16(List<byte> bytes, int value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    // Charset format 2: one range mapping glyphs 1 to n - 1 to CIDs 1 to n - 1 (glyph 0 is CID 0).
    private static byte[] Charset(int glyphCount)
    {
        if (glyphCount <= 1) return new byte[] { 0 };
        var bytes = new List<byte> { 2 };
        AppendU16(bytes, 1);
        AppendU16(bytes, glyphCount - 2);
        return bytes.ToArray();
    }

    // FontMatrix [scale 0 0 scale 0 0] with the scale as a real number.
    private static byte[] FontMatrix(double scale)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Real(scale));
        bytes.AddRange(new byte[] { 139, 139 });
        bytes.AddRange(Real(scale));
        bytes.AddRange(new byte[] { 139, 139, 12, 7 });
        return bytes.ToArray();
    }

    private static byte[] Index(IReadOnlyList<byte[]> entries)
    {
        var bytes = new List<byte>();
        AppendU16(bytes, entries.Count);
        if (entries.Count == 0) return bytes.ToArray();
        bytes.Add(4);
        uint offset = 1;
        void U32(uint v) { bytes.Add((byte)(v >> 24)); bytes.Add((byte)(v >> 16)); bytes.Add((byte)(v >> 8)); bytes.Add((byte)v); }
        U32(offset);
        foreach (var entry in entries) U32(offset += (uint)entry.Length);
        foreach (var entry in entries) bytes.AddRange(entry);
        return bytes.ToArray();
    }

    private static byte[] Int(int value) => new[] { (byte)29, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    // A DICT real number: nibbles for digits, '.', and the 0xF terminator.
    private static byte[] Real(double value)
    {
        string text = value.ToString("0.###############", CultureInfo.InvariantCulture);
        var nibbles = new List<int>();
        foreach (char c in text) nibbles.Add(c == '.' ? 0xA : c == '-' ? 0xE : c - '0');
        nibbles.Add(0xF);
        if (nibbles.Count % 2 == 1) nibbles.Add(0xF);
        var bytes = new List<byte> { 30 };
        for (int i = 0; i < nibbles.Count; i += 2) bytes.Add((byte)((nibbles[i] << 4) | nibbles[i + 1]));
        return bytes.ToArray();
    }

    #endregion

    #endregion
}
