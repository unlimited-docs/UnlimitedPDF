using System.Reflection;
using System.Runtime.CompilerServices;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// The renderer draws every page of every demo's output completely, with the same text as the PDF, for every demo the
/// catalog lists and each of its variants; and the library's public surface is unchanged by it (26111701 AC-08,
/// AC-12, V-02, V-08).
/// </summary>
public class PageRendererCoverageTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Every_demo_in_the_catalog_has_a_builder_here()
    {
        // A new demo can't be skipped quietly: it must be added to Outputs below.
        var covered = Outputs().Select(o => o.Demo).ToHashSet(StringComparer.Ordinal);
        var missing = SampleCatalog.Pages
            .Select(d => d.Title).Where(title => !covered.Contains(title)).ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(DemoOutputs))]
    public async Task Every_page_of_every_demo_output_is_drawn_with_its_text(string demo, string variant)
    {
        var output = Outputs().Single(o => o.Demo == demo && o.Variant == variant);

        foreach (byte[] pdf in await output.Build()) RenderTestFiles.AssertDrawnWithTheSameText(pdf);
    }

    [Fact]
    public void The_library_names_only_the_tests_and_the_demos_site_as_friends()
    {
        var friends = typeof(PdfDocument).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "UnlimitedPDF.Demos.BlazorWasm", "UnlimitedPDF.Tests" }, friends);
    }

    [Fact]
    public void The_public_surface_matches_the_recorded_one()
    {
        // The renderer is internal (AC-08): any change here is a deliberate public API change, recorded with it.
        string expected = File.ReadAllText(SurfaceFile()).Replace("\r\n", "\n").TrimEnd();

        Assert.Equal(expected, PublicSurface.Describe(typeof(PdfDocument).Assembly));
    }

    #endregion

    #region Private Methods

    private static PdfFile Generated(int pages) => PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(pages));

    private static async Task<IEnumerable<byte[]>> Html(string html)
    {
        var (document, _) = await HtmlToPdfSample.BuildAsync(html, (reference, _) => Task.FromResult(UnlimitedPDF.Tests.Html.HtmlGoldenTests.Images(reference)));
        return new[] { PdfPageReader.SaveBytes(document) };
    }

    // Every image a demo names, as a small generated JPEG (PR-011).
    private static SampleImages Images()
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 120, Height = 60 }.Build());
        var names = new[]
        {
            "invoice-logo.png", "keyboard.jpg", "dock.jpg", "receipt-logo.png", "medical-report-logo.png", "purchase-order-logo.png",
            "quotation-logo.png", "account-statement-logo.png", "payslip-logo.png", "delivery-note-logo.png",
        };
        return new SampleImages(names.ToDictionary(name => name, _ => image));
    }

    private static FontsAndLanguagesInput LanguageInput(string id)
    {
        var input = new FontsAndLanguagesInput();
        input.Apply(LanguageInvoices.Find(id));
        return input;
    }

    private static Func<Task<IEnumerable<byte[]>>> One(Func<PdfDocument> build) =>
        () => Task.FromResult<IEnumerable<byte[]>>(new[] { PdfPageReader.SaveBytes(build()) });

    // The default output of each demo, and of each of its variants, as its page builds it.
    private static IEnumerable<(string Demo, string Variant, Func<Task<IEnumerable<byte[]>>> Build)> Outputs()
    {
        var images = Images();
        yield return ("Text and pages", "", One(() => TextBasicsSample.Build(new TextBasicsInput())));
        yield return ("Tables", "", One(() => TablesSample.Build(new TablesInput())));
        yield return ("Images", "", One(() => ImagesSample.Build(new ImagesInput(), images["dock.jpg"])));
        yield return ("Headers and footers", "", One(() => HeadersFootersSample.Build(new HeadersFootersInput())));
        yield return ("Join files", "", One(() =>
        {
            var input = new JoinFilesInput();
            input.Files.Add(new MergeFileInput("a.pdf", Generated(2)));
            input.Files.Add(new MergeFileInput("b.pdf", Generated(1)));
            return JoinFilesSample.Build(input);
        }));
        yield return ("Cover + ranges", "", One(() =>
        {
            var input = new CoverAndRangesInput();
            input.Files.Add(new MergeFileInput("a.pdf", Generated(3)) { FirstPage = 2, LastPage = 3 });
            return CoverAndRangesSample.Build(input);
        }));
        yield return ("Pick pages", "", One(() => PickPagesSample.Build(new PickPagesInput { File = new MergeFileInput("Report", Generated(3)) })));
        yield return ("Bookmarks & numbers", "", One(() =>
        {
            var input = new BookmarksAndNumbersInput { FooterText = "Page {page} of {pages}" };
            input.Files.Add(new MergeFileInput("a.pdf", Generated(2)));
            input.Files.Add(new MergeFileInput("b.pdf", Generated(1)));
            return BookmarksAndNumbersSample.Build(input);
        }));
        foreach (var domain in DemoVariants.PackDomains)
        {
            yield return ("Document packs", domain.Value, One(() =>
            {
                var input = MergePackSample.Input(domain.Value);
                foreach (var document in input.Documents) document.File = Generated(1);
                return MergePackSample.Build(input);
            }));
        }
        yield return ("Every N pages", "", () => Task.FromResult(SplitEveryNSample.Build(new SplitInput { File = new MergeFileInput("report.pdf", Generated(5)), PagesPerPart = 2 }).Select(p => p.Bytes)));
        yield return ("Page ranges", "", () => Task.FromResult(SplitRangesSample.Build(new SplitInput { File = new MergeFileInput("report.pdf", Generated(5)) }).Select(p => p.Bytes)));
        foreach (var (name, html) in new[] { ("invoice", HtmlTemplates.Invoice), ("newsletter", HtmlTemplates.Newsletter), ("letter", HtmlTemplates.Letter), ("report", HtmlTemplates.Report), ("resume", HtmlTemplates.Resume), ("statement", HtmlTemplates.Statement()) })
        {
            yield return ("HTML to PDF", name, () => Html(html));
        }
        foreach (var scenario in HtmlScenarios.All)
        {
            yield return ("Common problems", scenario.Id, () => Html(scenario.Html));
        }
        foreach (var language in LanguageInvoices.All)
        {
            yield return ("Fonts and languages", language.Id, One(() =>
            {
                var input = LanguageInput(language.Id);
                var fonts = Fonts.DemoFonts.For(input);
                return FontsAndLanguagesSample.Build(input, fonts, fonts);
            }));
            yield return ("Fonts and languages", language.Id + "-html", One(() =>
            {
                var input = LanguageInput(language.Id);
                return FontsAndLanguagesHtmlSample.Build(input, Fonts.DemoFonts.Slots(input), out _);
            }));
        }
        yield return ("Invoice", "", One(() => InvoiceSample.Build(new InvoiceInput(), images)));
        yield return ("Quotation", "", One(() => QuotationSample.Build(new QuotationInput(), images)));
        yield return ("Purchase order", "", One(() => PurchaseOrderSample.Build(new PurchaseOrderInput(), images)));
        yield return ("Delivery note", "", One(() => DeliveryNoteSample.Build(new DeliveryNoteInput(), images)));
        yield return ("Receipt", "", One(() => ReceiptSample.Build(new ReceiptInput(), images)));
        yield return ("Account statement", "", One(() => AccountStatementSample.Build(new AccountStatementInput(), images)));
        yield return ("Payslip", "", One(() => PayslipSample.Build(new PayslipInput(), images)));
        yield return ("Medical report", "", One(() => MedicalReportSample.Build(new MedicalReportInput(), images)));
        yield return ("Bulk invoices", "", () => Task.FromResult(BulkInvoicesSample.Build(new BulkInvoicesInput { Count = 3 }, images).Select(i => i.Bytes)));
        yield return ("Inventory report", "", One(() => InventoryReportSample.Build(new InventoryReportInput())));
        yield return ("Contract", "", One(() => ContractSample.Build(new ContractInput())));
        yield return ("KPI report", "", One(() => KpiReportSample.Build(new KpiReportInput())));
        yield return ("Shipping labels", "", One(() => ShippingLabelsSample.Build(new ShippingLabelsInput())));
    }

    private static string SurfaceFile([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "..", "Fixtures", "public-surface.txt");

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<string, string> DemoOutputs
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (demo, variant, _) in Outputs()) data.Add(demo, variant);
            return data;
        }
    }

    #endregion
}
