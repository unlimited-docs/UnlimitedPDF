using System.Buffers;
using System.Buffers.Binary;

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
    private const int PostLength = 32;

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
    /// Returns the font program bytes keeping the outlines of <paramref name="kept"/>. Every table is written straight
    /// into one program buffer, in tag order, so no table is built or copied twice.
    /// </summary>
    /// <returns>The program, at the start of a buffer rented from <see cref="ArrayPool{T}.Shared"/>, which the caller
    /// returns once the program is compressed.</returns>
    public static ArraySegment<byte> Build(PdfFont font, IReadOnlySet<int> kept)
    {
        var source = font.Font;
        var instance = font.Instance;
        int glyphCount = source.GlyphCount;

        var keep = new bool[glyphCount];
        int glyfLength = 0;
        foreach (int glyph in kept)
        {
            if ((uint)glyph >= (uint)glyphCount || keep[glyph]) continue;
            keep[glyph] = true;
            glyfLength += Padded(instance.GlyphSpan(glyph).Length);
        }

        int headLength = source.TableRange("head").Length;
        if (headLength < 54) throw FontBytes.Corrupt("has a head table that is too short");
        var tables = new List<(string Tag, int Length)>
        {
            ("cmap", EmptyCmap.Length),
            ("glyf", glyfLength),
            ("head", headLength),
            ("hhea", source.TableRange("hhea").Length),
            ("hmtx", 4 * glyphCount),
            ("loca", 4 * (glyphCount + 1)),
            ("maxp", source.TableRange("maxp").Length),
            ("post", PostLength)
        };
        foreach (var tag in CopiedTables)
            if (source.HasTable(tag)) tables.Add((tag, source.TableRange(tag).Length));
        tables.Sort((a, b) => string.CompareOrdinal(a.Tag, b.Tag));

        int count = tables.Count;
        var offsets = new int[count];
        int end = 12 + 16 * count;
        for (int i = 0; i < count; i++)
        {
            offsets[i] = end;
            end += Padded(tables[i].Length);
        }
        var program = ArrayPool<byte>.Shared.Rent(end);
        program.AsSpan(0, end).Clear(); // padding stays zero, as the checksums and the file require
        int Offset(string tag) => offsets[tables.FindIndex(t => t.Tag == tag)];

        // glyf, loca, and hmtx: every glyph ID keeps its slot; glyphs that are not kept are empty.
        int glyf = Offset("glyf"), loca = Offset("loca"), hmtx = Offset("hmtx");
        int position = 0, advanceMax = 0;
        for (int g = 0; g < glyphCount; g++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(program.AsSpan(loca + 4 * g), (uint)position);
            var data = keep[g] ? instance.GlyphSpan(g) : ReadOnlySpan<byte>.Empty;
            data.CopyTo(program.AsSpan(glyf + position));
            position += Padded(data.Length);

            int advance = instance.Advance(g);
            advanceMax = Math.Max(advanceMax, advance);
            int leftSideBearing = data.Length >= 10 ? (short)((data[2] << 8) | data[3]) : 0;
            SetU16(program, hmtx + 4 * g, (ushort)advance);
            SetU16(program, hmtx + 4 * g + 2, unchecked((ushort)(short)leftSideBearing));
        }
        BinaryPrimitives.WriteUInt32BigEndian(program.AsSpan(loca + 4 * glyphCount), (uint)position);

        EmptyCmap.CopyTo(program.AsSpan(Offset("cmap")));
        int head = Offset("head");
        Head(source, program, head);
        Hhea(source, instance, program, Offset("hhea"), glyphCount, advanceMax);
        Copy(source, "maxp", program, Offset("maxp"));
        Post(source, program, Offset("post"));
        foreach (var tag in CopiedTables)
            if (source.HasTable(tag)) Copy(source, tag, program, Offset(tag));

        // The table directory, then the whole-font checksum adjustment in head.
        int entrySelector = 0;
        while ((2 << entrySelector) <= count) entrySelector++;
        int searchRange = 16 << entrySelector;
        BinaryPrimitives.WriteUInt32BigEndian(program, 0x00010000);
        SetU16(program, 4, count);
        SetU16(program, 6, searchRange);
        SetU16(program, 8, entrySelector);
        SetU16(program, 10, count * 16 - searchRange);
        for (int i = 0; i < count; i++)
        {
            var (tag, length) = tables[i];
            int entry = 12 + 16 * i;
            for (int c = 0; c < 4; c++) program[entry + c] = (byte)tag[c];
            BinaryPrimitives.WriteUInt32BigEndian(program.AsSpan(entry + 4), Checksum(program.AsSpan(offsets[i], Padded(length))));
            BinaryPrimitives.WriteUInt32BigEndian(program.AsSpan(entry + 8), (uint)offsets[i]);
            BinaryPrimitives.WriteUInt32BigEndian(program.AsSpan(entry + 12), (uint)length);
        }
        BinaryPrimitives.WriteUInt32BigEndian(program.AsSpan(head + 8), unchecked(ChecksumMagic - Checksum(program.AsSpan(0, end))));
        return new ArraySegment<byte>(program, 0, end);
    }

    #endregion

    #region Private Methods

    // The sum of the big-endian 32-bit words of data whose length is a multiple of 4 (tables are zero-padded).
    private static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i)));
        return sum;
    }

    private static void Copy(OpenTypeFont font, string tag, byte[] program, int offset)
    {
        var (start, length) = font.TableRange(tag);
        font.Data.Span(start, length).CopyTo(program.AsSpan(offset));
    }

    // head with long glyph offsets and the checksum adjustment cleared (Build sets it).
    private static void Head(OpenTypeFont font, byte[] program, int offset)
    {
        Copy(font, "head", program, offset);
        program.AsSpan(offset + 8, 4).Clear();
        SetU16(program, offset + 50, 1);
    }

    // hhea with one metric per glyph, the instance's ascender and descender, and the new widest advance.
    private static void Hhea(OpenTypeFont font, FontInstance instance, byte[] program, int offset, int glyphCount, int advanceMax)
    {
        Copy(font, "hhea", program, offset);
        SetU16(program, offset + 4, instance.Ascender);
        SetU16(program, offset + 6, instance.Descender);
        SetU16(program, offset + 10, advanceMax);
        SetU16(program, offset + 34, glyphCount);
    }

    private static int Padded(int length) => (length + 3) & ~3;

    // post version 3 (no glyph names), keeping the italic angle, underline, and fixed-pitch values: only the first 32
    // bytes of the font's post table are read.
    private static void Post(OpenTypeFont font, byte[] program, int offset)
    {
        if (font.HasTable("post") && font.TableRange("post") is { Length: >= PostLength } post)
            font.Data.Span(post.Offset, PostLength).CopyTo(program.AsSpan(offset));
        program[offset] = 0; program[offset + 1] = 3; program[offset + 2] = 0; program[offset + 3] = 0;
    }

    private static void SetU16(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    // A cmap with an empty format 4 subtable: CID fonts map codes to glyphs through the PDF, but some viewers expect
    // the table to exist.
    private static ReadOnlySpan<byte> EmptyCmap => new byte[]
    {
        0, 0, 0, 1, // version 0, one encoding record
        0, 3, 0, 1, 0, 0, 0, 12, // Windows Unicode BMP, subtable at 12
        0, 4, 0, 24, 0, 0, 0, 2, 0, 2, 0, 0, 0, 0, // format 4, length 24, one segment
        0xFF, 0xFF, 0, 0, 0xFF, 0xFF, 0, 1, 0, 0 // end 0xFFFF, pad, start 0xFFFF, delta 1, range offset 0
    };

    #endregion
}
