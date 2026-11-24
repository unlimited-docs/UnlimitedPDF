using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The Fonts and languages demo drawn with HTML (CR-002, AC-18, AC-19, AC-22; V-24, V-26, A05): every language's
/// invoice reads back exactly with the fonts registered under CSS family names, typed markup is drawn as written,
/// synthesis and its switch behave as stated, a variable font's heading weight is a real bold face, and missing
/// characters warn or fail the save.
/// </summary>
public class FontsAndLanguagesHtmlTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("hindi")]
    [InlineData("telugu")]
    [InlineData("tamil")]
    [InlineData("bengali")]
    [InlineData("gujarati")]
    [InlineData("punjabi")]
    [InlineData("odia")]
    [InlineData("kannada")]
    [InlineData("malayalam")]
    [InlineData("french")]
    [InlineData("mixed")]
    public void Each_language_reads_back_exactly_with_the_fonts_registered_as_families(string language)
    {
        var input = Input(language);
        var slots = DemoFonts.Slots(input);

        var (pdf, html) = Save(input, slots);

        PdfStructureChecker.AssertValid(pdf);
        string text = Text(pdf);
        var labels = input.Preset.Labels;
        foreach (string expected in new[] { labels.Title, labels.Seller, labels.Item, labels.Total, labels.Thanks, input.SellerName, input.BuyerAddress, input.Preset.Money(input.Total) }
            .Concat(input.Items.Select(i => i.Description)))
            Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.DoesNotContain("?", text, StringComparison.Ordinal);
        Assert.Empty(html.Warnings); // bold labels and headings are synthesized by default
        Assert.Equal(slots.Count, PdfInspector.Read(pdf).Fonts().Count);
    }

    [Fact]
    public void Typed_markup_is_drawn_as_written_and_adds_no_element()
    {
        var input = Input("french");
        input.SellerName = "<b>Bold</b> & \"Co\"";
        input.BuyerName = "</p><h1>Big</h1>";
        input.Items[0].Description = "<script>x</script> 'q'";
        // The test font lacks the markup characters; they are ASCII, so the built-in font draws them.
        var (pdf, html) = Save(input, DemoFonts.Slots(Input("french")));

        string text = Text(pdf);
        Assert.Contains("<b>Bold</b> & \"Co\"", text, StringComparison.Ordinal);
        Assert.Contains("</p><h1>Big</h1>", text, StringComparison.Ordinal);
        Assert.Contains("<script>x</script> 'q'", text, StringComparison.Ordinal);
        Assert.Empty(html.Warnings); // no script element, no unsupported element was ever parsed
    }

    [Fact]
    public void Without_synthesis_bold_text_warns_that_the_face_is_missing()
    {
        var input = Input("telugu");
        input.SynthesizeMissingStyles = false;

        var (pdf, html) = Save(input, DemoFonts.Slots(input));

        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.MissingFontFace && w.Source == "invoice telugu");
        Assert.DoesNotContain(" 2 Tr", Content(pdf), StringComparison.Ordinal);

        input.SynthesizeMissingStyles = true;
        var (synthesized, quiet) = Save(input, DemoFonts.Slots(input));
        Assert.Contains("2 Tr", Content(synthesized), StringComparison.Ordinal);
        Assert.Empty(quiet.Warnings);
    }

    [Fact]
    public void A_variable_font_draws_the_chosen_body_and_heading_weights()
    {
        // RV-08: the font is registered once and the weights come from the CSS. The test font's glyphs widen by 100
        // units from weight 400 to 900, so weights 500 and 900 differ by 80 (400 and 700, the old fixed weights, by 60).
        // HTML rounds a variable font's weights to the nearest hundred (RV-51).
        var input = Input("french");
        input.SynthesizeMissingStyles = false;
        input.BodyWeight = 500;
        input.HeadingWeight = 900;
        var slots = new List<(FontScript, PdfFont)> { (FontScript.Latin, PdfFont.FromBytes(DemoFonts.VariableBytes(input))) };

        var (pdf, html) = Save(input, slots);

        Assert.Empty(html.Warnings);
        var fonts = PdfInspector.Read(pdf).Fonts();
        Assert.Equal(2, fonts.Count);
        int glyph = fonts[0].Widths.Keys.Intersect(fonts[1].Widths.Keys).First(g => g != 0);
        var widths = fonts.Select(f => f.Widths[glyph]).Order().ToArray();
        Assert.Equal(80, widths[1] - widths[0], 1);
        Assert.DoesNotContain(" 2 Tr", Content(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_font_html_warns_for_each_missing_character_and_strict_saving_fails()
    {
        var input = Input("tamil");

        var (pdf, html) = Save(input, Array.Empty<(FontScript, PdfFont)>());

        Assert.Contains("?", Text(pdf), StringComparison.Ordinal);
        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.MissingCharacter && w.Source == "₹");

        input.FailOnMissingCharacters = true;
        var document = FontsAndLanguagesHtmlSample.Build(input, Array.Empty<(FontScript, PdfFont)>(), out _);
        var error = Assert.Throws<PdfTextException>(() => PdfPageReader.SaveBytes(document));
        Assert.Equal(1, error.PageNumber);
    }

    [Fact]
    public void Families_are_named_after_their_slots()
    {
        Assert.Equal("Invoice Devanagari", FontsAndLanguagesHtmlSample.Family(FontScript.Devanagari));
        Assert.Equal("Invoice Malayalam", FontsAndLanguagesHtmlSample.Family(FontScript.Malayalam));
        Assert.Equal(10, FontSlots.Scripts.Select(FontsAndLanguagesHtmlSample.Family).Distinct().Count());
    }

    #endregion

    #region Private Methods

    private static string Content(byte[] pdf) => string.Join("\n", PdfPageReader.Read(pdf).Select(p => p.Content));

    private static FontsAndLanguagesInput Input(string language)
    {
        var input = new FontsAndLanguagesInput { Mode = DrawMode.Html };
        input.Apply(LanguageInvoices.Find(language));
        return input;
    }

    private static (byte[] Pdf, PdfHtml Html) Save(FontsAndLanguagesInput input, IReadOnlyList<(FontScript, PdfFont)> slots)
    {
        var document = FontsAndLanguagesHtmlSample.Build(input, slots, out var html);
        return (PdfPageReader.SaveBytes(document), html);
    }

    // HTML draws each font's part of a line as its own text object, so the pieces are joined without separators.
    private static string Text(byte[] pdf)
    {
        var errors = new List<string>();
        string text = string.Concat(PdfInspector.Read(pdf).PageText(errors)).Replace("\n", string.Empty, StringComparison.Ordinal);
        Assert.Empty(errors);
        return text;
    }

    #endregion

    #endregion
}
