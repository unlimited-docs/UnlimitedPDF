using System.Globalization;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Caller fonts in HTML (WR-039, D-011, D-012, D-013, AC-03, AC-07, AC-10, AC-11, AC-12): CSS family names map to
/// registered faces by weight and style; each character takes the first listed family that has it, then the fallback
/// fonts, then the built-in fonts; a variable face draws each CSS weight; shaped widths place and break text, and a
/// word splits only between text elements; a character no font has is drawn as '?' with a warning.
/// </summary>
public class HtmlCallerFontTests
{
    #region Fields and Constants

    private const string Latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:";

    private static readonly Regex ShapedPosition = new(@"/E\d+ [\d.]+ Tf 1 0 0 1 ([-\d.]+) ([-\d.]+) Tm", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_registered_family_draws_html_text_with_the_caller_font()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin));

        var (pdf, html) = Render("<p style=\"font-family: 'Brand Sans'\">Hello world</p>", options);

        PdfStructureChecker.AssertValid(pdf);
        Assert.EndsWith("+BrandSans-Regular", Assert.Single(PdfInspector.Read(pdf).Fonts()).BaseFont);
        Assert.Equal("Hello world", Text(pdf));
        Assert.DoesNotContain("/HF", Content(pdf));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Family_names_match_without_regard_to_case_or_quotes()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("  \"Brand Sans\" ", Face("Brand Sans", Latin));

        var (pdf, _) = Render("<p style='font-family: BRAND sans'>Hello</p>", options);

        Assert.Single(PdfInspector.Read(pdf).Fonts());
    }

    [Fact]
    public void Bold_and_italic_text_takes_the_matching_faces()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin));
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin, weight: 700));
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin, italic: true));
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin, weight: 700, italic: true));

        var (pdf, _) = Render("<p style='font-family: Brand Sans'>Plain <b>Bold</b> <i>Italic</i> <b><i>Both</i></b></p>", options);

        var names = PdfInspector.Read(pdf).Fonts().Select(f => f.BaseFont.Substring(7)).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "BrandSans-Bold", "BrandSans-BoldItalic", "BrandSans-Italic", "BrandSans-Regular" }, names);
    }

    [Fact]
    public void A_missing_face_uses_the_nearest_weight_then_the_matching_style()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin));
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin, italic: true));

        var (bold, _) = Render("<p style='font-family: Brand Sans'><b>Bold</b></p>", options);
        var (boldItalic, _) = Render("<p style='font-family: Brand Sans'><b><i>Both</i></b></p>", options);

        Assert.EndsWith("BrandSans-Regular", Assert.Single(PdfInspector.Read(bold).Fonts()).BaseFont);
        Assert.EndsWith("BrandSans-Italic", Assert.Single(PdfInspector.Read(boldItalic).Fonts()).BaseFont);
    }

    [Fact]
    public void The_first_listed_family_that_has_a_character_draws_it()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("First", Face("First", "AB"));
        options.Fonts.Add("Second", Face("Second", "ABC"));

        var (pdf, _) = Render("<p style='font-family: Unknown, First, Second'>ABC</p>", options);

        var content = Content(pdf);
        var fonts = PdfInspector.Read(pdf).Fonts();
        Assert.Equal(2, fonts.Count);
        Assert.Equal("ABC", Text(pdf));
        Assert.True(content.IndexOf("/E1 ", StringComparison.Ordinal) < content.IndexOf("/E2 ", StringComparison.Ordinal)); // AB, then C
        Assert.EndsWith("+First-Regular", fonts.Single(f => f.Widths.Count == 2).BaseFont);
    }

    [Fact]
    public void Listed_families_come_first_then_the_fallback_fonts_then_the_built_in_font()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Deva", Face("Deva", "नमसते्"));
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin));
        var document = new PdfDocument();
        document.FontFallback.Add(Face("Symbols", "₹"));
        const string text = "नमस्ते Hello é ₹";

        var (pdf, html) = Render($"<p style=\"font-family: Missing, Deva, 'Brand Sans', serif\">{text}</p>", options, document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(new[] { "Brand", "Deva", "Symbols" }, PdfInspector.Read(pdf).Fonts().Select(f => f.BaseFont.Substring(7).Split('-')[0].Replace("Sans", string.Empty)).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Contains("/HF5 ", Content(pdf)); // é in the listed built-in family (serif: Times-Roman)
        Assert.Equal(text, Text(pdf));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Without_a_listed_family_the_built_in_font_draws_before_the_fallback_fonts()
    {
        var document = new PdfDocument();
        document.FontFallback.Add(Face("Everything", Latin + "తెలుగ"));

        var (plain, _) = Render("<p>Hello తెలుగు</p>", null, document);
        var (unknown, _) = Render("<p style='font-family: Unknown Sans'>Hello</p>", null, NewDocumentWith(document.FontFallback));

        Assert.Contains("/HF5 ", Content(plain)); // no font-family: the initial built-in family draws Latin
        Assert.Equal("Hello తెలుగు", Text(plain));
        Assert.DoesNotContain("/HF", Content(unknown)); // only an unknown name: the fallback font comes first
        Assert.Single(PdfInspector.Read(unknown).Fonts());
    }

    [Fact]
    public void Converting_html_uses_the_convert_options_fallback_fonts()
    {
        var telugu = Face("Telugu", "తెలుగ ");
        var html = PdfHtml.FromString("<p>తెలుగు</p>");
        using var output = new MemoryStream();

        PdfOperations.ConvertHtml(html, output, new PdfHtmlConvertOptions { FontFallback = { telugu } });

        Assert.Equal("తెలుగు", Text(output.ToArray()));
        Assert.Empty(html.Warnings);
        Assert.Throws<ArgumentNullException>(() => new PdfHtmlConvertOptions().FontFallback.Add(null!));
    }

    [Fact]
    public void A_variable_face_draws_each_css_weight_as_its_own_instance()
    {
        var builder = new TestFontBuilder("Flex") { WeightAxis = (100, 400, 900) };
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0), (0, 0), (100, 0), (0, 0), (0, 0) });
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Flex", PdfFont.FromBytes(builder.Build()));

        var (pdf, _) = Render("<div style='font-family: Flex'><p>A</p><p style='font-weight: 700'>A</p><p style='font-weight: 900'>A</p><p style='font-weight: 400'>A</p></div>", options);

        var widths = PdfInspector.Read(pdf).Fonts().Select(f => f.Widths[glyph]).OrderBy(w => w).ToList();
        Assert.Equal(new[] { 600.0, 660.0, 700.0 }, widths); // one face per weight; 400 is drawn twice with one face
    }

    [Fact]
    public void Shaped_widths_place_aligned_text()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin, advance: 900));
        double X(string align) => double.Parse(ShapedPosition.Match(Content(Render($"<p style='font-family: Brand Sans; text-align: {align}'>AAAA</p>", options).Pdf)).Groups[1].Value, CultureInfo.InvariantCulture);

        double left = X("left"), center = X("center"), right = X("right");

        // The free space is the body width (the A4 width less the 8 px HTML body margins) less the shaped width
        // (4 x 0.9 em at 12 pt); center takes half of it.
        Assert.Equal(right - left, 2 * (center - left), 2);
        Assert.Equal(595.28 - 16 * 0.75 - 4 * 0.9 * 12, right - left, 2);
    }

    [Fact]
    public void A_word_wider_than_the_line_splits_only_between_text_elements()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Deva", Face("Deva", "कि"));
        string word = string.Concat(Enumerable.Repeat("कि", 20));

        var (pdf, html) = Render($"<div style='font-family: Deva; width: 50px'>{word}</div>", options);

        var lines = Lines(pdf);
        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.StartsWith("क", line));
        Assert.Equal(word, string.Concat(lines));
        Assert.DoesNotContain(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped);
    }

    [Fact]
    public void A_built_in_font_word_splits_only_between_text_elements_too()
    {
        string word = string.Concat(Enumerable.Repeat("🙂", 40));

        var (pdf, _) = Render($"<div style='width: 40px'>{word}</div>");

        string drawn = string.Concat(Lines(pdf));
        Assert.Equal(new string('?', 40), drawn); // a surrogate pair is never cut into two '?'
    }

    [Fact]
    public void Letter_spacing_goes_after_each_cluster_of_caller_font_text()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin));

        var (pdf, _) = Render("<p style='font-family: Brand Sans; letter-spacing: 2pt'>ABC</p>", options);

        Assert.Contains("-166.667 <", Content(pdf)); // 2 pt at 12 pt is 166.667 thousandths of an em
    }

    [Fact]
    public void A_character_no_font_has_is_drawn_as_a_question_mark_and_reported_once()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", Face("Brand Sans", Latin));

        var (pdf, html) = Render("<p style='font-family: Brand Sans'>Hi 🙏 and 🙏</p>", options);

        var warning = Assert.Single(html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.MissingCharacter, warning.Kind);
        Assert.Equal("🙏", warning.Source);
        Assert.Contains("U+1F64F", warning.Message);
        Assert.Equal("Hi ? and ?", Text(pdf));
    }

    [Fact]
    public void A_missing_character_fails_a_strict_load_or_save()
    {
        var strict = new PdfHtmlLoadOptions { FailOnWarning = true };
        var document = new PdfDocument();
        document.Body.AddHtml(PdfHtml.FromString("<p>తెలుగు</p>", strict));
        Assert.Equal(PdfHtmlWarningKind.MissingCharacter, Assert.Throws<PdfHtmlException>(() => PdfPageReader.SaveBytes(document)).Warning!.Kind);

        var failing = new PdfDocument { FailOnMissingCharacters = true };
        failing.Body.AddHtml(PdfHtml.FromString("<p>Name: తెలుగు</p>"));
        var error = Assert.Throws<PdfTextException>(() => PdfPageReader.SaveBytes(failing));
        Assert.Equal("తె", error.Character);
        Assert.Equal(1, error.PageNumber);
    }

    [Fact]
    public void The_font_collection_checks_its_arguments_and_ignores_a_face_added_twice()
    {
        var fonts = new PdfHtmlLoadOptions().Fonts;
        var face = Face("Brand Sans", "A");

        Assert.Throws<ArgumentNullException>(() => fonts.Add(null!, face));
        Assert.Throws<ArgumentNullException>(() => fonts.Add("Brand", null!));
        Assert.Throws<ArgumentException>(() => fonts.Add(" '' ", face));
        fonts.Add("Brand", face);
        fonts.Add("BRAND", face);
        fonts.Add("Other", face);

        Assert.Equal(2, fonts.Count);
        Assert.Equal(new[] { "brand", "other" }, fonts.Select(f => f.Key));
    }

    #endregion

    #region Private Methods

    private static string Content(byte[] pdf) => string.Join("\n", PdfPageReader.Read(pdf).Select(p => p.Content));

    // A face with one box glyph per character, all with one advance.
    private static PdfFont Face(string family, string characters, ushort weight = 400, bool italic = false, ushort advance = 600)
    {
        var builder = new TestFontBuilder(family) { Weight = weight, Italic = italic };
        builder.MapCharacters(characters, advance);
        return PdfFont.FromBytes(builder.Build());
    }

    private static PdfDocument NewDocumentWith(IEnumerable<PdfFont> fallback)
    {
        var document = new PdfDocument();
        foreach (var font in fallback) document.FontFallback.Add(font);
        return document;
    }

    private static (byte[] Pdf, PdfHtml Html) Render(string markup, PdfHtmlLoadOptions? options = null, PdfDocument? document = null)
    {
        var html = PdfHtml.FromString(markup, options);
        document ??= new PdfDocument();
        document.Body.AddHtml(html);
        return (PdfPageReader.SaveBytes(document), html);
    }

    private static string Text(byte[] pdf) => string.Concat(Lines(pdf));

    // The page text as the text objects read back: one entry per text object, empty ones dropped.
    private static List<string> Lines(byte[] pdf)
    {
        var errors = new List<string>();
        var pages = PdfInspector.Read(pdf).PageText(errors);
        Assert.Empty(errors);
        return pages.SelectMany(p => p.Split('\n')).Where(l => l.Length > 0).ToList();
    }

    #endregion

    #endregion
}
