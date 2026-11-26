
namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The "HTML to PDF" example in docs/guides/html-to-pdf.md and nuget/README.md runs as written, the guide and the
/// package readme show the same section, and HTML to PDF is listed under 1.0.0 (AC-25, V-24, QF-26112603).
/// </summary>
public sealed class ReadmeHtmlExampleTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"readme-html-{Guid.NewGuid():N}");

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Readme_html_sample_runs_as_written()
    {
        // The readme's code, with its relative paths resolved under a temporary folder.
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "logo.png"), PdfHtmlLoadingTests.Png(8));
        File.WriteAllText(Path.Combine(_root, "invoice.html"), "<h1>Invoice</h1><img src='logo.png'><p>Total: €40</p>");
        string At(string path) => Path.Combine(_root, path);
        var output = new StringWriter();

        var invoice = PdfHtml.FromFile(At("invoice.html"));
        PdfOperations.ConvertHtml(invoice, At("invoice.pdf"));
        foreach (var warning in invoice.Warnings) output.WriteLine(warning); // what was skipped, if anything

        var document = new PdfDocument { MarginTop = 36, MarginBottom = 36, MarginLeft = 36, MarginRight = 36 };
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 0);
        document.Body.AddHtml(PdfHtml.FromString("<h1>Q3 Report</h1><p>Output rose <b>6%</b>.</p>"), headingBookmarks: true);
        document.Save(At("report.pdf"));

        var options = new PdfHtmlLoadOptions
        {
            ResourceResolver = reference => reference == "https://cdn.example.com/logo.png" ? File.OpenRead(At("logo.png")) : null
        };
        var banner = PdfHtml.FromString("<img src='https://cdn.example.com/logo.png'>", options);

        Assert.Empty(invoice.Warnings);
        Assert.Equal(string.Empty, output.ToString());
        PdfStructureChecker.AssertValid(File.ReadAllBytes(At("invoice.pdf")));
        PdfStructureChecker.AssertValid(File.ReadAllBytes(At("report.pdf")));
        Assert.Contains("/Outlines", Encoding.ASCII.GetString(File.ReadAllBytes(At("report.pdf"))));
        Assert.Single(banner.Model.Images);
    }

    [Fact]
    public void The_html_guide_matches_the_package_readme_and_html_is_listed_in_1_0_0()
    {
        string guide = RepositoryDocs.GuideBody("html-to-pdf.md");
        string nuget = RepositoryDocs.Read(Path.Combine("nuget", "README.md"));

        Assert.Equal(RepositoryDocs.WithoutLinkTargets(RepositoryDocs.SectionBody(nuget, "## HTML to PDF")), RepositoryDocs.WithoutLinkTargets(guide));
        Assert.Contains("PdfOperations.ConvertHtml(invoice, \"invoice.pdf\");", guide, StringComparison.Ordinal);
        Assert.Contains("never uses the network", guide, StringComparison.Ordinal);
        Assert.Contains("not as a browser shows it", guide, StringComparison.Ordinal);

        // Every scenario whose outcome is a fallback is listed as a known limitation, with the demo that shows it (AC-30).
        Assert.Contains("- Known limitations:", guide, StringComparison.Ordinal);
        Assert.Contains("Common problems demo", guide, StringComparison.Ordinal);
        foreach (string topic in new[] { "Flexbox and grid", "Floats and positioning", "Background images", "SVG and WebP", "Characters outside Western European", "`pre` lines don't wrap" })
            Assert.Contains(topic, guide, StringComparison.Ordinal);
        Assert.Contains("Common problems: HTML that often goes wrong in PDF", RepositoryDocs.Read(Path.Combine("docs", "guides", "README.md")), StringComparison.Ordinal);
        Assert.Contains("(docs/guides/html-to-pdf.md)", RepositoryDocs.Read("README.md"), StringComparison.Ordinal);

        foreach (string text in new[] { RepositoryDocs.Read("CHANGELOG.md"), nuget })
            Assert.Contains("- New: HTML to PDF (`PdfHtml`", text, StringComparison.Ordinal);
        Assert.True(nuget.IndexOf("## Split PDFs", StringComparison.Ordinal) < nuget.IndexOf("## HTML to PDF", StringComparison.Ordinal));
    }

    #endregion

    #endregion
}
