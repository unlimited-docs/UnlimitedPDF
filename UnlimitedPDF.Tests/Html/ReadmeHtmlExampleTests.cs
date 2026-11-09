using System.Runtime.CompilerServices;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The "HTML to PDF" example in README.md and nuget/README.md runs as written, and both readmes show the same section
/// after "Split PDFs" and list HTML to PDF under "Changes in 1.0.0" (AC-25, V-24).
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
    public void Both_readmes_show_the_same_html_section_and_list_it_in_1_0_0()
    {
        string readme = Section(File.ReadAllText(RepositoryFile("README.md")));
        string nuget = Section(File.ReadAllText(RepositoryFile(Path.Combine("nuget", "README.md"))));
        Assert.Equal(readme, nuget);
        Assert.Contains("PdfOperations.ConvertHtml(invoice, \"invoice.pdf\");", readme, StringComparison.Ordinal);
        Assert.Contains("never uses the network", readme, StringComparison.Ordinal);
        Assert.Contains("not as a browser shows it", readme, StringComparison.Ordinal);

        foreach (var file in new[] { "README.md", Path.Combine("nuget", "README.md") })
        {
            string text = File.ReadAllText(RepositoryFile(file));
            Assert.Contains("- New: HTML to PDF (`PdfHtml`", text, StringComparison.Ordinal);
            Assert.True(text.IndexOf("## Split PDFs", StringComparison.Ordinal) < text.IndexOf("## HTML to PDF", StringComparison.Ordinal), file);
        }
    }

    #endregion

    #region Private Methods

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!)!, relative);

    private static string Section(string text)
    {
        text = text.Replace("\r\n", "\n");
        int start = text.IndexOf("## HTML to PDF", StringComparison.Ordinal);
        int end = text.IndexOf("\n## ", start + 3, StringComparison.Ordinal);
        return text.Substring(start, end - start);
    }

    #endregion

    #endregion
}
