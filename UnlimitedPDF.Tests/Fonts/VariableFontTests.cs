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

    [Fact]
    public void Html_weights_of_a_variable_font_round_to_the_nearest_hundred_on_the_same_side_of_bold()
    {
        // RV-46, RV-51, RV-52: HTML may ask for any weight from 1 to 1000, and each weight used to build and keep its
        // own instance; rounding bounds the instances whatever was asked before, and bold stays bold.
        var builder = Axis(new TestFontBuilder { Cff2 = true });
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0) });
        var font = PdfFont.FromBytes(builder.Build());

        var faces = Enumerable.Range(1, 1000).Select(font.AtWeight).ToList();

        Assert.Equal(9, faces.Distinct().Count()); // 100 to 900
        Assert.Equal(100, font.AtWeight(149).Weight);
        Assert.Equal(200, font.AtWeight(150).Weight);
        Assert.Equal(500, font.AtWeight(599).Weight); // not bold, so not 600
        Assert.Equal(600, font.AtWeight(600).Weight);
        Assert.Equal(700, font.AtWeight(650).Weight);
        Assert.Equal(900, font.AtWeight(1000).Weight); // clamped to the axis
        Assert.Same(font, font.AtWeight(420));
        Assert.Same(font.AtWeight(700), font.AtWeight(700).AtWeight(680)); // every weight shares one set
    }

    [Fact]
    public void TrueType_glyphs_that_declare_many_points_in_few_bytes_are_corrupt_when_varied()
    {
        // RV-50: about 528 bytes of flags may declare 65,535 points that instancing writes at 5 bytes each; 300 such
        // glyphs in a 160 KB font kept 98.6 MB for one other weight (about 21.5 GB for a 34 MB font).
        var builder = Box();
        var raw = ManyPoints();
        for (int i = 0; i < 300; i++) builder.AddRawGlyph(600, raw);
        var font = PdfFont.FromBytes(builder.Build());

        var error = Assert.Throws<PdfFontException>(() => font.AtWeight(900));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("TrueType glyphs", error.Message); // the output budget stops it first
    }

    [Fact]
    public void TrueType_glyphs_whose_variations_repeat_many_small_tuples_are_corrupt_when_varied()
    {
        // RV-57: a tuple of 16 bytes that moves one of a glyph's 65,535 points makes the reader interpolate every point
        // again; 4,095 such tuples in about 65 KB took 9 seconds and allocated 4.6 GB for one other weight.
        var builder = Box();
        int glyph = builder.AddRawGlyph(600, ManyPoints());
        for (int t = 0; t < 4_095; t++) builder.AddWeightDeltas(glyph, new (short, short)[] { (1, 1) }, new[] { 0 });
        var bytes = builder.Build();

        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes).AtWeight(900));
        var loaded = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 }));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("variations that together take too long", error.Message);
        Assert.Contains("variations that together take too long", loaded.Message);
    }

    [Fact]
    public void The_other_weights_of_a_variable_font_share_one_budget()
    {
        // RV-58: each weight used to get its own budget, so the dozen weights HTML may ask for could keep a dozen
        // instances' worth of a hostile font's glyphs. Here one weight keeps 5.2 MB of an 8.4 MB instance budget, and
        // the weights share four budgets, so the seventh other weight is stopped.
        var builder = Box();
        var raw = ManyPoints();
        for (int i = 0; i < 16; i++) builder.AddRawGlyph(600, raw);
        var bytes = builder.Build();
        var font = PdfFont.FromBytes(bytes);

        Assert.Equal(700, PdfFont.FromBytes(bytes).AtWeight(700).Weight);
        var error = Assert.Throws<PdfFontException>(() =>
        {
            foreach (int weight in new[] { 100, 200, 300, 500, 600, 700, 800, 900 }) font.AtWeight(weight);
        });

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("grow too large when varied", error.Message);
    }

    [Fact]
    public void TrueType_variations_that_move_far_more_points_per_byte_than_real_fonts_are_corrupt()
    {
        // RV-64: the variation budget allowed 16 points per byte of glyf and gvar, where the tuples real fonts apply at
        // one weight move at most about 0.2, so a hostile font near the size limit took about 7 seconds per weight.
        // Here 3 MB of tuples that move every point of a 1,000-point glyph (0.25 points per byte) let 250 one-point
        // tuples on a glyph of 65,535 points move 16.4 million points per weight: within 16 per byte, not within 2.
        var builder = Box();
        int ballast = builder.AddGlyph(600, Enumerable.Range(0, 1_000).Select(i => ((short)i, (short)(i % 2 * 100))).ToArray());
        var moves = Enumerable.Repeat(((short)300, (short)300), 1_004).ToArray();
        for (int t = 0; t < 750; t++) builder.AddWeightDeltas(ballast, moves);
        int glyph = builder.AddRawGlyph(600, ManyPoints());
        for (int t = 0; t < 250; t++) builder.AddWeightDeltas(glyph, new (short, short)[] { (1, 1) }, new[] { 0 });
        var bytes = builder.Build();
        var font = PdfFont.FromBytes(bytes);

        var loaded = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 }));
        var error = Assert.Throws<PdfFontException>(() =>
        {
            foreach (int weight in new[] { 500, 600, 700, 800, 900 }) font.AtWeight(weight);
        });

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("variations that together take too long", loaded.Message);
        Assert.Contains("variations that together take too long", error.Message);
    }

    [Fact]
    public void TrueType_variations_decode_their_deltas_without_allocating_per_tuple()
    {
        // RV-65: each tuple that applies read its deltas into two new arrays of its point count, so tuples of a few
        // bytes over every point of a 65,535-point glyph allocated about 512 KB each (6.3 GB per weight near the size
        // limit). Thirty such tuples must cost about what one does.
        long Allocated(int tuples)
        {
            var builder = Box();
            int glyph = builder.AddRawGlyph(600, ManyPoints());
            var zeros = new (short, short)[65_539];
            for (int t = 0; t < tuples; t++) builder.AddWeightDeltas(glyph, zeros);
            var bytes = builder.Build();
            PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 }); // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 });
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        long one = Allocated(1), thirty = Allocated(30);

        Assert.True(thirty - one < 2L * 1024 * 1024, $"Thirty tuples allocated {thirty - one:N0} bytes more than one.");
    }

    [Fact]
    public void Every_tuple_that_applies_reads_its_own_deltas()
    {
        // The scratch arrays a glyph's tuples reuse are cleared for each tuple, so a tuple of zeros after one that moves
        // points adds nothing: over every point (zero runs) and over a point list (interpolated).
        var everyPoint = Box();
        everyPoint.AddWeightDeltas(1, new (short, short)[8]);
        var pointList = Axis(new TestFontBuilder());
        int glyph = pointList.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        pointList.Map('A', glyph);
        pointList.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (100, 50) }, new[] { 0, 2 });
        pointList.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0) }, new[] { 0, 2 });

        var first = PdfFont.FromBytes(everyPoint.Build(), new PdfFontLoadOptions { Weight = 900 });
        var second = PdfFont.FromBytes(pointList.Build(), new PdfFontLoadOptions { Weight = 900 });

        Assert.Equal(700, first.Instance.Advance(1));
        Assert.Equal(new[] { (40, 0), (40, 700), (660, 700), (660, 0) }, Points(first.Instance.GlyphData(1)));
        Assert.Equal(new[] { (40, 0), (40, 750), (660, 750), (660, 0) }, Points(second.Instance.GlyphData(glyph)));
    }

    #endregion

    #region Private Methods

    // A glyph of about 528 bytes that declares 65,535 points on one contour: flags on curve, with the same x and y,
    // repeated.
    private static byte[] ManyPoints()
    {
        var glyph = new BigEndianWriter().I16(1).I16(0).I16(0).I16(0).I16(0).U16(65534).U16(0);
        for (int i = 0; i < 256; i++) glyph.U8(0x39).U8((byte)(i < 255 ? 255 : 254));
        return glyph.ToArray();
    }

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
