using System.Buffers.Binary;
using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// CFF outlines (AC-01, D-010, A06): name-keyed and CID-keyed <c>.otf</c> fonts load; every glyph is flattened at
/// load, so subroutine nesting over 10 levels, more than 48 operands, missing subroutines, missing endchar, and
/// runaway expansion fail with <see cref="PdfFontException"/>.
/// </summary>
[Collection(nameof(FontFuzzCollection))]
public class CffFontTests
{
    #region Fields and Constants

    private const byte CallSubr = 10;
    private const byte CallGSubr = 29;
    private const byte Return = 11;
    private const byte EndChar = 14;
    private const byte RLineTo = 5;
    private const byte RMoveTo = 21;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_name_keyed_cff_font_loads()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.MapCharacters("Hello", advance: 610);

        var font = PdfFont.FromBytes(builder.Build());

        Assert.Equal(FontOutlines.Cff, font.Font.Outlines);
        Assert.False(font.Font.Cff!.IsCidKeyed);
        Assert.Equal(builder.GlyphCount, font.Font.Cff.CharStrings.Count);
        Assert.Equal(610, font.Font.Advance(font.Font.CharacterMap.GlyphFor('H')));
        Assert.Equal("TestSans", font.Font.Cff.Name);
    }

    [Fact]
    public void A_cid_keyed_cff_font_loads()
    {
        var builder = new TestFontBuilder { Cff = true, CffCidKeyed = true };
        builder.MapCharacters("తెలుగు");

        var font = PdfFont.FromBytes(builder.Build());

        Assert.True(font.Font.Cff!.IsCidKeyed);
        Assert.Equal(builder.GlyphCount, font.Font.Cff.CharStrings.Count);
    }

    [Fact]
    public void Glyphs_without_subroutines_flatten_to_themselves()
    {
        var builder = new TestFontBuilder { Cff = true };
        var code = Code(500, 10, 20, RMoveTo, 100, 0, 0, 100, -100, 0, RLineTo, EndChar);
        int glyph = builder.AddCharstringGlyph(500, code);

        var font = OpenTypeFont.Parse(builder.Build());

        Assert.Equal(code, font.Cff!.Flatten(glyph));
    }

    [Fact]
    public void Local_and_global_subroutine_calls_are_expanded()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.CffLocalSubrs.Add(Code(10, 20, RMoveTo, Return));
        builder.CffGlobalSubrs.Add(Code(100, 0, RLineTo, Return));
        int glyph = builder.AddCharstringGlyph(500, Code(500, -107, CallSubr, -107, CallGSubr, EndChar));

        var font = OpenTypeFont.Parse(builder.Build());

        Assert.Equal(Code(500, 10, 20, RMoveTo, 100, 0, RLineTo, EndChar), font.Cff!.Flatten(glyph));
    }

    [Fact]
    public void Operands_pushed_by_a_subroutine_stay_for_the_caller()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.CffLocalSubrs.Add(Code(10, 20, Return));
        int glyph = builder.AddCharstringGlyph(500, Code(500, -107, CallSubr, RMoveTo, EndChar));

        var font = OpenTypeFont.Parse(builder.Build());

        Assert.Equal(Code(500, 10, 20, RMoveTo, EndChar), font.Cff!.Flatten(glyph));
    }

    [Fact]
    public void Hint_masks_keep_their_mask_bytes()
    {
        var builder = new TestFontBuilder { Cff = true };
        // Width, two horizontal stems (hstemhm), then a hint mask whose one byte follows the operator.
        var code = Code(500, 0, 50, 100, 50, 18).Concat(new byte[] { 19, 0b1100_0000 }).Concat(Code(0, 0, RMoveTo, 10, 10, RLineTo, EndChar)).ToArray();
        int glyph = builder.AddCharstringGlyph(500, code);

        var font = OpenTypeFont.Parse(builder.Build());

        Assert.Equal(code, font.Cff!.Flatten(glyph));
    }

    [Fact]
    public void Subroutine_nesting_is_capped_at_ten_levels()
    {
        PdfFont.FromBytes(Nested(10));

        AssertCorrupt(Nested(11), "deeper than 10");
    }

    [Fact]
    public void More_than_48_operands_are_corrupt()
    {
        PdfFont.FromBytes(Operands(48));

        AssertCorrupt(Operands(49), "more than 48 operands");
    }

    [Fact]
    public void A_missing_subroutine_is_corrupt()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.AddCharstringGlyph(500, Code(500, 5, CallSubr, EndChar));

        AssertCorrupt(builder.Build(), "subroutine that does not exist");
    }

    [Fact]
    public void A_glyph_without_endchar_is_corrupt()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.AddCharstringGlyph(500, Code(500, 0, 0, RMoveTo));

        AssertCorrupt(builder.Build(), "does not end with endchar");
    }

    [Fact]
    public void Subroutines_that_expand_too_far_are_corrupt()
    {
        // Each of nine levels calls the next three times: 3^9 copies of the innermost two lines (about 118 KB).
        var builder = new TestFontBuilder { Cff = true };
        for (int level = 0; level < 9; level++)
        {
            int next = level + 1 - 107;
            builder.CffLocalSubrs.Add(Code(next, CallSubr, next, CallSubr, next, CallSubr, Return));
        }
        builder.CffLocalSubrs.Add(Code(1, 1, RLineTo, 1, 1, RLineTo, Return));
        builder.AddCharstringGlyph(500, Code(500, 0, 0, RMoveTo, -107, CallSubr, EndChar));

        var clock = Stopwatch.StartNew();
        AssertCorrupt(builder.Build(), "expands too far");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
    }

    [Fact]
    public void A_glyph_count_that_differs_from_maxp_is_corrupt()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.MapCharacters("Hi");
        var font = builder.Build();
        int maxp = Offset(font, "maxp");
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(maxp + 4), (ushort)(builder.GlyphCount + 1));

        AssertCorrupt(font, "differs from maxp");
    }

    [Fact]
    public void Variable_cff2_outlines_are_not_supported_yet()
    {
        var font = new TestFontBuilder().MapCharacters("A").AddTable("CFF2", new byte[] { 2, 0, 5, 0, 0 }).Build();

        Assert.Equal(PdfFontErrorReason.UnsupportedFormat, Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(font)).Reason);
    }

    [Fact]
    public void Random_damage_to_a_cff_font_never_escapes_as_another_exception()
    {
        var builder = new TestFontBuilder { Cff = true, CffCidKeyed = true };
        builder.MapCharacters("Hello");
        builder.CffLocalSubrs.Add(Code(10, 20, RMoveTo, Return));
        builder.AddCharstringGlyph(500, Code(500, -107, CallSubr, EndChar));
        var original = builder.Build();
        var random = new Random(4545);
        var clock = Stopwatch.StartNew();

        for (int run = 0; run < 3000; run++)
        {
            var font = (byte[])original.Clone();
            for (int i = random.Next(1, 9); i > 0; i--) font[random.Next(font.Length)] = (byte)random.Next(256);
            try
            {
                OpenTypeFont.Parse(font);
            }
            catch (PdfFontException)
            {
            }
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), clock.Elapsed.ToString());
    }

    #endregion

    #region Private Methods

    private static void AssertCorrupt(byte[] font, string message)
    {
        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(font));
        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains(message, error.Message);
    }

    // Numbers become charstring operands; values marked as operators (by being passed as byte) become operators.
    private static byte[] Code(params object[] parts) => parts.SelectMany(p => p switch
    {
        byte op => new[] { op },
        int number => TestFontBuilder.CharstringNumber(number),
        _ => throw new ArgumentException("Unexpected part.")
    }).ToArray();

    private static byte[] Nested(int levels)
    {
        var builder = new TestFontBuilder { Cff = true };
        for (int level = 0; level < levels - 1; level++) builder.CffLocalSubrs.Add(Code(level + 1 - 107, CallSubr, Return));
        builder.CffLocalSubrs.Add(Code(Return));
        builder.AddCharstringGlyph(500, Code(500, -107, CallSubr, EndChar));
        return builder.Build();
    }

    private static byte[] Operands(int count)
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.AddCharstringGlyph(500, Code(Enumerable.Repeat((object)1, count).Append(EndChar).ToArray()));
        return builder.Build();
    }

    private static int Offset(byte[] font, string tag)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            if (Encoding.ASCII.GetString(font, 12 + 16 * i, 4) == tag)
                return (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(12 + 16 * i + 8));
        }
        throw new InvalidOperationException($"No {tag} table.");
    }

    #endregion

    #endregion
}
