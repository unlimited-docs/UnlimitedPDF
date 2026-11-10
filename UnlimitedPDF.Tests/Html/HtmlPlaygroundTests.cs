using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The playground's HTML to PDF category: every template builds a valid PDF with no warning through the sample code
/// the page shows, and the category sits after Split (AC-24, V-23).
/// </summary>
public class HtmlPlaygroundTests
{
    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> Templates() => new[]
    {
        new object[] { "invoice" },
        new object[] { "report" },
        new object[] { "newsletter" },
        new object[] { "letter" },
        new object[] { "resume" },
        new object[] { "statement" }
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Every_template_builds_through_the_sample(string template)
    {
        string html = template switch
        {
            "invoice" => HtmlTemplates.Invoice,
            "report" => HtmlTemplates.Report,
            "newsletter" => HtmlTemplates.Newsletter,
            "letter" => HtmlTemplates.Letter,
            "resume" => HtmlTemplates.Resume,
            _ => HtmlTemplates.Statement()
        };
        var (document, loaded) = await HtmlToPdfSample.BuildAsync(html, (reference, _) => Task.FromResult(HtmlGoldenTests.Images(reference)));
        Assert.Empty(loaded.Warnings);
        var pdf = PdfPageReader.SaveBytes(document);
        PdfStructureChecker.AssertValid(pdf);
        var pages = PdfPageReader.Read(pdf);
        Assert.All(pages.Select((page, i) => (page, i)), p => Assert.True(p.page.Shows($"Page {p.i + 1} of {pages.Count}")));
    }

    [Fact]
    public async Task Pasted_html_with_unsupported_parts_lists_its_warnings()
    {
        var (_, loaded) = await HtmlToPdfSample.BuildAsync("<div style='display:flex'>a</div><img src='photo.webp' alt='x'><script>x()</script>", (_, _) => Task.FromResult<Stream?>(null));
        Assert.Equal(new[] { PdfHtmlWarningKind.ResourceNotResolved, PdfHtmlWarningKind.UnsupportedElement, PdfHtmlWarningKind.UnsupportedValue },
            loaded.Warnings.Select(w => w.Kind).OrderBy(k => k.ToString(), StringComparer.Ordinal));
    }

    [Fact]
    public void The_catalog_lists_html_to_pdf_after_split()
    {
        var names = SampleCatalog.Categories.Select(c => c.Title).ToList();
        Assert.Equal(names.IndexOf("Split") + 1, names.IndexOf("HTML to PDF"));
        // The templates page first, then the known-problem scenarios (14-html-to-pdf AC-28).
        var demos = SampleCatalog.Categories.Single(c => c.Title == "HTML to PDF").Demos;
        Assert.Equal(new[] { "HTML to PDF", "Common problems" }, demos.Select(d => d.Title));
        Assert.Equal("Common HTML to PDF problems", demos[1].Heading);
        Assert.Equal(new[] { "playground/html/html-to-pdf", "playground/html/common-problems" }, demos.Select(d => d.Route));
        Assert.All(demos, d => Assert.Equal("HtmlToPdfSample.cs", d.SourceFile));
        Assert.Equal("common-html-to-pdf-problems.pdf", demos[1].FileName);
    }

    #endregion

    #endregion
}
