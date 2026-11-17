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
        Assert.Contains("font-size=\"12\" font-variant=\"no-common-ligatures no-discretionary-ligatures\" x=\"0 8.004\" y=\"0\" fill=\"#000000\">AB</text>", page.Svg);
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
        var page = Render("BT /F2 10 Tf (\\200\\226\\351) Tj ET", "<< /Font << /F2 5 0 R >> >>",
            (5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));

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
    public void Actual_text_around_a_text_object_gives_its_line_once()
    {
        var around = Render("/Span << /ActualText <FEFF00660069> >> BDC BT /F1 10 Tf (x) Tj ET EMC");
        var aroundTwo = Render("/Span << /ActualText <FEFF00660069> >> BDC BT /F1 10 Tf (x) Tj ET BT (y) Tj ET EMC");
        var alone = Render("/Span << /ActualText <FEFF00660069> >> BDC EMC");

        Assert.Equal(new[] { "fi" }, around.TextLines);
        Assert.Equal(new[] { "fi", "" }, aroundTwo.TextLines);
        Assert.Equal(new[] { "fi" }, alone.TextLines);
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

    [Fact]
    public void A_font_without_an_encoding_uses_its_standard_encoding()
    {
        var page = Render("BT /F1 10 Tf (it's `x') Tj ET");

        // Quoteright and quoteleft replace the ASCII quotes, with their own widths (222 in Helvetica).
        Assert.Equal(new[] { "it\u2019s \u2018x\u2019" }, page.TextLines);
        Assert.Contains("x=\"0 2.22 5 7.22 12.22 15 17.22 22.22\"", page.Svg);
        Assert.Contains("doesn't define", Render("BT /F1 10 Tf (\\341) Tj ET").Reason);
    }

    [Fact]
    public void A_negative_font_size_turns_the_text_but_keeps_its_advance()
    {
        var page = Render("BT /F1 -10 Tf 100 700 Td (AB) Tj (C) Tj ET");

        // The frame mirrors the text; positions in it are unsigned, so B sits 6.67 to the left of A after the turn.
        Assert.Contains("transform=\"matrix(-1 0 0 -1 100 142)\"", page.Svg);
        Assert.Contains("x=\"0 6.67\"", page.Svg);
        Assert.Contains("transform=\"matrix(-1 0 0 -1 86.66 142)\"", page.Svg); // C after -6.67 - 6.67
    }

    [Fact]
    public void Control_codes_are_refused_and_layout_characters_in_actual_text_become_spaces()
    {
        var page = Render("BT /F2 10 Tf (A\\001B) Tj ET", "<< /Font << /F2 5 0 R >> >>",
            (5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        Assert.Contains("control code", page.Reason);

        // An /ActualText with a tab, a line break, and a control code stays one line of text.
        var spans = Render("BT /F1 10 Tf /Span << /ActualText <FEFF0041000900420001000A0043> >> BDC (z) Tj EMC ET");
        Assert.Equal(new[] { "A B C" }, spans.TextLines);
    }

    [Fact]
    public void The_double_quote_operator_sets_word_then_character_spacing()
    {
        var page = Render("BT /F1 10 Tf 12 TL 0 100 Td 5 1 ( ab) \" ET");

        // Space: 2.78 + 1 (Tc) + 5 (Tw) = 8.78 before the a; a: 5.56 + 1 (Tc only) = 6.56 before the b.
        Assert.Contains("x=\"0 8.78 15.34\"", page.Svg);
    }

    [Fact]
    public void TD_sets_the_leading_that_T_star_uses()
    {
        var page = Render("BT /F1 10 Tf 0 100 Td 0 -20 TD T* (a) Tj ET");

        Assert.Contains("matrix(1 0 0 1 0 782)", page.Svg); // 100 - 20 - 20 = 60 from the bottom
    }

    [Fact]
    public void Each_text_object_starts_at_the_origin()
    {
        var page = Render("BT /F1 10 Tf 50 50 Td ET BT (a) Tj ET");

        Assert.Contains("matrix(1 0 0 1 0 842)", page.Svg);
    }

    [Fact]
    public void Actual_text_named_in_the_page_properties_reaches_the_layer()
    {
        var page = Render("BT /F1 10 Tf /Span /P1 BDC (zz) Tj EMC ET",
            "<< /Font << /F1 5 0 R >> /Properties << /P1 << /ActualText <FEFF0058> >> >> >>",
            (5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        Assert.Equal(new[] { "X" }, page.TextLines);
    }

    [Fact]
    public void The_text_layer_follows_the_text_objects_like_the_pdf_text()
    {
        Assert.Equal(new[] { "", "a" }, Render("BT /F1 10 Tf ET /Span << /ActualText <FEFF0058> >> BDC EMC BT /F1 10 Tf (a) Tj ET").TextLines);
        Assert.Equal(new[] { "a", "X" }, Render("BT /F1 10 Tf (a) Tj ET /Span << /ActualText <FEFF0058> >> BDC EMC").TextLines);
        Assert.Equal(new[] { "open" }, Render("BT /F1 10 Tf (open) Tj").TextLines);
    }

    [Fact]
    public void Text_strokes_scale_with_the_text_matrix_and_keep_a_hairline_for_zero_width()
    {
        Assert.Contains("stroke-width=\"0.5\"", Render("BT /F1 10 Tf 1 Tr 2 0 0 2 0 0 Tm (a) Tj ET").Svg);
        Assert.Contains("stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"", Render("BT /F1 10 Tf 1 Tr 0 w (a) Tj ET").Svg);
        Assert.Contains("stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"", Render("BT /F1 10 Tf 2 Tr -2 w (a) Tj ET").Svg);
    }

    [Fact]
    public void Text_and_actual_text_count_toward_the_output_bound()
    {
        var tight = PageRenderLimits.Default with { MaxOutputCharacters = 5000 };
        string big = new('a', 6000);
        Assert.Contains("too large", Render($"BT /F1 10 Tf ({big}) Tj ET", null, tight).Reason);

        string actual = "FEFF" + string.Concat(Enumerable.Repeat("0041", 1000));
        string spans = string.Concat(Enumerable.Repeat("/Span /P1 BDC EMC ", 10));
        var page = Render(spans, $"<< /Properties << /P1 << /ActualText <{actual}> >> >> >>", tight);
        Assert.Matches("too large|too much /ActualText", page.Reason);

        string tooLong = "FEFF" + string.Concat(Enumerable.Repeat("0041", 5000));
        Assert.Contains("too long", Render($"/Span << /ActualText <{tooLong}> >> BDC EMC").Reason);
    }

    [Fact]
    public void Marked_content_nesting_is_bounded()
    {
        Assert.Contains("marked content too deeply", Render(string.Concat(Enumerable.Repeat("/P BMC ", 200))).Reason);
    }

    [Theory]
    [InlineData("ET", "outside a text object")]
    [InlineData("BT /F1 10 Tf 9 Tr ET", "doesn't exist")]
    public void Stray_text_operators_and_unknown_modes_are_refused(string content, string reason)
    {
        Assert.Contains(reason, Render(content).Reason);
    }

    [Fact]
    public void The_glyph_bound_refuses_a_huge_string_before_reading_it()
    {
        var limits = PageRenderLimits.Default with { MaxGlyphs = 100 };

        Assert.Contains("too many glyphs", Render("BT /F1 10 Tf (" + new string('a', 101) + ") Tj ET", null, limits).Reason);
        Assert.True(Render("BT /F1 10 Tf (" + new string('a', 100) + ") Tj ET", null, limits).IsDrawn);
    }

    [Fact]
    public void Finished_lines_count_toward_the_output_bound()
    {
        var limits = PageRenderLimits.Default with { MaxOutputCharacters = 40_000 };
        string lines = string.Concat(Enumerable.Repeat("BT /F1 10 Tf 3 Tr (" + new string('a', 1000) + ") Tj ET ", 30));

        Assert.Contains("too large", Render(lines, null, limits).Reason);
    }

    [Fact]
    public void Actual_text_across_the_page_is_bounded_even_when_it_is_cleared()
    {
        var limits = PageRenderLimits.Default with { MaxOutputCharacters = 50_000 };
        string actual = "FEFF" + string.Concat(Enumerable.Repeat("0041", 1000));
        string repeats = string.Concat(Enumerable.Repeat("/Span /P1 BDC EMC BT ET ", 60));

        var page = Render(repeats, $"<< /Properties << /P1 << /ActualText <{actual}> >> >> >>", limits);

        Assert.Contains("too much /ActualText", page.Reason);
    }

    [Fact]
    public void A_tj_number_that_is_not_finite_has_its_own_reason()
    {
        Assert.Contains("isn't finite", Render("BT /F1 10 Tf [(a) 1" + new string('0', 400) + "] TJ ET").Reason);
    }

    [Fact]
    public void Bold_fonts_without_an_encoding_use_their_own_quote_width()
    {
        var page = Render("BT /F2 10 Tf ('a) Tj ET", "<< /Font << /F2 5 0 R >> >>",
            (5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));

        Assert.Contains("x=\"0 2.78\"", page.Svg);
    }

    #endregion

    #region Private Methods

    private static PageRenderResult Render(string content, string? resources = null, params (int Number, string Dictionary)[] objects) =>
        Render(content, resources, null, objects);

    private static PageRenderResult Render(string content, string? resources, PageRenderLimits? limits, params (int Number, string Dictionary)[] objects)
    {
        var extra = objects.Select(o => (o.Number, o.Dictionary.Trim().TrimStart('<').TrimEnd('>').Trim(), (byte[]?)null)).ToList();
        if (resources is null)
        {
            resources = "<< /Font << /F1 5 0 R >> >>";
            extra.Add((5, "/Type /Font /Subtype /Type1 /BaseFont /Helvetica", null));
        }
        var pdf = RenderTestFiles.OnePage(content, resources, extra.Select(e => (e.Item1, e.Item2.Length == 0 ? "<< >>" : $"<< {e.Item2} >>", e.Item3)));
        return RenderTestFiles.RenderFirst(pdf, limits);
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
