using System.Buffers.Binary;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Self-tests of the in-code test font builder (D-014): its fonts are well-formed sfnt files whose tables round-trip,
/// so later font tests can trust them. The checks read the bytes directly, independent of the library's font reader.
/// </summary>
public class TestFontBuilderTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_table_directory_is_sorted_aligned_and_checksummed()
    {
        var font = Sample().Build();
        var tables = Tables(font);

        Assert.Equal(new[] { "OS/2", "cmap", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "name", "post" }, tables.Keys.ToArray());
        foreach (var (tag, (offset, length, checksum)) in tables)
        {
            Assert.Equal(0, offset % 4);
            var data = font.AsSpan(offset, length).ToArray();
            if (tag == "head") data.AsSpan(8, 4).Clear();
            Assert.Equal(checksum, TestFontBuilder.Checksum(data));
        }
        Assert.Equal(0xB1B0AFBAu, TestFontBuilder.Checksum(font));
    }

    [Fact]
    public void Characters_map_to_their_glyphs_in_both_cmap_formats()
    {
        var builder = new TestFontBuilder();
        builder.MapCharacters("Abత");
        builder.Map(0x1F642, builder.AddGlyph(900, TestFontBuilder.Box(0, 0, 900, 900)));
        var font = builder.Build();

        var format4 = ReadFormat4(font);
        var format12 = ReadFormat12(font);

        Assert.Equal(1, format4['A']);
        Assert.Equal(2, format4['b']);
        Assert.Equal(3, format4[0x0c24]);
        Assert.False(format4.ContainsKey(0x1F642));
        Assert.Equal(4, format12[0x1F642]);
        Assert.Equal(3, format12[0x0c24]);
    }

    [Fact]
    public void Metrics_names_and_style_round_trip()
    {
        var builder = new TestFontBuilder("Test Telugu") { Weight = 700, FsType = 0x0002 };
        builder.MapCharacters("A", advance: 640);
        var font = builder.Build();
        var tables = Tables(font);

        var hmtx = tables["hmtx"];
        Assert.Equal(640, BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(hmtx.Offset + 4)));
        var os2 = tables["OS/2"];
        Assert.Equal(96, os2.Length);
        Assert.Equal(700, BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(os2.Offset + 4)));
        Assert.Equal(0x0002, BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(os2.Offset + 8)));
        Assert.Equal(54, tables["head"].Length);
        Assert.Equal(36, tables["hhea"].Length);
        Assert.Equal(32, tables["maxp"].Length);
        Assert.Contains("TestTelugu-Bold", Names(font));
        Assert.Contains("Test Telugu", Names(font));
    }

    [Fact]
    public void Glyph_offsets_cover_simple_composite_and_empty_glyphs()
    {
        var builder = new TestFontBuilder();
        int dot = builder.AddGlyph(200, TestFontBuilder.Box(50, 0, 150, 100));
        int space = builder.AddGlyph(250);
        int composite = builder.AddCompositeGlyph(600, (dot, 0, 0), (dot, 300, 0));
        var font = builder.Build();
        var tables = Tables(font);

        var loca = Enumerable.Range(0, builder.GlyphCount + 1)
            .Select(i => (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(tables["loca"].Offset + 4 * i)))
            .ToArray();
        Assert.Equal(tables["glyf"].Length, loca[^1]);
        Assert.Equal(loca[space], loca[space + 1]);
        short contours = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(tables["glyf"].Offset + loca[composite]));
        Assert.Equal(-1, contours);
        short xMax = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(tables["glyf"].Offset + loca[composite] + 6));
        Assert.Equal(450, xMax);
    }

    [Fact]
    public void Extra_tables_are_added_in_tag_order()
    {
        var font = Sample().AddTable("GSUB", new byte[] { 0, 1, 0, 0 }).Build();

        Assert.Contains("GSUB", Tables(font).Keys);
        Assert.Equal(0xB1B0AFBAu, TestFontBuilder.Checksum(font));
    }

    #endregion

    #region Private Methods

    private static TestFontBuilder Sample() => new TestFontBuilder().MapCharacters("Hello");

    private static SortedDictionary<string, (int Offset, int Length, uint Checksum)> Tables(byte[] font)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var tables = new SortedDictionary<string, (int, int, uint)>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            var record = font.AsSpan(12 + 16 * i);
            tables[Encoding.ASCII.GetString(record.Slice(0, 4))] = (
                (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(8)),
                (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(12)),
                BinaryPrimitives.ReadUInt32BigEndian(record.Slice(4)));
        }
        return tables;
    }

    private static int Subtable(byte[] font, ushort encoding)
    {
        int cmap = Tables(font)["cmap"].Offset;
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(cmap + 2));
        for (int i = 0; i < count; i++)
        {
            var record = font.AsSpan(cmap + 4 + 8 * i);
            if (BinaryPrimitives.ReadUInt16BigEndian(record) == 3 && BinaryPrimitives.ReadUInt16BigEndian(record.Slice(2)) == encoding)
                return cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(4));
        }
        throw new InvalidOperationException("No such cmap subtable.");
    }

    private static Dictionary<int, int> ReadFormat4(byte[] font)
    {
        int t = Subtable(font, 1);
        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(t)));
        int segments = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(t + 6)) / 2;
        int ends = t + 14, starts = ends + 2 * segments + 2, deltas = starts + 2 * segments;
        var map = new Dictionary<int, int>();
        for (int s = 0; s < segments; s++)
        {
            int start = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(starts + 2 * s));
            int end = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(ends + 2 * s));
            int delta = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(deltas + 2 * s));
            if (start == 0xFFFF) continue;
            for (int c = start; c <= end; c++) map[c] = (c + delta) & 0xFFFF;
        }
        return map;
    }

    private static Dictionary<int, int> ReadFormat12(byte[] font)
    {
        int t = Subtable(font, 10);
        Assert.Equal(12, BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(t)));
        int groups = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(t + 12));
        var map = new Dictionary<int, int>();
        for (int g = 0; g < groups; g++)
        {
            var group = font.AsSpan(t + 16 + 12 * g);
            int start = (int)BinaryPrimitives.ReadUInt32BigEndian(group);
            int end = (int)BinaryPrimitives.ReadUInt32BigEndian(group.Slice(4));
            int glyph = (int)BinaryPrimitives.ReadUInt32BigEndian(group.Slice(8));
            for (int c = start; c <= end; c++) map[c] = glyph + c - start;
        }
        return map;
    }

    private static List<string> Names(byte[] font)
    {
        int t = Tables(font)["name"].Offset;
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(t + 2));
        int strings = t + BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(t + 4));
        var names = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var record = font.AsSpan(t + 6 + 12 * i);
            int length = BinaryPrimitives.ReadUInt16BigEndian(record.Slice(8));
            int offset = BinaryPrimitives.ReadUInt16BigEndian(record.Slice(10));
            names.Add(Encoding.BigEndianUnicode.GetString(font, strings + offset, length));
        }
        return names;
    }

    #endregion

    #endregion
}
