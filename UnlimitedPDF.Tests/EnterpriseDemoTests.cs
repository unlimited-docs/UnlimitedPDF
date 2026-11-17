using System.Runtime.CompilerServices;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Tests.Html;
using UnlimitedPDF.Tests.Rendering;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The five enterprise demos and the larger Account statement (26111701 AC-11, D-013, T-10): each generates what its
/// page promises, the counts are capped, and every page of the catalog has its page, sitemap entry, and domain.
/// </summary>
public class EnterpriseDemoTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Bulk_invoices_make_one_numbered_pdf_each_and_stop_at_a_thousand()
    {
        var images = Images();

        var three = BulkInvoicesSample.Build(new BulkInvoicesInput { Count = 3 }, images);
        Assert.Equal(new[] { "INV-2026-0001.pdf", "INV-2026-0002.pdf", "INV-2026-0003.pdf" }, three.Select(i => i.FileName));
        Assert.All(three, invoice =>
        {
            PdfStructureChecker.AssertValid(invoice.Bytes);
            Assert.Contains(invoice.FileName[..^4], string.Join(" ", ImportedPagesTests.PageTexts(PdfFile.FromBytes(invoice.Bytes))), StringComparison.Ordinal);
        });
        Assert.NotEqual(BulkInvoicesSample.Invoice(new BulkInvoicesInput(), 0).BuyerName, BulkInvoicesSample.Invoice(new BulkInvoicesInput(), 1).BuyerName);

        // The count is kept between 1 and 1,000 without making the invoices.
        Assert.Single(BulkInvoicesSample.Build(new BulkInvoicesInput { Count = 0 }, images));
        Assert.Equal(1000, BulkInvoicesInput.MaxCount);
        Assert.Contains("Math.Clamp(input.Count, 1, BulkInvoicesInput.MaxCount)", Sample("BulkInvoicesSample.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_account_statement_takes_up_to_five_thousand_rows()
    {
        var input = new AccountStatementInput { GeneratedTransactions = 6000 };
        var images = Images();

        var big = PdfFile.FromBytes(PdfPageReader.SaveBytes(AccountStatementSample.Build(input, images)));
        var capped = PdfFile.FromBytes(PdfPageReader.SaveBytes(AccountStatementSample.Build(new AccountStatementInput { GeneratedTransactions = 5000 }, images)));

        Assert.Equal(5000, AccountStatementInput.MaxGeneratedTransactions);
        Assert.Equal(capped.PageCount, big.PageCount);
        Assert.True(big.PageCount > 100, $"{big.PageCount} pages");
    }

    [Fact]
    public void The_contract_is_html_with_numbered_clauses_page_x_of_y_and_encoded_values()
    {
        var input = new ContractInput { EmployeeName = "<b>Asha</b> & Co" };

        string html = ContractSample.Html(input);
        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(PdfPageReader.SaveBytes(ContractSample.Build(input))));

        Assert.Contains("&lt;b&gt;Asha&lt;/b&gt; &amp; Co", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Asha", html, StringComparison.Ordinal);
        Assert.True(texts.Length >= 2, $"{texts.Length} pages");
        Assert.Contains($"Page 1 of {texts.Length}", texts[0], StringComparison.Ordinal);
        Assert.Contains("7. General", string.Join(" ", texts), StringComparison.Ordinal);
        Assert.Contains("<b>Asha</b> & Co", texts[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_inventory_report_has_a_subtotal_per_warehouse_and_a_grand_total()
    {
        var input = new InventoryReportInput { Warehouses = 3, ItemsPerWarehouse = 60 };

        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(PdfPageReader.SaveBytes(InventoryReportSample.Build(input))));
        string all = string.Join(" ", texts);

        Assert.True(texts.Length > 3, $"{texts.Length} pages");
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(all, "Subtotal, ").Count);
        Assert.Contains("Grand total across 3 warehouses", all, StringComparison.Ordinal);
        // The header row repeats on every page.
        Assert.All(texts, text => Assert.Contains("Unit cost", text, StringComparison.Ordinal));
        Assert.Equal(InventoryReportInput.MaxWarehouses,
            System.Text.RegularExpressions.Regex.Matches(string.Join(" ", ImportedPagesTests.PageTexts(PdfFile.FromBytes(PdfPageReader.SaveBytes(
                InventoryReportSample.Build(new InventoryReportInput { Warehouses = 99, ItemsPerWarehouse = 1 }))))), "Subtotal, ").Count);
    }

    [Fact]
    public void The_kpi_report_draws_a_bar_per_month_scaled_to_the_best()
    {
        var input = new KpiReportInput();

        var page = RenderTestFiles.RenderFirst(PdfPageReader.SaveBytes(KpiReportSample.Build(input)));
        string text = string.Join(" ", page.TextLines);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains(input.MonthlyRevenue.Sum().ToString("N0"), text, StringComparison.Ordinal);
        Assert.All(new[] { "Jan", "Jun", "Dec" }, month => Assert.Contains(month, text, StringComparison.Ordinal));
        // A negative month draws no bar and no error.
        input.MonthlyRevenue[3] = -10;
        Assert.True(RenderTestFiles.RenderFirst(PdfPageReader.SaveBytes(KpiReportSample.Build(input))).IsDrawn);
    }

    [Fact]
    public void Shipping_labels_fill_sheets_of_twenty_one()
    {
        Assert.Equal(1, Pages(new ShippingLabelsInput { Count = 21 }));
        Assert.Equal(2, Pages(new ShippingLabelsInput { Count = 22 }));
        Assert.Equal(10, Pages(new ShippingLabelsInput { Count = 5000 }));
        Assert.Equal(1, Pages(new ShippingLabelsInput { Count = -3 }));
    }

    [Fact]
    public void Every_new_demo_has_a_page_a_sitemap_entry_and_a_domain()
    {
        var demos = new[] { SampleCatalog.BulkInvoices, SampleCatalog.InventoryReport, SampleCatalog.Contract, SampleCatalog.KpiReport, SampleCatalog.ShippingLabels };
        string sitemap = DemosFile("wwwroot/sitemap.xml");

        Assert.Equal(23, ScenarioGallery.Scenarios.Count);
        Assert.All(demos, demo =>
        {
            Assert.Contains(demo, ScenarioGallery.Scenarios);
            Assert.Contains($"<loc>https://unlimitedpdf.dev/{demo.Route}</loc>", sitemap, StringComparison.Ordinal);
            Assert.Contains($"@page \"/{demo.Route}\"", DemosFile($"Pages/Playground/Documents/{demo.SourceFile.Replace("Sample.cs", ".razor")}"), StringComparison.Ordinal);
            Assert.NotEmpty(demo.Domains);
        });
        Assert.Contains("Legal", SampleCatalog.Contract.Domains);
        Assert.Contains("Management", SampleCatalog.KpiReport.Domains);
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    // The logos the samples name, as a small generated JPEG (PR-011).
    private static SampleImages Images()
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 120, Height = 60 }.Build());
        return new SampleImages(new[] { "invoice-logo.png", "account-statement-logo.png", "keyboard.jpg", "dock.jpg" }.ToDictionary(n => n, _ => image));
    }

    private static int Pages(ShippingLabelsInput input) => PdfFile.FromBytes(PdfPageReader.SaveBytes(ShippingLabelsSample.Build(input))).PageCount;

    private static string Sample(string file) => DemosFile("Samples/" + file);

    #endregion

    #endregion
}
