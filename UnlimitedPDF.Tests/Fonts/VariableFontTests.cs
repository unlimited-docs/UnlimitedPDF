using System.Buffers.Binary;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Variable fonts (AC-01, AC-10, D-013): a font with a weight axis loads at any weight it holds, clamped to the axis;
/// outlines and advances change with the weight (<c>gvar</c> with interpolated untouched points and phantom points,
/// <c>HVAR</c>, <c>avar</c>, <c>MVAR</c>, and CFF2 <c>blend</c>); damaged variation data fails at load.
/// </summary>
[Collection(nameof(FontFuzzCollection))]
public class VariableFontTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_variable_font_reports_its_weight_range_and_default()
    {
        var font = PdfFont.FromBytes(Box().Build());

        Assert.True(font.IsVariable);
        Assert.Equal(100, font.MinimumWeight);
        Assert.Equal(900, font.MaximumWeight);
        Assert.Equal(400, font.Weight);
    }

    [Theory]
    [InlineData(400, 600, 560)]
    [InlineData(650, 650, 610)]
    [InlineData(700, 660, 620)]
    [InlineData(900, 700, 660)]
    [InlineData(1000, 700, 660)] // clamped to the axis maximum
    public void Outlines_and_advances_follow_the_weight(int weight, int advance, int right)
    {
        var bytes = Box().Build();

        var font = PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = weight });

        Assert.Equal(Math.Min(weight, 900), font.Weight);
        int glyph = font.Font.CharacterMap.GlyphFor('A');
        Assert.Equal(advance, font.Instance.Advance(glyph));
        var points = Points(font.Instance.GlyphData(glyph));
        Assert.Equal(new[] { (40, 0), (40, 700), (right, 700), (right, 0) }, points);
    }

    [Fact]
    public void Weights_below_the_default_move_toward_the_minimum()
    {
        // Deltas are defined at the maximum only, so weights below the default leave the outline unchanged.
        var font = PdfFont.FromBytes(Box().Build(), new PdfFontLoadOptions { Weight = 100 });

        Assert.Equal(600, font.Instance.Advance(font.Font.CharacterMap.GlyphFor('A')));
        Assert.Equal(100, font.Weight);
    }

    [Fact]
    public void Untouched_points_are_interpolated()
    {
        var builder = Axis(new TestFontBuilder());
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        // Touch the first point (no move) and the third (+100, +50); the second and fourth are interpolated.
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (100, 50) }, new[] { 0, 2 });

        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { Weight = 900 });

        // Point 1 (40, 700): x at the low end keeps 0; y at the high end takes 50. Point 3 (560, 0): x takes 100, y 0.
        Assert.Equal(new[] { (40, 0), (40, 750), (660, 750), (660, 0) }, Points(font.Instance.GlyphData(glyph)));
        Assert.Equal(600, font.Instance.Advance(glyph)); // phantom points untouched
    }

    [Fact]
    public void Hvar_advances_win_over_phantom_points()
    {
        var builder = Box();
        builder.HvarAdvanceDeltas[1] = 200;

        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { Weight = 900 });

        Assert.Equal(800, font.Instance.Advance(1));
    }

    [Fact]
    public void An_avar_map_reshapes_the_axis()
    {
        var builder = Box();
        builder.AvarMap = new[] { (-1.0, -1.0), (0.0, 0.0), (0.5, 0.8), (1.0, 1.0) };

        // 650 normalizes to 0.5, which avar maps to 0.8: 80 % of the +100 delta.
        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { Weight = 650 });

        Assert.Equal(680, font.Instance.Advance(1));
    }

    [Fact]
    public void Composite_component_offsets_move()
    {
        var builder = Axis(new TestFontBuilder());
        int dot = builder.AddGlyph(200, TestFontBuilder.Box(0, 0, 100, 100));
        int pair = builder.AddCompositeGlyph(600, (dot, 0, 0), (dot, 300, 0));
        builder.Map('A', pair);
        builder.AddWeightDeltas(pair, new (short, short)[] { (0, 0), (30, 10), (0, 0), (40, 0), (0, 0), (0, 0) });

        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { Weight = 900 });

        var data = font.Instance.GlyphData(pair);
        Assert.Equal(-1, BinaryPrimitives.ReadInt16BigEndian(data));
        // The second component's offsets follow its flags and glyph ID (words, because offsets are written as words).
        int second = 10 + 8;
        Assert.Equal(330, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(second + 4)));
        Assert.Equal(10, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(second + 6)));
        Assert.Equal(640, font.Instance.Advance(pair));
    }

    [Fact]
    public void Mvar_moves_font_metrics()
    {
        var builder = Box();
        builder.MvarDeltas["cpht"] = 50;
        builder.MvarDeltas["hasc"] = 20;

        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { Weight = 900 });

        Assert.Equal(750, font.Instance.CapHeight);
        Assert.Equal(820, font.Instance.Ascender);
    }

    [Fact]
    public void A_cff2_font_is_instanced_into_cff_charstrings()
    {
        var builder = Axis(new TestFontBuilder { Cff2 = true });
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0) });
        builder.HvarAdvanceDeltas[glyph] = 100;
        var bytes = builder.Build();

        var regular = PdfFont.FromBytes(bytes);
        var bold = PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 });

        Assert.True(bold.IsVariable);
        Assert.Equal(FontOutlines.Cff2, bold.Font.Outlines);
        Assert.Equal(600, regular.Instance.Advance(glyph));
        Assert.Equal(700, bold.Instance.Advance(glyph));
        Assert.Equal(Code(40, 0, 21, 0, 700, 5, 520, 0, 5, 0, -700, 5, 14), regular.Instance.Charstring(glyph));
        Assert.Equal(Code(40, 0, 21, 0, 700, 5, 620, 0, 5, 0, -700, 5, 14), bold.Instance.Charstring(glyph));
    }

    [Fact]
    public void A_static_font_keeps_its_own_data()
    {
        var builder = new TestFontBuilder().MapCharacters("A");
        var bytes = builder.Build();

        var font = PdfFont.FromBytes(bytes);

        Assert.False(font.IsVariable);
        Assert.Equal(OpenTypeFont.Parse(bytes).Advance(1), font.Instance.Advance(1));
        Assert.Equal(4, Points(font.Instance.GlyphData(1)).Length);
    }

    [Fact]
    public void Damaged_variation_data_fails_at_load()
    {
        var bytes = Box().Build();
        var (gvar, length) = Table(bytes, "gvar");
        // Overwrite everything after glyph 1's offset (the last glyph offset and the tuple data) with 0xFF.
        for (int i = gvar + 20 + 4 * 2; i < gvar + length; i++) bytes[i] = 0xFF;

        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 }));
        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
    }

    [Fact]
    public void Random_damage_to_variation_tables_never_escapes_as_another_exception()
    {
        var builder = Box();
        builder.HvarAdvanceDeltas[1] = 50;
        builder.MvarDeltas["cpht"] = 50;
        var original = builder.Build();
        var ranges = new[] { "fvar", "gvar", "HVAR", "MVAR" }.Select(t => Table(original, t)).ToArray();
        var random = new Random(4513);

        for (int run = 0; run < 3000; run++)
        {
            var font = (byte[])original.Clone();
            var (offset, length) = ranges[random.Next(ranges.Length)];
            for (int i = random.Next(1, 6); i > 0; i--) font[offset + random.Next(length)] = (byte)random.Next(256);
            try
            {
                PdfFont.FromBytes(font, new PdfFontLoadOptions { Weight = 1 + random.Next(1000) });
            }
            catch (PdfFontException)
            {
            }
        }
    }

    #endregion

    #region Private Methods

    private static TestFontBuilder Axis(TestFontBuilder builder)
    {
        builder.WeightAxis = (100, 400, 900);
        return builder;
    }

    // Glyph 1 ('A') is a 520 x 700 box whose right edge and advance grow by 100 at the axis maximum.
    private static TestFontBuilder Box()
    {
        var builder = Axis(new TestFontBuilder());
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0), (0, 0), (100, 0), (0, 0), (0, 0) });
        return builder;
    }

    private static byte[] Code(params int[] parts)
    {
        // Operators here are 5, 14, and 21, written as the last value of each group; everything else is a number.
        var operators = new HashSet<int> { 5, 14, 21 };
        var bytes = new List<byte>();
        int index = 0;
        foreach (int part in parts)
        {
            bool isOperator = operators.Contains(part) && (index == 2 || index == 5 || index == 8 || index == 11 || index == 12);
            if (isOperator) bytes.Add((byte)part);
            else bytes.AddRange(TestFontBuilder.CharstringNumber(part));
            index++;
        }
        return bytes.ToArray();
    }

    // Decodes a simple glyph written by the library (all flags on-curve with 16-bit coordinates, no instructions).
    private static (int X, int Y)[] Points(byte[] glyph)
    {
        int contours = BinaryPrimitives.ReadInt16BigEndian(glyph);
        int points = BinaryPrimitives.ReadUInt16BigEndian(glyph.AsSpan(10 + 2 * (contours - 1))) + 1;
        int instructions = BinaryPrimitives.ReadUInt16BigEndian(glyph.AsSpan(10 + 2 * contours));
        int p = 12 + 2 * contours + instructions;
        var flags = glyph.AsSpan(p, points).ToArray();
        p += points;
        var result = new (int X, int Y)[points];
        int x = 0, y = 0;
        for (int i = 0; i < points; i++)
        {
            Assert.Equal(0, flags[i] & 0x36); // 16-bit coordinates
            x += BinaryPrimitives.ReadInt16BigEndian(glyph.AsSpan(p));
            p += 2;
            result[i].X = x;
        }
        for (int i = 0; i < points; i++)
        {
            y += BinaryPrimitives.ReadInt16BigEndian(glyph.AsSpan(p));
            p += 2;
            result[i].Y = y;
        }
        return result;
    }

    private static (int Offset, int Length) Table(byte[] font, string tag)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            if (Encoding.ASCII.GetString(font, 12 + 16 * i, 4) == tag)
                return ((int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(12 + 16 * i + 8)), (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(12 + 16 * i + 12)));
        }
        throw new InvalidOperationException($"No {tag} table.");
    }

    #endregion

    #endregion
}
