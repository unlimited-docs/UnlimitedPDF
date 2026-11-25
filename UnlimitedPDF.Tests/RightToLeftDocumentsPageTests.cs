using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Tests.Fonts;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The Right-to-left documents demo (65-right-to-left-scripts CR-001; V-20, V-21, V-22, V-23): it sits in the Languages
/// category after Fonts and languages with a route, title, description, and icon of its own; it offers exactly the five
/// right-to-left invoices with right to left first; its page has the direction switch, a language query parameter, and
/// fields marked right to left; every invoice reads back in both modes and all three directions.
/// </summary>
public class RightToLeftDocumentsPageTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_demo_follows_fonts_and_languages_in_the_languages_category()
    {
        var demo = SampleCatalog.RightToLeftDocuments;
        var languages = SampleCatalog.Categories.Single(c => c.Id == "languages");

        Assert.Equal(new[] { SampleCatalog.FontsAndLanguages, demo }, languages.Demos);
        Assert.Equal("Right-to-left documents", demo.Title);
        Assert.Equal("playground/languages/right-to-left-documents", demo.Route);
        Assert.Equal("direction", demo.Icon);
        Assert.True(demo.DocumentTitle.Length <= 60, demo.DocumentTitle);
        Assert.True(demo.Description.Length <= 160, demo.Description);
        Assert.Contains(demo, ScenarioGallery.Scenarios);
    }

    [Fact]
    public void The_demo_offers_the_five_right_to_left_invoices()
    {
        Assert.Equal(new[] { "arabic", "persian", "urdu", "hebrew", "dhivehi" }, LanguageInvoices.RightToLeft.Select(p => p.Id));
    }

    [Fact]
    public void The_page_has_the_direction_switch_and_fields_marked_right_to_left()
    {
        string page = File.ReadAllText(Path.Combine(BidiRoot(), "UnlimitedPDF.Demos.BlazorWasm", "Pages", "Playground", "Languages", "RightToLeftDocuments.razor"));

        Assert.Contains("@page \"/playground/languages/right-to-left-documents\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Direction\"", page, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"language\")]", page, StringComparison.Ordinal);
        Assert.Contains("(PdfTextDirection.RightToLeft, \"Right to left\")", page, StringComparison.Ordinal);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(page, "dir=\"@FieldDirection\"").Count);
    }

    public static IEnumerable<object[]> Cases() =>
        from preset in new[] { "arabic", "persian", "urdu", "hebrew", "dhivehi" }
        from direction in new[] { PdfTextDirection.RightToLeft, PdfTextDirection.Auto, PdfTextDirection.LeftToRight }
        from mode in new[] { DrawMode.TextCalls, DrawMode.Html }
        select new object[] { preset, direction, mode };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_invoice_reads_back_in_each_direction_and_mode(string preset, PdfTextDirection direction, DrawMode mode)
    {
        var input = new FontsAndLanguagesInput { Direction = direction, Mode = mode };
        input.Apply(LanguageInvoices.Find(preset));

        var document = mode == DrawMode.Html
            ? FontsAndLanguagesHtmlSample.Build(input, DemoFonts.Slots(input), out _)
            : FontsAndLanguagesSample.Build(input, DemoFonts.For(input), DemoFonts.For(input));
        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        string text = string.Concat(RightToLeftScriptTests.PageText(pdf).Split('\n'));
        Assert.Contains(input.Preset.Labels.Title, text, StringComparison.Ordinal);
        Assert.Contains(input.SellerName, text, StringComparison.Ordinal);
        Assert.DoesNotContain("?", text, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string BidiRoot([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));

    #endregion

    #endregion
}
