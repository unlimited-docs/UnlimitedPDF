using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Embedding CFF and CFF2 fonts (AC-09, AC-10, D-010, D-013): name-keyed, CID-keyed, and variable CFF2 fonts are all
/// embedded as one CID-keyed CFF program (CIDFontType0, <c>FontFile3 /CIDFontType0C</c>) whose CIDs equal glyph IDs,
/// keeping only drawn outlines, without subroutines, and at the chosen weight.
/// </summary>
public class EmbeddedCffTests
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_cff_font_is_embedded_as_a_cid_keyed_subset(bool cidKeyedSource)
    {
        var builder = new TestFontBuilder { Cff = true, CffCidKeyed = cidKeyedSource };
        builder.MapCharacters("Hello, world");
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hello"), 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf); // includes: the program is CID-keyed and every charstring reads
        var embedded = Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.Equal("CIDFontType0", embedded.CidFontSubtype);
        Assert.Equal("FontFile3", embedded.ProgramKey);
        Assert.Equal(font.Font.GlyphCount, embedded.GlyphCount);
        Assert.Equal(new[] { 0 }.Concat("Helo".Select(c => font.Font.CharacterMap.GlyphFor(c))).OrderBy(g => g), embedded.GlyphsWithOutlines.OrderBy(g => g));
        Assert.Equal(600, embedded.Widths[font.Font.CharacterMap.GlyphFor('H')]);
        Assert.Equal("Hello", Assert.Single(PdfInspector.Read(pdf).PageText()));
    }

    [Fact]
    public void Subroutine_calls_are_expanded_in_the_embedded_program()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.CffLocalSubrs.Add(Code(10, 20, RMoveTo, Return));
        builder.CffGlobalSubrs.Add(Code(100, 0, RLineTo, Return));
        int glyph = builder.AddCharstringGlyph(500, Code(500, -107, CallSubr, -107, CallGSubr, EndChar));
        builder.Map('A', glyph);
        int unused = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('Z', unused);
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, new[] { (glyph, "A") }, 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var charstrings = EmbeddedFont.CffCharstrings(Assert.Single(PdfInspector.Read(pdf).Fonts()).Program);
        Assert.Equal(Code(500, 10, 20, RMoveTo, 100, 0, RLineTo, EndChar), charstrings[glyph]);
        Assert.Equal(new[] { EndChar }, charstrings[unused]); // an undrawn glyph keeps only endchar
    }

    [Fact]
    public void A_cff2_variable_font_embeds_its_chosen_weight()
    {
        var bytes = Cff2Box(out int glyph);

        var regular = Embed(PdfFont.FromBytes(bytes), glyph);
        var bold = Embed(PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 }), glyph);

        Assert.Equal(600, regular.Widths[glyph]);
        Assert.Equal(700, bold.Widths[glyph]);
        var regularGlyph = EmbeddedFont.CffCharstrings(regular.Program)[glyph];
        var boldGlyph = EmbeddedFont.CffCharstrings(bold.Program)[glyph];
        Assert.Equal(Code(40, 0, RMoveTo, 0, 700, RLineTo, 520, 0, RLineTo, 0, -700, RLineTo, EndChar), regularGlyph);
        Assert.Equal(Code(40, 0, RMoveTo, 0, 700, RLineTo, 620, 0, RLineTo, 0, -700, RLineTo, EndChar), boldGlyph);
        Assert.NotEqual(regular.BaseFont, bold.BaseFont); // a different instance gets a different tag
    }

    [Fact]
    public void A_cff_font_that_forbids_subsetting_is_embedded_whole()
    {
        var builder = new TestFontBuilder { Cff = true, FsType = 0x0100 };
        builder.MapCharacters("Hello, world");
        var font = PdfFont.FromBytes(builder.Build());

        var embedded = Embed(font, font.Font.CharacterMap.GlyphFor('H'));

        Assert.Equal(Enumerable.Range(0, font.Font.GlyphCount).ToHashSet(), embedded.GlyphsWithOutlines.ToHashSet());
    }

    [Fact]
    public void Saving_a_cff_font_twice_gives_the_same_bytes()
    {
        var builder = new TestFontBuilder { Cff = true };
        builder.MapCharacters("Hello");
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hello"), 72, 720);

        Assert.Equal(PdfPageReader.SaveBytes(document), PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void A_font_whose_em_is_not_1000_units_states_its_scale()
    {
        var builder = new TestFontBuilder("Test Sans", unitsPerEm: 2048) { Cff = true };
        builder.MapCharacters("Hi", advance: 1229);
        var font = PdfFont.FromBytes(builder.Build());

        var embedded = Embed(font, font.Font.CharacterMap.GlyphFor('H'));

        Assert.Equal(600, embedded.Widths[font.Font.CharacterMap.GlyphFor('H')]); // 1229 / 2048 em
        // Top DICT FontMatrix: the real 0.00048828125 (1/2048) followed by the operator 12 7.
        Assert.Contains("1E 0A 00 04 88 28 12 5F", BitConverter.ToString(embedded.Program).Replace('-', ' '));
    }

    #endregion

    #region Private Methods

    private static byte[] Code(params object[] parts) => parts.SelectMany(p => p switch
    {
        byte op => new[] { op },
        int number => TestFontBuilder.CharstringNumber(number),
        _ => throw new ArgumentException("Unexpected part.")
    }).ToArray();

    private static byte[] Cff2Box(out int glyph)
    {
        var builder = new TestFontBuilder { Cff2 = true, WeightAxis = (100, 400, 900) };
        glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0) });
        builder.HvarAdvanceDeltas[glyph] = 100;
        return builder.Build();
    }

    private static EmbeddedFont Embed(PdfFont font, int glyph)
    {
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, new[] { (glyph, "A") }, 72, 720);
        var pdf = PdfPageReader.SaveBytes(document);
        PdfStructureChecker.AssertValid(pdf);
        return Assert.Single(PdfInspector.Read(pdf).Fonts());
    }

    private static (int Glyph, string Text)[] Run(PdfFont font, string text) =>
        text.Select(c => (font.Font.CharacterMap.GlyphFor(c), c.ToString())).ToArray();

    #endregion

    #endregion
}
