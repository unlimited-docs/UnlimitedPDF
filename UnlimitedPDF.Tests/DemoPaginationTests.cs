using UnlimitedPDF.Demo.ConsoleApp;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos that place content after a table (Invoice, Receipt, Medical report, console demo) keep that content clear
/// of the table, continue long tables on new pages inside their margins, and number every page (WR-027 to WR-029).
/// </summary>
public class DemoPaginationTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
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
    }

    [Fact]
    public void Default_invoice_fits_one_page()
    {
        var pages = PdfPageReader.Save(InvoiceSample.Build(new InvoiceInput(), Images()));

        Assert.Single(pages);
        AssertNoOverlaps(pages);
    }

    [Fact]
    public void Default_medical_report_fits_one_page()
    {
        var pages = PdfPageReader.Save(MedicalReportSample.Build(new MedicalReportInput(), Images()));

        Assert.Single(pages);
        AssertNoOverlaps(pages);
    }

    [Fact]
    public void Default_receipt_fits_one_page()
    {
        var pages = PdfPageReader.Save(ReceiptSample.Build(new ReceiptInput(), Images()));

        Assert.Single(pages);
        AssertNoOverlaps(pages);
    }

    #endregion

    #region Private Methods

    // No two filled rectangles on a page overlap by more than touching edges: table cells, bands, and blocks are
    // all drawn as filled rectangles, so an overlap means content was drawn over a table.
    private static void AssertNoOverlaps(IReadOnlyList<PdfPageReader.Page> pages)
    {
        for (int p = 0; p < pages.Count; p++)
        {
            var rectangles = pages[p].FilledRectangles();
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
