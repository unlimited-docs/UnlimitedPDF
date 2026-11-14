namespace UnlimitedPDF.Models.Internal.Fonts.OpenType;

/// <summary>
/// Writes the TrueType font program a PDF embeds (D-010): every glyph ID is kept, so text can use the original IDs
/// with an identity CID-to-GID map, but only the used glyphs (plus <c>.notdef</c> and the parts of composite glyphs)
/// keep their outlines; the rest are empty. A font whose embedding permissions forbid subsetting keeps every glyph
/// (PR-022). Outlines come from the font's instance, so a variable font embeds its chosen weight (D-013).
/// </summary>
internal static class TrueTypeSubsetter
{
    #region Fields and Constants

    private const uint ChecksumMagic = 0xB1B0AFBA;

    // Hinting tables are copied when present; viewers that hint use them with the glyphs' own instructions.
    private static readonly string[] CopiedTables = { "cvt ", "fpgm", "prep", "OS/2" };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the glyphs whose outlines the embedded program keeps: the used glyphs, <c>.notdef</c>, and every part
    /// of a used composite glyph; every glyph when the font forbids subsetting.
    /// </summary>
    public static SortedSet<int> KeptGlyphs(PdfFont font, IEnumerable<int> used)
    {
        var source = font.Font;
        if (font.EmbedWholeFont) return new SortedSet<int>(Enumerable.Range(0, source.GlyphCount));
        var kept = new SortedSet<int>();
        var pending = new Stack<int>(used.Where(g => g >= 0 && g < source.GlyphCount).Append(0));
        while (pending.Count > 0)
        {
            int glyph = pending.Pop();
            if (!kept.Add(glyph)) continue;
            foreach (int part in source.Components(glyph)) pending.Push(part);
        }
        return kept;
    }

    /// <summary>
    /// Returns the font program bytes keeping the outlines of <paramref name="kept"/>.
    /// </summary>
    public static byte[] Build(PdfFont font, IReadOnlySet<int> kept)
    {
        var source = font.Font;
        var instance = font.Instance;
        int glyphCount = source.GlyphCount;

        var glyf = new List<byte>();
        var loca = new List<byte>();
        var hmtx = new List<byte>();
        int advanceMax = 0;
        for (int g = 0; g < glyphCount; g++)
        {
            AppendU32(loca, (uint)glyf.Count);
            var data = kept.Contains(g) ? instance.GlyphData(g) : Array.Empty<byte>();
            glyf.AddRange(data);
            while (glyf.Count % 4 != 0) glyf.Add(0);

            int advance = instance.Advance(g);
            advanceMax = Math.Max(advanceMax, advance);
            int leftSideBearing = data.Length >= 10 ? (short)((data[2] << 8) | data[3]) : 0;
            AppendU16(hmtx, (ushort)advance);
            AppendU16(hmtx, unchecked((ushort)(short)leftSideBearing));
        }
        AppendU32(loca, (uint)glyf.Count);

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cmap"] = EmptyCmap(),
            ["glyf"] = glyf.ToArray(),
            ["head"] = Head(source),
            ["hhea"] = Hhea(source, instance, glyphCount, advanceMax),
            ["hmtx"] = hmtx.ToArray(),
            ["loca"] = loca.ToArray(),
            ["maxp"] = Copy(source, "maxp"),
            ["post"] = Post(source)
        };
        foreach (var tag in CopiedTables.Where(source.HasTable)) tables[tag] = Copy(source, tag);
        return Assemble(tables);
    }

    #endregion

    #region Private Methods

    private static void AppendU16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    private static void AppendU32(List<byte> bytes, uint value)
    {
        AppendU16(bytes, (ushort)(value >> 16));
        AppendU16(bytes, (ushort)value);
    }

    private static void SetU16(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }

    private static byte[] Copy(OpenTypeFont font, string tag)
    {
        var (offset, length) = font.TableRange(tag);
        return font.Data.Slice(offset, length);
    }

    // A cmap with an empty format 4 subtable: CID fonts map codes to glyphs through the PDF, but some viewers expect
    // the table to exist.
    private static byte[] EmptyCmap() => new byte[]
    {
        0, 0, 0, 1, // version 0, one encoding record
        0, 3, 0, 1, 0, 0, 0, 12, // Windows Unicode BMP, subtable at 12
        0, 4, 0, 24, 0, 0, 0, 2, 0, 2, 0, 0, 0, 0, // format 4, length 24, one segment
        0xFF, 0xFF, 0, 0, 0xFF, 0xFF, 0, 1, 0, 0 // end 0xFFFF, pad, start 0xFFFF, delta 1, range offset 0
    };

    // head with long glyph offsets and the checksum adjustment cleared (Assemble sets it).
    private static byte[] Head(OpenTypeFont font)
    {
        var head = Copy(font, "head");
        if (head.Length < 54) throw FontBytes.Corrupt("has a head table that is too short");
        head.AsSpan(8, 4).Clear();
        SetU16(head, 50, 1);
        return head;
    }

    // hhea with one metric per glyph, the instance's ascender and descender, and the new widest advance.
    private static byte[] Hhea(OpenTypeFont font, FontInstance instance, int glyphCount, int advanceMax)
    {
        var hhea = Copy(font, "hhea");
        SetU16(hhea, 4, instance.Ascender);
        SetU16(hhea, 6, instance.Descender);
        SetU16(hhea, 10, advanceMax);
        SetU16(hhea, 34, glyphCount);
        return hhea;
    }

    // post version 3 (no glyph names), keeping the italic angle, underline, and fixed-pitch values.
    private static byte[] Post(OpenTypeFont font)
    {
        var post = font.HasTable("post") && font.TableRange("post").Length >= 32 ? Copy(font, "post").AsSpan(0, 32).ToArray() : new byte[32];
        post[0] = 0; post[1] = 3; post[2] = 0; post[3] = 0;
        return post;
    }

    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (int j = 0; j < 4; j++) word = (word << 8) | (i + j < data.Length ? data[i + j] : 0u);
            sum += word;
        }
        return sum;
    }

    private static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        int count = tables.Count;
        int entrySelector = 0;
        while ((2 << entrySelector) <= count) entrySelector++;
        int searchRange = 16 << entrySelector;
        var header = new List<byte>();
        AppendU32(header, 0x00010000);
        AppendU16(header, (ushort)count);
        AppendU16(header, (ushort)searchRange);
        AppendU16(header, (ushort)entrySelector);
        AppendU16(header, (ushort)(count * 16 - searchRange));

        int offset = 12 + 16 * count;
        int headOffset = 0;
        foreach (var (tag, data) in tables)
        {
            header.AddRange(Encoding.ASCII.GetBytes(tag));
            AppendU32(header, Checksum(data));
            AppendU32(header, (uint)offset);
            AppendU32(header, (uint)data.Length);
            if (tag == "head") headOffset = offset;
            offset += (data.Length + 3) & ~3;
        }

        var font = new byte[offset];
        header.CopyTo(font);
        int p = 12 + 16 * count;
        foreach (var data in tables.Values)
        {
            data.CopyTo(font, p);
            p += (data.Length + 3) & ~3;
        }
        uint adjustment = unchecked(ChecksumMagic - Checksum(font));
        font[headOffset + 8] = (byte)(adjustment >> 24);
        font[headOffset + 9] = (byte)(adjustment >> 16);
        font[headOffset + 10] = (byte)(adjustment >> 8);
        font[headOffset + 11] = (byte)adjustment;
        return font;
    }

    #endregion

    #endregion
}
