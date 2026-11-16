using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// Text in the standard fonts: placed by the PDF's widths, drawn as real text that can be selected and read, and the
/// page's text layer equal to what the PDF's own text mapping gives (26111701 AC-12, AC-14, V-04, V-09).
/// </summary>
public class PageRendererTextTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [MemberData(nameof(StandardFontGoldens))]
    public void The_text_layer_of_every_page_equals_the_pdf_text(string file)
    {
        byte[] pdf = File.ReadAllBytes(file);
        var expected = PdfInspector.Read(pdf).PageText();

        var pages = RenderTestFiles.RenderAll(pdf);

        Assert.Equal(expected.Count, pages.Count);
        for (int i = 0; i < pages.Count; i++)
        {
            Assert.True(pages[i].IsDrawn, $"{Path.GetFileName(file)} page {i + 1}: {pages[i].Reason}");
            Assert.Equal(expected[i], string.Join("\n", pages[i].TextLines));
        }
    }

    [Fact]
    public void Characters_are_placed_by_the_pdf_widths()
    {
        var page = Render("BT /F1 12 Tf 100 700 Td (AB) Tj ET");

        // A is 667/1000 em in Helvetica: 8.004 at 12 points.
        Assert.Contains("<text xml:space=\"preserve\" transform=\"matrix(1 0 0 1 100 142)\" font-family=\"Helvetica, Arial, &#39;Liberation Sans&#39;, sans-serif\"", page.Svg!.Replace("'", "&#39;"));
        Assert.Contains("font-size=\"12\" x=\"0 8.004\" y=\"0\" fill=\"#000000\">AB</text>", page.Svg);
        Assert.Equal(new[] { "AB" }, page.TextLines);
        Assert.Equal(2, page.Counts.Glyphs);
    }

    [Fact]
    public void Spacing_scaling_rise_and_tj_adjustments_move_the_characters()
    {
        var page = Render("BT /F1 10 Tf 2 Tc 3 Tw 50 Tz 4 Ts 0 0 Td [(A) -500 ( B)] TJ (C) Tj ET");

        // A: 6.67 + 2; then -(-500)/1000*10 = +5; space: 2.78 + 2 + 3; B ...
        Assert.Contains("x=\"0 13.67 21.45\"", page.Svg);
        Assert.Contains("transform=\"matrix(0.5 0 0 1 0 838)\"", page.Svg); // 50 % width, raised 4 points
        // C starts after A, the adjustment, the space, and B, scaled by 50 %.
        Assert.Contains("transform=\"matrix(0.5 0 0 1 15.06 838)\"", page.Svg);
    }

    [Fact]
    public void Line_operators_move_by_the_leading()
    {
        var page = Render("BT /F1 10 Tf 14 TL 20 100 Td (a) Tj T* (b) Tj (c) ' 1 2 (d) \" 5 -7 TD (e) Tj ET");

        Assert.Contains("matrix(1 0 0 1 20 742)", page.Svg);
        Assert.Contains("matrix(1 0 0 1 20 756)", page.Svg);
        Assert.Contains("matrix(1 0 0 1 20 770)", page.Svg);
        Assert.Contains("matrix(1 0 0 1 20 784)", page.Svg);
        Assert.Contains("matrix(1 0 0 1 25 791)", page.Svg);
        Assert.Equal(new[] { "abcde" }, page.TextLines);
    }

    [Theory]
    [InlineData("1", "fill=\"none\" stroke=\"#000000\" stroke-width=\"1\"")]
    [InlineData("2", "fill=\"#000000\" stroke=\"#000000\" stroke-width=\"1\"")]
    [InlineData("3", "fill=\"#000000\" fill-opacity=\"0\"")]
    public void Rendering_modes_fill_stroke_or_hide_the_text(string mode, string expected)
    {
        Assert.Contains(expected, Render($"BT /F1 10 Tf {mode} Tr (x) Tj ET").Svg);
    }

    [Fact]
    public void Bold_italic_times_and_courier_fonts_pick_matching_families()
    {
        var page = Render("BT /F2 10 Tf (a) Tj /F3 10 Tf (b) Tj ET",
            "<< /Font << /F2 5 0 R /F3 6 0 R >> >>",
            (5, "<< /Type /Font /Subtype /Type1 /BaseFont /Times-BoldItalic /Encoding /WinAnsiEncoding >>"),
            (6, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"));

        Assert.Contains("font-family=\"'Times New Roman', Times, 'Liberation Serif', serif\" font-size=\"10\" font-weight=\"bold\" font-style=\"italic\"", page.Svg);
        Assert.Contains("font-family=\"'Courier New', Courier, 'Liberation Mono', monospace\"", page.Svg);
    }

    [Fact]
    public void Font_widths_in_the_font_dictionary_win_over_the_standard_widths()
    {
        var page = Render("BT /F2 10 Tf (AA) Tj ET", "<< /Font << /F2 5 0 R >> >>",
            (5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FirstChar 65 /Widths [400] >>"));

        Assert.Contains("x=\"0 4\"", page.Svg);
    }

    [Fact]
    public void WinAnsi_codes_become_their_unicode_characters()
    {
        var page = Render("BT /F1 10 Tf (\\200\\226\\351) Tj ET");

        Assert.Equal(new[] { "€–é" }, page.TextLines);
        Assert.Contains(">€–é</text>", page.Svg);
    }

    [Fact]
    public void Actual_text_replaces_the_glyph_text_in_the_layer()
    {
        var page = Render("BT /F1 10 Tf /Span << /ActualText <FEFF0041> >> BDC (zz) Tj EMC (b) Tj ET");

        Assert.Equal(new[] { "Ab" }, page.TextLines);
    }

    [Fact]
    public void Text_that_looks_like_markup_stays_text()
    {
        var page = Render("BT /F1 10 Tf (<script>alert\\(1\\)</script>&\") Tj ET");

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;&amp;&quot;</text>", page.Svg);
        Assert.DoesNotContain("<script", page.Svg);
    }

    [Theory]
    [InlineData("/F1 10 Tf (a) Tj", "outside a text object")]
    [InlineData("BT (a) Tj ET", "before it sets a font")]
    [InlineData("BT /F9 10 Tf ET", "font it doesn't have")]
    [InlineData("BT BT ET", "inside another")]
    [InlineData("BT /F1 10 Tf 7 Tr (a) Tj ET", "clips with text")]
    [InlineData("BT /F1 10 Tf [(a) /x] TJ ET", "TJ array holds")]
    public void Text_it_does_not_draw_leaves_the_page_undrawn(string content, string reason)
    {
        Assert.Contains(reason, Render(content).Reason);
    }

    [Theory]
    [InlineData("<< /Type /Font /Subtype /TrueType /BaseFont /Arial >>")]
    [InlineData("<< /Type /Font /Subtype /Type1 /BaseFont /Symbol >>")]
    [InlineData("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /B] >> >>")]
    [InlineData("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /MacRomanEncoding >>")]
    [InlineData("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FontDescriptor << /FontFile 9 0 R >> >>")]
    public void Fonts_it_does_not_draw_leave_the_page_undrawn(string font)
    {
        var page = Render("BT /F2 10 Tf (a) Tj ET", "<< /Font << /F2 5 0 R >> >>", (5, font));

        Assert.Contains("kind of font", page.Reason);
    }

    #endregion

    #region Private Methods

    private static PageRenderResult Render(string content, string? resources = null, params (int Number, string Dictionary)[] objects)
    {
        var extra = objects.Select(o => (o.Number, o.Dictionary.Trim().TrimStart('<').TrimEnd('>').Trim(), (byte[]?)null)).ToList();
        if (resources is null)
        {
            resources = "<< /Font << /F1 5 0 R >> >>";
            extra.Add((5, "/Type /Font /Subtype /Type1 /BaseFont /Helvetica", null));
        }
        var pdf = RenderTestFiles.OnePage(content, resources, extra.Select(e => (e.Item1, e.Item2.Length == 0 ? "<< >>" : $"<< {e.Item2} >>", e.Item3)));
        return RenderTestFiles.RenderFirst(pdf);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<string> StandardFontGoldens
    {
        get
        {
            var data = new TheoryData<string>();
            string folder = Path.GetDirectoryName(GoldenFiles.PathFor("invoice"))!;
            foreach (string file in Directory.GetFiles(folder, "*.pdf").OrderBy(f => f, StringComparer.Ordinal)) data.Add(file);
            return data;
        }
    }

    #endregion
}
