using System.Globalization;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The Fonts and languages demo drawn with the text calls (CR-002, AC-18, AC-19, AC-21, AC-22; V-24, V-25, V-26): every
/// language's invoice reads back exactly with generated fonts, edits and totals show, empty slots give '?' and the
/// missing list, failing on missing characters stops the save, and a variable font draws headings at their weight.
/// </summary>
public class FontsAndLanguagesTextCallsTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("hindi")]
    [InlineData("telugu")]
    [InlineData("tamil")]
    [InlineData("french")]
    [InlineData("mixed")]
    public void Each_language_reads_back_exactly_with_a_font_for_each_script_it_needs(string language)
    {
        var input = Input(language);
        var fonts = DemoFonts.For(input);

        var pdf = Save(FontsAndLanguagesSample.Build(input, fonts, fonts));

        PdfStructureChecker.AssertValid(pdf);
        string text = PageText(pdf);
        foreach (string expected in Expected(input)) Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.DoesNotContain("?", text, StringComparison.Ordinal);
        Assert.Equal(fonts.Count, PdfInspector.Read(pdf).Fonts().Count);
        Assert.Empty(FontsAndLanguagesSample.MissingCharacters(input, fonts));
    }

    [Fact]
    public void Edited_fields_and_items_are_drawn_with_the_computed_total()
    {
        var input = Input("telugu");
        input.SellerName = "కొత్త దుకాణం";
        input.Items.RemoveAt(0);
        input.Items.Add(new LanguageInvoiceLine { Description = "పుస్తకం", Quantity = 3, Rate = 120m });
        var fonts = DemoFonts.For(input);

        string text = PageText(Save(FontsAndLanguagesSample.Build(input, fonts, fonts)));

        Assert.Contains("కొత్త దుకాణం", text, StringComparison.Ordinal);
        Assert.Contains("పుస్తకం", text, StringComparison.Ordinal);
        Assert.Contains("₹ 360.00", text, StringComparison.Ordinal);
        Assert.Contains(input.Preset.Money(800m + 1350m + 360m), text, StringComparison.Ordinal);
        Assert.DoesNotContain("కాటన్ చీర", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_telugu_font_the_invoice_still_generates_with_question_marks_and_the_missing_list()
    {
        var input = Input("telugu");

        var pdf = Save(FontsAndLanguagesSample.Build(input, Array.Empty<PdfFont>(), Array.Empty<PdfFont>()));
        var missing = FontsAndLanguagesSample.MissingCharacters(input, Array.Empty<PdfFont>());

        PdfStructureChecker.AssertValid(pdf);
        Assert.Contains("?", PageText(pdf), StringComparison.Ordinal);
        Assert.Contains("₹", missing);
        Assert.All(missing, m => Assert.True(m == "₹" || FontSlots.ScriptOf(m[0]) == FontScript.Telugu, m));
        Assert.Equal(missing.Count, missing.Distinct().Count());

        // With a Latin font that has ₹ (the mixed invoice's, which prices in rupees), only the Telugu text is missing.
        var latinOnly = DemoFonts.For(Input("mixed")).Take(1).ToList();
        Assert.DoesNotContain("₹", FontsAndLanguagesSample.MissingCharacters(input, latinOnly));
    }

    [Fact]
    public void Failing_on_missing_characters_stops_the_save_with_the_character_and_page()
    {
        var input = Input("tamil");
        input.FailOnMissingCharacters = true;
        var document = FontsAndLanguagesSample.Build(input, Array.Empty<PdfFont>(), Array.Empty<PdfFont>());

        var error = Assert.Throws<PdfTextException>(() => Save(document));

        Assert.Equal(1, error.PageNumber);
        Assert.Contains(error.Character, FontsAndLanguagesSample.MissingCharacters(input, Array.Empty<PdfFont>()));
        Assert.Contains(error.Character, error.Message, StringComparison.Ordinal);

        // With the fonts it needs, the strict save passes.
        var fonts = DemoFonts.For(input);
        PdfStructureChecker.AssertValid(Save(FontsAndLanguagesSample.Build(input, fonts, fonts)));
    }

    [Theory]
    [InlineData("french")]
    [InlineData("telugu")]
    public void A_variable_font_draws_headings_at_the_heading_weight(string language)
    {
        var input = Input(language);
        byte[] bytes = DemoFonts.VariableBytes(input);
        var body = new[] { PdfFont.FromBytes(bytes) };
        var headings = new[] { PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 700 }) };

        var pdf = Save(FontsAndLanguagesSample.Build(input, body, headings));

        var faces = PdfInspector.Read(pdf).Fonts();
        Assert.Equal(2, faces.Count);
        int glyph = faces[0].Widths.Keys.Intersect(faces[1].Widths.Keys).First();
        Assert.NotEqual(faces[0].Widths[glyph], faces[1].Widths[glyph]);
        Assert.Contains(input.Preset.Labels.Title, PageText(pdf), StringComparison.Ordinal);

        // A font with one weight draws everything with one face.
        var still = DemoFonts.For(input);
        Assert.Single(PdfInspector.Read(Save(FontsAndLanguagesSample.Build(input, still, still))).Fonts());
    }

    #endregion

    #region Private Methods

    private static IEnumerable<string> Expected(FontsAndLanguagesInput input)
    {
        var labels = input.Preset.Labels;
        yield return labels.Title;
        yield return labels.Seller;
        yield return labels.Buyer;
        yield return labels.Item;
        yield return labels.Total;
        yield return labels.Thanks;
        yield return input.SellerName;
        yield return input.BuyerAddress;
        yield return $"{labels.Number}: {input.InvoiceNumber}";
        yield return input.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        foreach (var line in input.Items)
        {
            yield return line.Description;
            yield return input.Preset.Money(line.Amount);
        }
        yield return input.Preset.Money(input.Total);
    }

    private static FontsAndLanguagesInput Input(string language)
    {
        var input = new FontsAndLanguagesInput();
        input.Apply(LanguageInvoices.Find(language));
        return input;
    }

    private static string PageText(byte[] pdf)
    {
        var errors = new List<string>();
        string text = string.Join("\n", PdfInspector.Read(pdf).PageText(errors));
        Assert.Empty(errors);
        return text;
    }

    private static byte[] Save(PdfDocument document) => PdfPageReader.SaveBytes(document);

    #endregion

    #endregion
}

/// <summary>
/// Generated fonts for the Fonts and languages demo tests (PR-011): one per script the invoice needs, in slot order,
/// each holding that script's characters plus the shared ones (digits, punctuation, ₹, €), as a visitor would fill
/// the needed slots.
/// </summary>
internal static class DemoFonts
{
    #region Methods

    #region Public Methods

    public static List<PdfFont> For(FontsAndLanguagesInput input) => Slots(input).Select(s => s.Font).ToList();

    // The filled slots for the HTML sample: each needed script with its font, which is also its heading font.
    public static List<(FontScript Script, PdfFont Font, PdfFont HeadingFont)> Slots(FontsAndLanguagesInput input) =>
        FontSlots.Needed(Text(input)).Select(script =>
        {
            var font = PdfFont.FromBytes(Build(input, script, variable: false));
            return (script, font, font);
        }).ToList();

    // One variable font holding every character the invoice draws; each glyph widens by 100 units at the axis maximum.
    public static byte[] VariableBytes(FontsAndLanguagesInput input) => Build(input, null, variable: true);

    #endregion

    #region Private Methods

    private static byte[] Build(FontsAndLanguagesInput input, FontScript? script, bool variable)
    {
        string text = Text(input);
        string characters = new(text.Where(c => script is null || FontSlots.ScriptOf(c) is null || FontSlots.ScriptOf(c) == script).Distinct().ToArray());
        var builder = new TestFontBuilder($"Demo {script?.ToString() ?? "All"}");
        if (variable) builder.WeightAxis = (100, 400, 900);
        builder.MapCharacters(characters);
        if (variable)
        {
            for (int glyph = 1; glyph < builder.GlyphCount; glyph++)
                builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0), (0, 0), (100, 0), (0, 0), (0, 0) });
        }
        return builder.Build();
    }

    // Every character the invoice draws: labels, fields, numbers, dates, money, and the footer's page numbers.
    private static string Text(FontsAndLanguagesInput input)
    {
        var preset = input.Preset;
        var money = string.Concat(input.Items.Select(i => preset.Money(i.Rate) + preset.Money(i.Amount))) + preset.Money(input.Total);
        return input.AllText() + input.InvoiceNumber + input.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + money + ": 0123456789/";
    }

    #endregion

    #endregion
}
