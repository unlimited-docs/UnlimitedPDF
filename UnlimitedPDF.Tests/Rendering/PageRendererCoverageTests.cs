using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// The renderer draws every page of every demo's output completely, with the same text as the PDF, and the library's
/// public surface is unchanged by it (26111701 AC-08, AC-12, V-02, V-08).
/// </summary>
public class PageRendererCoverageTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [MemberData(nameof(DemoOutputs))]
    public void Every_page_of_every_demo_output_is_drawn_with_its_text(string demo)
    {
        byte[] pdf = Build(demo);

        RenderTestFiles.AssertDrawnWithTheSameText(pdf);
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

        Assert.Equal(expected, PublicSurface());
    }

    #endregion

    #region Private Methods

    private static byte[] Build(string demo)
    {
        var images = Images();
        PdfDocument document = demo switch
        {
            "invoice" => InvoiceSample.Build(new InvoiceInput(), images),
            "quotation" => QuotationSample.Build(new QuotationInput(), images),
            "purchase-order" => PurchaseOrderSample.Build(new PurchaseOrderInput(), images),
            "delivery-note" => DeliveryNoteSample.Build(new DeliveryNoteInput(), images),
            "account-statement" => AccountStatementSample.Build(new AccountStatementInput(), images),
            "payslip" => PayslipSample.Build(new PayslipInput(), images),
            "medical-report" => MedicalReportSample.Build(new MedicalReportInput(), images),
            "receipt" => ReceiptSample.Build(new ReceiptInput(), images),
            "certificate" => CertificateSample.Build(new CertificateInput(), images),
            "employee-directory" => EmployeeDirectorySample.Build(new EmployeeDirectoryInput()),
            "headers-and-footers" => HeadersFootersSample.Build(new HeadersFootersInput()),
            "bookmarks-and-numbers" => BookmarksAndNumbers(),
            _ when demo.StartsWith("language-", StringComparison.Ordinal) => Language(demo.Substring("language-".Length)),
            _ when demo == "split-part" => null!,
            _ => throw new ArgumentOutOfRangeException(nameof(demo)),
        };
        if (demo == "split-part")
        {
            var parts = SplitEveryNSample.Build(new SplitInput { File = new MergeFileInput("report.pdf", PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3))), PagesPerPart = 2 });
            return parts[0].Bytes;
        }
        return PdfPageReader.SaveBytes(document);
    }

    private static PdfDocument BookmarksAndNumbers()
    {
        var input = new BookmarksAndNumbersInput { FooterText = "Page {page} of {pages}" };
        input.Files.Add(new MergeFileInput("a.pdf", PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2))));
        input.Files.Add(new MergeFileInput("b.pdf", PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1))));
        return BookmarksAndNumbersSample.Build(input);
    }

    // Every image a demo names, as a small generated JPEG (PR-011).
    private static SampleImages Images()
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 120, Height = 60 }.Build());
        var names = new[]
        {
            "invoice-logo.png", "keyboard.jpg", "dock.jpg", "receipt-logo.png", "medical-report-logo.png", "purchase-order-logo.png",
            "quotation-logo.png", "account-statement-logo.png", "payslip-logo.png", "delivery-note-logo.png", "certificate-seal.png",
            "certificate-signature.png",
        };
        return new SampleImages(names.ToDictionary(name => name, _ => image));
    }

    private static PdfDocument Language(string id)
    {
        var input = new FontsAndLanguagesInput();
        input.Apply(LanguageInvoices.Find(id));
        var fonts = Fonts.DemoFonts.For(input);
        return FontsAndLanguagesSample.Build(input, fonts, fonts);
    }

    // Every public type with its public members, one per line, in a stable order.
    private static string PublicSurface()
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var lines = new List<string>();
        foreach (var type in typeof(PdfDocument).Assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add(type.FullName!);
            foreach (var member in type.GetMembers(Flags).Select(m => "  " + m).OrderBy(m => m, StringComparer.Ordinal)) lines.Add(member);
        }
        return string.Join("\n", lines);
    }

    private static string SurfaceFile([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "..", "Fixtures", "public-surface.txt");

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<string> DemoOutputs
    {
        get
        {
            var data = new TheoryData<string>
            {
                "invoice", "quotation", "purchase-order", "delivery-note", "account-statement", "payslip", "medical-report",
                "receipt", "certificate", "employee-directory", "headers-and-footers", "bookmarks-and-numbers", "split-part",
            };
            foreach (var language in LanguageInvoices.All) data.Add("language-" + language.Id);
            return data;
        }
    }

    #endregion
}
