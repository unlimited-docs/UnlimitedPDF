using UnlimitedPDF.Demo.ConsoleApp;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The console demo converts its sample invoice HTML with its logo, with no warning and no machine path (AC-23, V-22).
/// </summary>
public class HtmlDemoTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_demo_converts_the_sample_invoice_with_its_logo()
    {
        string output = Path.Combine(Path.GetTempPath(), "updf-html-demo-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var html = HtmlDemo.Write(Fixtures.DemoAssets(), output);
            Assert.Empty(html.Warnings);
            var pdf = File.ReadAllBytes(output);
            PdfStructureChecker.AssertValid(pdf);
            var page = Assert.Single(PdfPageReader.Read(pdf));
            Assert.Contains("Do Q", page.Content);
            Assert.Contains(HtmlLayoutTests.Runs(page), r => r.Text == "INVOICE");
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void The_sample_html_file_matches_the_playground_template()
    {
        string file = File.ReadAllText(Path.Combine(Fixtures.DemoAssets(), HtmlDemo.FileName)).Replace("\r\n", "\n").Trim();
        Assert.Equal(HtmlTemplates.Invoice.Replace("\r\n", "\n").Trim(), file);
    }

    [Fact]
    public void The_demo_program_writes_next_to_the_sample()
    {
        string program = File.ReadAllText(Path.Combine(Fixtures.DemoAssets(), "..", "Program.cs"));
        Assert.Contains("HtmlDemo.Write(assetsDirectory, htmlFilePath)", program);
        Assert.Contains("Path.Combine(outputDirectory,", program);
    }

    #endregion

    #endregion
}
