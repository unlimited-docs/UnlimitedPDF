using System.Globalization;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Tests.Fonts;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The right-to-left HTML to PDF templates (65-right-to-left-scripts CR-001, AC-24, V-24): the Arabic invoice and the
/// Hebrew letter draw through the sample with fallback fonts for their letters, with no warning and no "?"; they read
/// back in logical order with the English names and emails kept whole by <c>bdi</c>; their lines start at the right;
/// the page loads the included fonts only for HTML that has Arabic, Hebrew, or Thaana letters.
/// </summary>
public class RightToLeftTemplateTests
{
    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> Templates() => new[]
    {
        new object[] { "invoice" },
        new object[] { "letter" }
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Each_template_draws_with_fallback_fonts_and_reads_back_in_order(string template)
    {
        string html = Html(template);
        var (document, loaded) = await HtmlToPdfSample.BuildAsync(html, NoImages, [RightToLeftScriptTests.Font(RightToLeftLetters(html))]);

        Assert.Empty(loaded.Warnings);
        var pdf = PdfPageReader.SaveBytes(document);
        PdfStructureChecker.AssertValid(pdf);
        string text = string.Concat(RightToLeftScriptTests.PageText(pdf).Split('\n'));
        Assert.DoesNotContain("?", text, StringComparison.Ordinal);
        Assert.Contains("Northwind Traders Ltd.", text, StringComparison.Ordinal);
        Assert.Contains(template == "invoice" ? "billing@nakheel.example" : "orders@carmel-bakery.example", text, StringComparison.Ordinal);
        Assert.Contains(template == "invoice" ? "فاتورة ضريبية" : "הנדון: הצעת מחיר למוצרי מאפה", text, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 1", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Each_template_starts_its_lines_at_the_right(string template)
    {
        string html = Html(template);
        var (document, _) = await HtmlToPdfSample.BuildAsync(html, NoImages, [RightToLeftScriptTests.Font(RightToLeftLetters(html))]);
        var content = Assert.Single(PdfPageReader.Read(PdfPageReader.SaveBytes(document))).Content;

        // The first text drawn is the heading (invoice) or the sender's name (letter), both starting at the right margin.
        double first = Regex.Matches(content, @"1 0 (?:-?[\d.]+ )?1 (-?[\d.]+) -?[\d.]+ Tm").Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).First();
        Assert.True(first > 300, $"x = {first}");
    }

    [Fact]
    public void Only_html_with_right_to_left_letters_needs_the_included_fonts()
    {
        var rightToLeft = new[] { FontScript.Arabic, FontScript.Hebrew, FontScript.Thaana };

        Assert.Contains(FontScript.Arabic, FontSlots.Needed(HtmlTemplates.RightToLeftInvoice));
        Assert.Contains(FontScript.Hebrew, FontSlots.Needed(HtmlTemplates.RightToLeftLetter));
        foreach (var html in new[] { HtmlTemplates.Invoice, HtmlTemplates.Report, HtmlTemplates.Newsletter, HtmlTemplates.Letter, HtmlTemplates.Resume, HtmlTemplates.Statement() })
            Assert.DoesNotContain(FontSlots.Needed(html), rightToLeft.Contains);
    }

    [Fact]
    public void The_page_lists_both_templates_and_passes_the_fonts_to_the_sample()
    {
        string page = File.ReadAllText(Path.Combine(Root(), "UnlimitedPDF.Demos.BlazorWasm", "Pages", "Playground", "Html", "HtmlToPdf.razor"));

        Assert.Contains("[\"Right-to-left invoice (Arabic)\"] = () => HtmlTemplates.RightToLeftInvoice", page, StringComparison.Ordinal);
        Assert.Contains("[\"Right-to-left letter (Hebrew)\"] = () => HtmlTemplates.RightToLeftLetter", page, StringComparison.Ordinal);
        Assert.Contains("HtmlToPdfSample.BuildAsync(_html, Images.ResolveAsync, fallback)", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_listings_name_the_templates_and_the_footer_links_the_right_to_left_demo()
    {
        const string Description = "Pick a template, including an Arabic invoice and a Hebrew letter that read from the right, or paste your own HTML: no engine required, nothing uploaded.";
        Assert.Equal(Description, SampleCatalog.HtmlToPdf.Description);
        foreach (var file in new[] { "UnlimitedPDF.Demos.BlazorWasm/wwwroot/llms.txt", "UnlimitedPDF.Demos.BlazorWasm/wwwroot/sitemap.md" })
            Assert.Contains($"(https://unlimitedpdf.dev/playground/html/html-to-pdf): {Description}", File.ReadAllText(Path.Combine(Root(), file)), StringComparison.Ordinal);
        Assert.Contains("HTML to PDF: templates, including an Arabic invoice and a Hebrew letter that read from the right, or your own HTML", File.ReadAllText(Path.Combine(Root(), "README.md")), StringComparison.Ordinal);
        Assert.Contains(FooterLinks.Columns.Single(c => c.Title == "Languages").Links, link => link.Href == SampleCatalog.RightToLeftDocuments.Route && link.Text == "Right-to-left PDFs");
    }

    #endregion

    #region Private Methods

    private static string Html(string template) => template == "invoice" ? HtmlTemplates.RightToLeftInvoice : HtmlTemplates.RightToLeftLetter;

    private static Task<Stream?> NoImages(string reference, CancellationToken token) => Task.FromResult<Stream?>(null);

    // The template's characters past Latin-1: its Arabic or Hebrew letters, marks, and punctuation such as the Arabic comma.
    private static string RightToLeftLetters(string html) => new(html.Where(c => c > 0xFF).Distinct().ToArray());

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    #endregion

    #endregion
}
