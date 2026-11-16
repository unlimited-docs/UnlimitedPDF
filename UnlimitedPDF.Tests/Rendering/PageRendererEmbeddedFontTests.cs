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

    [Fact]
    public void Shared_composite_components_cannot_multiply_without_bound()
    {
        var builder = new TestFontBuilder();
        int box = builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 10, 10));
        int middle = builder.AddCompositeGlyph(600, Enumerable.Range(0, 40).Select(i => (box, (short)i, (short)0)).ToArray());
        int top = builder.AddCompositeGlyph(600, Enumerable.Range(0, 40).Select(i => (middle, (short)0, (short)i)).ToArray());
        builder.Map('A', top);
        var font = PdfFont.FromBytes(builder.Build());

        var page = Draw(font, "A", 0, 0, 10);

        Assert.False(page.IsDrawn);
        Assert.Contains("too many components", page.Reason);
    }

    [Theory]
    [InlineData(new[] { false, false, false, false }, "M0 50Q0 0 50 0Q100 0 100 50Q100 100 50 100Q0 100 0 50Z")]
    [InlineData(new[] { false, true, true, false }, "M0 50Q0 0 100 0L100 100Q0 100 0 50Z")]
    [InlineData(new[] { false, true, true, true }, "M0 100Q0 0 100 0L100 100Z")]
    [InlineData(new[] { true, true, true, true }, "M0 0L100 0L100 100L0 100Z")]
    public void Quadratic_contours_start_on_the_curve_wherever_their_points_begin(bool[] onCurve, string expected)
    {
        Assert.Equal(expected, GlyphOutlines.Contour(new double[] { 0, 100, 100, 0 }, new double[] { 0, 0, 100, 100 }, onCurve));
    }

    [Theory]
    [InlineData(new object[] { 0, 0, RMoveTo, 10, 0, 0, 10, -10, 0, RLineTo, EndChar }, "M0 0L10 0L10 10L0 10Z")]
    [InlineData(new object[] { 0, 0, RMoveTo, 5, 10, 20, 30, 40, (byte)26, EndChar }, "M0 0C5 10 25 40 25 80Z")]
    [InlineData(new object[] { 0, 0, RMoveTo, 10, 20, 30, 40, 5, (byte)31, EndChar }, "M0 0C10 0 30 30 35 70Z")]
    [InlineData(new object[] { 0, 0, RMoveTo, 10, 20, 30, 40, 5, (byte)30, EndChar }, "M0 0C0 10 20 40 60 45Z")]
    [InlineData(new object[] { 0, 0, RMoveTo, 10, 0, 10, 0, 10, 0, 10, 0, 10, 0, 10, 0, 50, (byte)12, (byte)35, EndChar }, "M0 0C10 0 20 0 30 0C40 0 50 0 60 0Z")]
    [InlineData(new object[] { 0, 0, RMoveTo, 10, 20, 0, 30, 40, 0, 5, 5, (byte)24, EndChar }, "M0 0C10 20 10 50 50 50L55 55Z")]
    public void Type2_operators_build_their_paths(object[] parts, string expected)
    {
        Assert.Equal(expected, GlyphOutlines.Type2(Code(parts)));
    }

    [Fact]
    public void Repeated_or_long_text_map_entries_are_bounded()
    {
        string ranges = string.Concat(Enumerable.Repeat("1 beginbfrange <0000> <FFFF> <0041> endbfrange\n", 3));
        Assert.Contains("too large", Assert.Throws<PageRenderException>(() => ToUnicodeMap.Read(Encoding.ASCII.GetBytes(ranges))).Message);

        string longText = "1 beginbfchar <0001> <" + string.Concat(Enumerable.Repeat("0041", 300)) + "> endbfchar";
        Assert.Contains("too long a text", Assert.Throws<PageRenderException>(() => ToUnicodeMap.Read(Encoding.ASCII.GetBytes(longText))).Message);
    }

    [Fact]
    public void Repeated_width_ranges_are_bounded()
    {
        byte[] pdf = Save(BoxFont(), "A", 0, 0, 10);
        string text = Encoding.Latin1.GetString(pdf);
        text = Regex.Replace(text, @"/W \[[^\]]*\]\]", "/W [0 65535 500 0 65535 500 0 65535 500]");

        var page = RenderTestFiles.RenderFirst(Encoding.Latin1.GetBytes(text));

        Assert.Contains("widths are too many", page.Reason);
    }

    [Fact]
    public void Stroked_glyphs_with_a_zero_width_keep_a_hairline()
    {
        var page = RenderTestFiles.RenderFirst(EditContent(Save(BoxFont(), "A", 0, 0, 10), content => content.Replace("BT", "BT 1 Tr 0 w")));

        Assert.Contains("fill=\"none\" stroke=\"#000000\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"", page.Svg);
    }

    [Fact]
    public void Actual_text_over_two_shows_is_placed_once()
    {
        byte[] pdf = EditContent(Save(BoxFont(), "A", 0, 0, 10), content =>
            Regex.Replace(content, @"(<[0-9A-Fa-f]+> Tj)", "/Span << /ActualText <FEFF0058> >> BDC $1 $1 EMC"));

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Single(Regex.Matches(page.Svg!, "fill-opacity=\"0\">X</text>"));
        Assert.Equal(new[] { "X" }, page.TextLines);
    }

    [Fact]
    public void Glyphs_follow_horizontal_scaling_and_rise()
    {
        var page = RenderTestFiles.RenderFirst(EditContent(Save(BoxFont(), "A", 100, 700, 10), content => content.Replace("BT", "BT 50 Tz 3 Ts")));

        Assert.Contains("<use href=\"#t-f0g1\" transform=\"matrix(0.005 0 0 -0.01 100 138.89)\"/>", page.Svg);
    }

    #endregion

    #region Private Methods

    private static byte[] Code(params object[] parts) => parts.SelectMany(p => p switch
    {
        byte op => new[] { op },
        int number => TestFontBuilder.CharstringNumber(number),
        _ => throw new ArgumentException("Unexpected part."),
    }).ToArray();

    private static PdfFont BoxFont()
    {
        var builder = new TestFontBuilder();
        builder.Map('A', builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 100, 100)));
        return PdfFont.FromBytes(builder.Build());
    }

    // Rewrites the page's (uncompressed) content stream: the stream around the first BT. The reader recovers the
    // cross-references and lengths that move.
    private static byte[] EditContent(byte[] pdf, Func<string, string> edit)
    {
        string text = Encoding.Latin1.GetString(pdf);
        int bt = text.IndexOf("BT", StringComparison.Ordinal);
        Assert.True(bt > 0, "No text object found.");
        int start = text.LastIndexOf("stream", bt, StringComparison.Ordinal) + "stream".Length;
        while (text[start] is '\r' or '\n') start++;
        int end = text.IndexOf("endstream", bt, StringComparison.Ordinal);
        string edited = edit(text.Substring(start, end - start));
        return Encoding.Latin1.GetBytes(text.Substring(0, start) + edited + text.Substring(end));
    }

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
