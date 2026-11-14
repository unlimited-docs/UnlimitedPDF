using System.Buffers.Binary;
using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The internal OpenType reader (D-008, D-010, A06): it parses TrueType fonts, rejects font collections, web fonts,
/// and non-fonts as unsupported, and turns every damaged or hostile input into <see cref="PdfFontException"/> without
/// hanging or throwing anything else.
/// </summary>
public class OpenTypeFontTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_generated_font_parses_with_its_metrics_names_and_map()
    {
        var builder = new TestFontBuilder("Test Telugu") { Weight = 700, FsType = 0x0004, Italic = true };
        builder.MapCharacters("Aత", advance: 640);
        builder.Map(0x1F642, builder.AddGlyph(900, TestFontBuilder.Box(0, 0, 900, 900)));

        var font = OpenTypeFont.Parse(builder.Build());

        Assert.Equal(FontOutlines.TrueType, font.Outlines);
        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(4, font.GlyphCount);
        Assert.Equal(1, font.CharacterMap.GlyphFor('A'));
        Assert.Equal(2, font.CharacterMap.GlyphFor(0x0c24));
        Assert.Equal(3, font.CharacterMap.GlyphFor(0x1F642));
        Assert.Equal(0, font.CharacterMap.GlyphFor('Z'));
        Assert.Equal(640, font.Advance(1));
        Assert.Equal(900, font.Advance(3));
        Assert.Equal("Test Telugu", font.FamilyName);
        Assert.Equal("Bold Italic", font.SubfamilyName);
        Assert.Equal("TestTelugu-BoldItalic", font.PostScriptName);
        Assert.Equal(700, font.WeightClass);
        Assert.Equal(0x0004, font.FsType);
        Assert.Equal(-12, font.ItalicAngle);
        Assert.Equal(800, font.Ascender);
        Assert.Equal(-200, font.Descender);
        Assert.Equal(700, font.CapHeight);
    }

    [Fact]
    public void Composite_and_empty_glyphs_are_located()
    {
        var builder = new TestFontBuilder();
        int dot = builder.AddGlyph(200, TestFontBuilder.Box(50, 0, 150, 100));
        int space = builder.AddGlyph(250);
        int pair = builder.AddCompositeGlyph(600, (dot, 0, 0), (dot, 300, 0));

        var font = OpenTypeFont.Parse(builder.Build());

        Assert.Equal(new[] { dot, dot }, font.Components(pair));
        Assert.Empty(font.Components(dot));
        Assert.Equal(0, font.GlyphRange(space).Length);
        Assert.True(font.GlyphRange(pair).Length > 0);
    }

    [Theory]
    [InlineData(new byte[] { 0x74, 0x74, 0x63, 0x66, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 12 }, "font collection")]
    [InlineData(new byte[] { 0x77, 0x4F, 0x46, 0x46, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 12 }, "web font")]
    [InlineData(new byte[] { 0x77, 0x4F, 0x46, 0x32, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 12 }, "web font")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0, 0, 0, 0 }, "not a TrueType or OpenType font")]
    [InlineData(new byte[] { 0, 1, 0, 0 }, "too short")]
    [InlineData(new byte[0], "too short")]
    public void Collections_web_fonts_and_other_data_are_unsupported(byte[] data, string message)
    {
        var error = Assert.Throws<PdfFontException>(() => OpenTypeFont.Parse(data));

        Assert.Equal(PdfFontErrorReason.UnsupportedFormat, error.Reason);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Every_truncation_fails_with_a_font_exception()
    {
        var font = Sample();

        for (int length = 0; length < font.Length; length += 3)
        {
            var error = Assert.Throws<PdfFontException>(() => OpenTypeFont.Parse(font.AsSpan(0, length).ToArray()));
            Assert.True(error.Reason is PdfFontErrorReason.CorruptData or PdfFontErrorReason.UnsupportedFormat, $"length {length}: {error.Reason}");
        }
    }

    [Fact]
    public void A_table_outside_the_data_is_corrupt()
    {
        var font = Sample();
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(Record(font, "hmtx") + 8), 0x7FFFFFF0);

        AssertCorrupt(font, "outside the data");
    }

    [Fact]
    public void A_missing_required_table_is_corrupt()
    {
        var font = Sample();
        Encoding.ASCII.GetBytes("xmap").CopyTo(font, Record(font, "cmap"));

        AssertCorrupt(font, "no cmap table");
    }

    [Fact]
    public void Decreasing_glyph_offsets_are_corrupt()
    {
        var font = Sample();
        int loca = Offset(font, "loca");
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(loca + 4), 0x00010000);

        AssertCorrupt(font, "glyph offsets");
    }

    [Fact]
    public void An_invalid_units_per_em_is_corrupt()
    {
        var font = Sample();
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(Offset(font, "head") + 18), 0);

        AssertCorrupt(font, "unitsPerEm");
    }

    [Fact]
    public void A_character_map_range_outside_the_data_is_corrupt_at_load()
    {
        // Point the format 4 subtable's first idRangeOffset far past the end, which a lookup would follow.
        var font = Sample();
        int cmap = Offset(font, "cmap");
        int format4 = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(cmap + 8));
        int segments = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(format4 + 6)) / 2;
        int rangeOffsets = format4 + 14 + 2 * segments + 2 + 4 * segments;
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(rangeOffsets), 0xFFF0);
        // Make format 4 the only Unicode subtable, so the reader must use it.
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(cmap + 2), 1);

        AssertCorrupt(font, "cmap glyph range");
    }

    [Fact]
    public void A_composite_glyph_that_uses_itself_is_corrupt()
    {
        var builder = new TestFontBuilder();
        int self = builder.GlyphCount;
        builder.AddCompositeGlyph(500, (self, 0, 0));

        AssertCorrupt(builder.Build(), "cycle");
    }

    [Fact]
    public void A_composite_cycle_through_two_glyphs_is_corrupt()
    {
        var builder = new TestFontBuilder();
        int first = builder.GlyphCount;
        builder.AddCompositeGlyph(500, (first + 1, 0, 0));
        builder.AddCompositeGlyph(500, (first, 0, 0));

        AssertCorrupt(builder.Build(), "cycle");
    }

    [Fact]
    public void Composite_nesting_is_capped_at_sixteen_levels()
    {
        OpenTypeFont.Parse(Chain(OpenTypeFont.MaxCompositeDepth - 1)); // 15 composites over a simple glyph: 16 levels

        AssertCorrupt(Chain(OpenTypeFont.MaxCompositeDepth), "deeper than 16");
    }

    [Fact]
    public void Random_damage_never_escapes_as_another_exception_or_hangs()
    {
        var original = Sample();
        var random = new Random(45);
        var clock = Stopwatch.StartNew();

        for (int run = 0; run < 3000; run++)
        {
            var font = (byte[])original.Clone();
            int changes = random.Next(1, 9);
            for (int i = 0; i < changes; i++) font[random.Next(font.Length)] = (byte)random.Next(256);
            try
            {
                var parsed = OpenTypeFont.Parse(font);
                for (int c = 0; c < 0x80; c++) parsed.CharacterMap.GlyphFor(c);
                for (int g = 0; g < parsed.GlyphCount; g++) parsed.Advance(g);
            }
            catch (PdfFontException)
            {
            }
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"3000 damaged fonts took {clock.Elapsed}");
    }

    #endregion

    #region Private Methods

    private static void AssertCorrupt(byte[] font, string message)
    {
        var error = Assert.Throws<PdfFontException>(() => OpenTypeFont.Parse(font));
        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains(message, error.Message);
    }

    private static byte[] Chain(int composites)
    {
        var builder = new TestFontBuilder();
        int glyph = builder.AddGlyph(200, TestFontBuilder.Box(0, 0, 100, 100));
        for (int i = 0; i < composites; i++) glyph = builder.AddCompositeGlyph(200, (glyph, 0, 0));
        return builder.Build();
    }

    private static int Offset(byte[] font, string tag) => (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(Record(font, tag) + 8));

    private static int Record(byte[] font, string tag)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            if (Encoding.ASCII.GetString(font, 12 + 16 * i, 4) == tag) return 12 + 16 * i;
        }
        throw new InvalidOperationException($"No {tag} table.");
    }

    private static byte[] Sample()
    {
        var builder = new TestFontBuilder();
        builder.MapCharacters("Hello, world");
        int dot = builder.AddGlyph(200, TestFontBuilder.Box(50, 0, 150, 100));
        builder.AddCompositeGlyph(600, (dot, 0, 0), (dot, 300, 0));
        return builder.Build();
    }

    #endregion

    #endregion
}
