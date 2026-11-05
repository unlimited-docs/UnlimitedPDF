using System.Text.RegularExpressions;
using UnlimitedPDF.Demo.ConsoleApp;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The document demos (Invoice, Receipt, Medical report, Employee directory, console demo) are built on header and
/// footer sections and the body flow (WR-031, WR-033): with default data and with many rows, no content overlaps a
/// table, and every page shows its header, its footer, and "Page n of m".
/// </summary>
public class DemoPaginationTests
{
    #region Fields and Constants

    private const double Tolerance = 1e-6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Default_console_demo_keeps_its_three_pages()
    {
        var pages = PdfPageReader.Save(DemoDocument.Build(Fixtures.DemoAssets()));

        Assert.Equal(3, pages.Count);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.True(pages[0].Shows("INVOICE"));
    }

    [Fact]
    public void Default_invoice_fits_one_page()
    {
        var pages = PdfPageReader.Save(InvoiceSample.Build(new InvoiceInput(), Images()));

        Assert.Single(pages);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
    }

    [Fact]
    public void Long_invoice_continues_with_its_header_and_keeps_the_totals_together()
    {
        var input = new InvoiceInput();
        for (int i = 0; i < 60; i++) input.Items.Add(new InvoiceLine { Description = "Cable " + i, Quantity = 1, Rate = 10 });

        var pages = PdfPageReader.Save(InvoiceSample.Build(input, Images()));

        Assert.True(pages.Count >= 2);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.All(pages, page => Assert.True(page.Shows("INVOICE")));
        Assert.All(pages, page => Assert.True(page.Shows("Item Description")));
        var last = pages[pages.Count - 1];
        Assert.True(last.Shows("Total"));
        Assert.True(last.Shows("2. Goods once sold will not be taken back."));
    }

    [Fact]
    public void Default_medical_report_fits_one_page()
    {
        var pages = PdfPageReader.Save(MedicalReportSample.Build(new MedicalReportInput(), Images()));

        Assert.Single(pages);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
    }

    [Fact]
    public void Long_medical_report_repeats_its_results_header_on_every_page()
    {
        var input = new MedicalReportInput();
        for (int i = 0; i < 80; i++) input.Results.Add(new LabResult { Test = "Marker " + i, Result = "5", Unit = "mg/dL", Low = 1, High = 9 });

        var pages = PdfPageReader.Save(MedicalReportSample.Build(input, Images()));

        Assert.True(pages.Count >= 2);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.All(pages, page => Assert.True(page.Shows("Reference range")));
        Assert.True(pages[pages.Count - 1].Shows("Results should be interpreted by a qualified clinician."));
    }

    [Fact]
    public void Default_receipt_fits_one_page()
    {
        var pages = PdfPageReader.Save(ReceiptSample.Build(new ReceiptInput(), Images()));

        Assert.Single(pages);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
    }

    [Fact]
    public void Long_receipt_continues_on_more_A6_pages()
    {
        var input = new ReceiptInput();
        for (int i = 0; i < 30; i++) input.Items.Add(new ReceiptLine { Name = "Snack " + i, Quantity = 1, Price = 1.5m });

        var pages = PdfPageReader.Save(ReceiptSample.Build(input, Images()));

        Assert.True(pages.Count >= 2);
        Assert.All(pages, page => Assert.Equal(pages[0].MediaBox, page.MediaBox));
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.All(pages, page => Assert.True(page.Shows("Thank you for shopping with us!")));
        Assert.Contains(pages, page => page.Shows("Paid by: Card"));
    }

    [Fact]
    public void Employee_directory_numbers_every_page()
    {
        var pages = PdfPageReader.Save(EmployeeDirectorySample.Build(new EmployeeDirectoryInput { EmployeeCount = 300 }));

        Assert.True(pages.Count >= 3);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.All(pages, page => Assert.True(page.Shows("Employee directory")));
        Assert.True(pages[pages.Count - 1].Shows("300"));
    }

    [Fact]
    public void Headers_and_footers_sample_shows_rules_page_numbers_and_a_kept_block()
    {
        var pages = PdfPageReader.Save(HeadersFootersSample.Build(new HeadersFootersInput { SectionCount = 8, RowsPerSection = 20 }));

        Assert.True(pages.Count >= 3);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.True(pages[0].Shows("Prepared by the finance team"));
        Assert.False(pages[1].Shows("Prepared by the finance team"));
        Assert.True(pages[1].Shows("Confidential"));
        Assert.True(pages[pages.Count - 1].Shows("End of report"));
        Assert.True(pages[pages.Count - 1].Shows("Reviewed and approved"));
        Assert.True(pages[pages.Count - 1].Shows("Date: ______________________"));

        // Every section heading is on the page of its table's first row: the heading is never the last line of a page.
        foreach (var page in pages)
        {
            var texts = page.LayoutTexts().Where(t => t.Text.EndsWith(" region")).ToList();
            foreach (var heading in texts) Assert.True(page.Shows("Month"), $"Heading '{heading.Text}' has no table below it.");
        }
    }

    [Fact]
    public void Headers_and_footers_sample_fails_to_save_past_its_page_limit()
    {
        var document = HeadersFootersSample.Build(new HeadersFootersInput { SectionCount = 10, RowsPerSection = 30, MaxPages = 2 });

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        Assert.Contains("Body.MaxPages is 2", error.Message);
    }

    [Fact]
    public void No_demo_uses_a_table_end_query()
    {
        var root = Path.GetFullPath(Path.Combine(Fixtures.DemoAssets(), "..", ".."));
        var sources = Directory.GetFiles(Path.Combine(root, "UnlimitedPDF.Demos.BlazorWasm", "Samples"), "*.cs")
            .Append(Path.Combine(root, "UnlimitedPDF.Demo.ConsoleApp", "DemoDocument.cs"));

        Assert.All(sources, path => Assert.DoesNotContain("GetTableEnd", File.ReadAllText(path)));
    }

    #endregion

    #region Private Methods

    // Every page shows "Page n of m" with its own number and the page count.
    private static void AssertNumbered(IReadOnlyList<PdfPageReader.Page> pages)
    {
        for (int p = 0; p < pages.Count; p++)
            Assert.True(pages[p].Shows($"Page {p + 1} of {pages.Count}"), $"Page {p + 1} has no page number.");
    }

    // No two filled rectangles on a page overlap by more than touching edges: table cells and flow rectangles are all
    // drawn as filled rectangles, so an overlap means content was drawn over a table.
    private static void AssertNoOverlaps(IReadOnlyList<PdfPageReader.Page> pages)
    {
        for (int p = 0; p < pages.Count; p++)
        {
            var rectangles = pages[p].FilledRectangles().Concat(BodyRectangles(pages[p])).ToList();
            for (int i = 0; i < rectangles.Count; i++)
            {
                for (int j = i + 1; j < rectangles.Count; j++)
                {
                    var a = rectangles[i];
                    var b = rectangles[j];
                    double width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
                    double height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
                    Assert.False(width > Tolerance && height > Tolerance, $"Page {p + 1}: rectangle {a} overlaps {b}.");
                }
            }
        }
    }

    // Rectangles the flow draws, outside the header and footer bands (which are drawn inside a clip).
    private static IEnumerable<(double X, double Y, double Width, double Height)> BodyRectangles(PdfPageReader.Page page)
    {
        int clip = page.Content.IndexOf("re W n", StringComparison.Ordinal);
        var body = clip < 0 ? page.Content : page.Content.Substring(0, clip);
        return new PdfPageReader.Page(page.MediaBox, body).LayoutRectangles();
    }

    // Every image a demo names, as a small generated JPEG (PR-011).
    private static SampleImages Images()
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 120, Height = 60 }.Build());
        var names = new[] { "invoice-logo.png", "keyboard.jpg", "dock.jpg", "receipt-logo.png", "medical-report-logo.png" };
        return new SampleImages(names.ToDictionary(name => name, _ => image));
    }

    #endregion

    #endregion
}
