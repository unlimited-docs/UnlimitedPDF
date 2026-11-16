using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// Text in caller fonts the library embeds: glyphs drawn from the embedded TrueType or CFF outlines, placed by the
/// font's widths, with an invisible text run that carries the characters (26111701 AC-12, AC-14, V-04).
/// </summary>
public class PageRendererEmbeddedFontTests
{
    #region Fields and Constants

    private const byte EndChar = 14;
    private const byte RLineTo = 5;
    private const byte RMoveTo = 21;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void TrueType_glyphs_are_drawn_from_their_outlines_once_and_placed_by_their_widths()
    {
        var builder = new TestFontBuilder();
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        var font = PdfFont.FromBytes(builder.Build());

        var page = Draw(font, "AA", 100, 700, 10);

        Assert.True(page.IsDrawn, page.Reason);
        var definition = Assert.Single(Regex.Matches(page.Svg!, "<path id=\"t-f0g\\d+\" d=\"([^\"]+)\"/>"));
        Assert.Equal(Outline(TestFontBuilder.Box(40, 0, 560, 700)), definition.Groups[1].Value);
        var uses = Regex.Matches(page.Svg!, "<use href=\"#t-f0g\\d+\" transform=\"([^\"]+)\"/>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "matrix(0.01 0 0 -0.01 100 141.89)", "matrix(0.01 0 0 -0.01 106 141.89)" }, uses);
        Assert.Equal(2, page.Counts.Glyphs);
        Assert.Equal(new[] { "AA" }, page.TextLines);
        Assert.Contains("textLength=\"12\" lengthAdjust=\"spacingAndGlyphs\" fill=\"#000000\" fill-opacity=\"0\">AA</text>", page.Svg);
    }

    [Fact]
    public void Cff_glyphs_are_drawn_from_their_charstrings()
    {
        var builder = new TestFontBuilder { Cff = true };
        int glyph = builder.AddCharstringGlyph(500, Code(500, 10, 20, RMoveTo, 100, 0, RLineTo, 0, 100, RLineTo, EndChar));
        builder.Map('A', glyph);
        var font = PdfFont.FromBytes(builder.Build());

        var page = Draw(font, "A", 0, 0, 10);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains("d=\"M10 20L110 20L110 120Z\"", page.Svg);
        Assert.Equal(new[] { "A" }, page.TextLines);
    }

    [Fact]
    public void Composite_truetype_glyphs_place_their_components()
    {
        var builder = new TestFontBuilder();
        int box = builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 100, 100));
        int composite = builder.AddCompositeGlyph(600, (box, 0, 0), (box, 200, 50));
        builder.Map('B', composite);
        var font = PdfFont.FromBytes(builder.Build());

        var page = Draw(font, "B", 0, 0, 10);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains(Outline(TestFontBuilder.Box(0, 0, 100, 100)) + Outline(TestFontBuilder.Box(200, 50, 300, 150)), page.Svg);
    }

    [Fact]
    public void Fonts_with_other_em_sizes_are_scaled_by_their_units()
    {
        var builder = new TestFontBuilder(unitsPerEm: 2048);
        builder.Map('A', builder.AddGlyph(1024, TestFontBuilder.Box(0, 0, 1024, 1024)));
        var font = PdfFont.FromBytes(builder.Build());

        var page = Draw(font, "A", 0, 0, 20.48);

        Assert.Contains("matrix(0.01 0 0 -0.01 0 841.89)", page.Svg);
    }

    [Theory]
    [InlineData("/Encoding /Identity-V")]
    [InlineData("/CIDToGIDMap 9 0 R")]
    public void Embedded_fonts_it_does_not_draw_leave_the_page_undrawn(string change)
    {
        var builder = new TestFontBuilder();
        builder.Map('A', builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 100, 100)));
        byte[] pdf = Save(PdfFont.FromBytes(builder.Build()), "A", 0, 0, 10);
        string text = Encoding.Latin1.GetString(pdf);
        text = change.StartsWith("/Encoding", StringComparison.Ordinal)
            ? text.Replace("/Encoding /Identity-H", change)
            : text.Replace("/CIDToGIDMap /Identity", change);

        var page = RenderTestFiles.RenderFirst(Encoding.Latin1.GetBytes(text));

        Assert.False(page.IsDrawn);
        Assert.Contains("kind of font", page.Reason);
    }

    [Fact]
    public void A_damaged_font_program_leaves_the_page_undrawn()
    {
        var builder = new TestFontBuilder();
        builder.Map('A', builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 100, 100)));
        byte[] pdf = Save(PdfFont.FromBytes(builder.Build()), "A", 0, 0, 10);
        string text = Encoding.Latin1.GetString(pdf);
        int program = text.IndexOf("/FontFile2 ", StringComparison.Ordinal);
        Assert.True(program > 0);

        // Point the program at the ToUnicode stream: not a font.
        var match = Regex.Match(text, @"/ToUnicode (\d+) 0 R");
        text = Regex.Replace(text, @"/FontFile2 \d+ 0 R", $"/FontFile2 {match.Groups[1].Value} 0 R");

        var page = RenderTestFiles.RenderFirst(Encoding.Latin1.GetBytes(text));

        Assert.False(page.IsDrawn);
        Assert.Contains("font", page.Reason);
    }

    [Fact]
    public void ToUnicode_ranges_in_both_forms_map_their_codes()
    {
        var map = ToUnicodeMap.Read(Encoding.ASCII.GetBytes(
            "1 begincodespacerange <0000> <FFFF> endcodespacerange\n" +
            "2 beginbfchar <0003> <0041> <0004> <D835DC00> endbfchar\n" +
            "2 beginbfrange <0010> <0012> <0061> <0020> <0021> [<0078> <00790079>] endbfrange"));

        Assert.Equal("A", map.Text(3));
        Assert.Equal("\U0001D400", map.Text(4));
        Assert.Equal("abc", map.Text(0x10) + map.Text(0x11) + map.Text(0x12));
        Assert.Equal("x", map.Text(0x20));
        Assert.Equal("yy", map.Text(0x21));
        Assert.Equal(string.Empty, map.Text(0x99));
    }

    [Theory]
    [InlineData("1 beginbfrange <0010> <0001> <0041> endbfrange", "bad range")]
    [InlineData("1 beginbfchar <000102> <0041> endbfchar", "one or two bytes")]
    [InlineData("1 beginbfrange <0000> <FFFF> /x endbfrange", "damaged")]
    public void Damaged_text_maps_are_refused(string cmap, string reason)
    {
        var exception = Assert.Throws<PageRenderException>(() => ToUnicodeMap.Read(Encoding.ASCII.GetBytes(cmap)));
        Assert.Contains(reason, exception.Message);
    }

    [Theory]
    [InlineData(new byte[] { 5, 0, 5, 1, 14 }, "draws before it moves")]
    [InlineData(new byte[] { 139, 139, 139, 139, 14 }, "accented-character")]
    [InlineData(new byte[] { 139, 139, 21, 2 }, "charstring operator")]
    [InlineData(new byte[] { 28, 1 }, "ends inside a number")]
    public void Charstrings_it_does_not_draw_are_refused(byte[] charstring, string reason)
    {
        var exception = Assert.Throws<PageRenderException>(() => GlyphOutlines.Type2(charstring));
        Assert.Contains(reason, exception.Message);
    }

    [Fact]
    public void Type2_curves_flex_and_hint_masks_are_read()
    {
        // hstem pair, hintmask (one byte for one stem), rmoveto, hvcurveto, hhcurveto, endchar
        byte[] charstring = Code(10, 20, (byte)1, (byte)19, (byte)0x80, 0, 0, RMoveTo, 10, 20, 30, 40, (byte)31, 5, 10, 20, 30, 40, (byte)27, EndChar);

        Assert.Equal("M0 0C10 0 30 30 30 70C40 75 60 105 100 105Z", GlyphOutlines.Type2(charstring));
    }

    #endregion

    #region Private Methods

    private static byte[] Code(params object[] parts) => parts.SelectMany(p => p switch
    {
        byte op => new[] { op },
        int number => TestFontBuilder.CharstringNumber(number),
        _ => throw new ArgumentException("Unexpected part."),
    }).ToArray();

    private static PageRenderResult Draw(PdfFont font, string text, double x, double y, double size) =>
        RenderTestFiles.RenderFirst(Save(font, text, x, y, size));

    // The path the renderer writes for one closed polygon of on-curve points.
    private static string Outline((short X, short Y)[] points) =>
        "M" + string.Join("L", points.Select(p => $"{p.X} {p.Y}")) + "Z";

    private static byte[] Save(PdfFont font, string text, double x, double y, double size)
    {
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, text.Select(c => (font.Font.CharacterMap.GlyphFor(c), c.ToString())).ToArray(), x, y, size);
        return PdfPageReader.SaveBytes(document);
    }

    #endregion

    #endregion
}
