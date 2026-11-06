using System.Text.RegularExpressions;
using UnlimitedPDF.Demo.ConsoleApp;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The document demos (Invoice, Quotation, Purchase order, Delivery note, Account statement, Payslip, Receipt, Medical
/// report, Employee directory, console demo) are built on header and footer sections and the body flow (WR-031,
/// WR-033): with default data and with many rows, no filled rectangles overlap, no text line overlaps another (measured
/// with Helvetica widths), and every page shows its header, its footer, and "Page n of m".
/// </summary>
public class DemoPaginationTests
{
    #region Fields and Constants

    private const double Tolerance = 1e-6;

    // A text object: its font size, its position (Tm or Td), and its escaped text.
    private static readonly Regex TextObject = new(
        @"BT\s+(?:0 0 0 rg\s+)?/F1 (\S+) Tf\s+(?:0 0 0 rg\s+)?(?:1 0 0 1 (\S+) (\S+) Tm|(\S+) (\S+) Td)\s+\(((?:\\.|[^\\)])*)\) Tj\s+ET",
        RegexOptions.CultureInvariant);

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
    public void Purchase_order_runs_across_pages_and_keeps_its_totals_with_the_authorization()
    {
        Assert.Single(PdfPageReader.Save(PurchaseOrderSample.Build(new PurchaseOrderInput(), Images())));

        var input = new PurchaseOrderInput { GeneratedLines = 300, VendorName = "Apex Components (India Ltd" };
        var pages = PdfPageReader.Save(PurchaseOrderSample.Build(input, Images()));

        Assert.True(pages.Count >= 3);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.All(pages, page => Assert.True(page.Shows("Unit price")));
        Assert.Contains("(Apex Components \\(India Ltd) Tj", pages[0].Content);
        var last = pages[pages.Count - 1];
        Assert.True(last.Shows("Total"));
        Assert.Contains(last.LayoutTexts(), t => t.Text.StartsWith("Authorized by:"));
    }

    [Fact]
    public void Quotation_has_its_own_first_page_header_last_page_footer_and_kept_terms()
    {
        var input = new QuotationInput();
        for (int i = 0; i < 40; i++) input.Services.Add(new OrderLine { Description = "Support hours, block " + i, Quantity = 4, UnitPrice = 55m });

        var pages = PdfPageReader.Save(QuotationSample.Build(input, Images()));

        Assert.True(pages.Count >= 2);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.True(pages[0].Shows("QUOTATION"));
        Assert.All(pages.Skip(1), page => Assert.False(page.Shows("QUOTATION")));
        Assert.All(pages.Take(pages.Count - 1), page => Assert.False(page.Shows("Thank you for your business")));
        var last = pages[pages.Count - 1];
        Assert.True(last.Shows("Thank you for your business"));
        var terms = pages.Single(page => page.Shows("Terms and acceptance"));
        Assert.Contains(terms.LayoutTexts(), t => t.Text.StartsWith("Accepted by:"));
    }

    [Fact]
    public void Account_statement_repeats_its_header_and_closes_with_the_running_balance()
    {
        foreach (int generated in new[] { 60, 1000 })
        {
            var input = new AccountStatementInput { GeneratedTransactions = generated };
            var pages = PdfPageReader.Save(AccountStatementSample.Build(input, Images()));

            AssertNoOverlaps(pages);
            AssertNumbered(pages);
            Assert.True(pages[0].Shows("Opening balance"));
            Assert.All(pages.Skip(1), page => Assert.False(page.Shows("Opening balance")));
            Assert.All(pages, page => Assert.True(page.Shows("Balance")));
            Assert.True(pages[pages.Count - 1].Shows("End of statement"));
            Assert.All(pages.Take(pages.Count - 1), page => Assert.False(page.Shows("End of statement")));

            // The last row's balance equals the closing balance, and both are on the last page.
            var closing = pages[pages.Count - 1].LayoutTexts().Single(t => t.Text.StartsWith("Closing balance on")).Text;
            string amount = closing.Substring(closing.LastIndexOf(' ') + 1);
            Assert.True(pages[pages.Count - 1].Shows(amount), $"The last row does not show {amount}.");
        }
    }

    [Fact]
    public void Payslip_is_one_page_and_too_many_lines_fail_with_the_page_limit()
    {
        var input = new PayslipInput();
        var pages = PdfPageReader.Save(PayslipSample.Build(input, Images()));

        Assert.Single(pages);
        AssertNumbered(pages);
        AssertNoOverlaps(pages);
        decimal net = input.Earnings.Sum(line => line.Amount) - input.Deductions.Sum(line => line.Amount);
        Assert.True(pages[0].Shows(net.ToString("N2")));

        var crowded = new PayslipInput();
        for (int i = 0; i < 60; i++) crowded.Earnings.Add(new PayslipLine { Label = "Allowance " + i, Amount = 100m });
        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(PayslipSample.Build(crowded, Images())));
        Assert.Contains("Body.MaxPages is 1", error.Message);
    }

    [Fact]
    public void Delivery_note_repeats_its_header_and_keeps_the_receipt_block_together()
    {
        // Review F-17: the one-line sender address ran across into the recipient's column.
        var single = PdfPageReader.Save(DeliveryNoteSample.Build(new DeliveryNoteInput(), Images()));
        Assert.Single(single);
        AssertNoOverlaps(single);
        Assert.True(single[0].Shows("Gate 3, Port Road"));
        Assert.True(single[0].Shows("Mumbai, MH 400001"));
        Assert.True(single[0].Shows("Nagpur, MH 440023"));

        var pages = PdfPageReader.Save(DeliveryNoteSample.Build(new DeliveryNoteInput { GeneratedLines = 300 }, Images()));

        Assert.True(pages.Count >= 3);
        AssertNoOverlaps(pages);
        AssertNumbered(pages);
        Assert.All(pages, page => Assert.True(page.Shows("Shipped")));
        var receipt = pages.Single(page => page.Shows("Received in good condition"));
        Assert.Contains(receipt.LayoutTexts(), t => t.Text.StartsWith("Date: "));
        Assert.Same(pages[pages.Count - 1], receipt);
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

            var texts = TextBoxes(pages[p]);
            Assert.NotEmpty(texts);
            for (int i = 0; i < texts.Count; i++)
            {
                for (int j = i + 1; j < texts.Count; j++)
                {
                    var a = texts[i];
                    var b = texts[j];
                    double width = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
                    double height = Math.Min(a.Top, b.Top) - Math.Max(a.Bottom, b.Bottom);
                    Assert.False(width > Tolerance && height > Tolerance, $"Page {p + 1}: text \"{a.Text}\" overlaps \"{b.Text}\".");
                }
            }
        }
    }

    // Every text line the page draws, as the box from its baseline's descent to its ascent and from its start to its
    // Helvetica width.
    private static List<(string Text, double Left, double Right, double Bottom, double Top)> TextBoxes(PdfPageReader.Page page)
    {
        var boxes = new List<(string, double, double, double, double)>();
        foreach (Match match in TextObject.Matches(page.Content))
        {
            string text = Regex.Replace(match.Groups[6].Value, @"\\(.)", "$1");
            if (text.Trim().Length == 0) continue;
            double size = Number(match.Groups[1].Value);
            bool matrix = match.Groups[2].Success;
            double x = Number(matrix ? match.Groups[2].Value : match.Groups[4].Value);
            double baseline = Number(matrix ? match.Groups[3].Value : match.Groups[5].Value);
            boxes.Add((text, x, x + UnlimitedPDF.Models.Internal.HelveticaMetrics.Width(text, size), baseline - 0.2 * size, baseline + 0.7 * size));
        }
        return boxes;
    }

    private static double Number(string value) => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

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
        var names = new[]
        {
            "invoice-logo.png", "keyboard.jpg", "dock.jpg", "receipt-logo.png", "medical-report-logo.png", "purchase-order-logo.png",
            "quotation-logo.png", "account-statement-logo.png", "payslip-logo.png", "delivery-note-logo.png"
        };
        return new SampleImages(names.ToDictionary(name => name, _ => image));
    }

    #endregion

    #endregion
}
