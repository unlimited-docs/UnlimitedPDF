using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos with variants (the Invoice demo's document types, the Document packs demo's domains): each variant has
/// its own address that opens it again, any other address opens the first one, each domain's pack builds, and every
/// demo page belongs to known business domains (26111701 AC-09, T-09).
/// </summary>
public class DemoVariantsTests
{
    #region Fields and Constants

    private static readonly string[] KnownDomains = ["General", "Finance", "HR", "Healthcare", "Retail", "Logistics"];

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Each_domain_pack_holds_documents_the_page_generates_and_builds_with_a_cover()
    {
        // The page's generator must know every name a pack lists, or the pack fails when it is opened.
        var generated = GeneratorNames("Pages/Playground/Merge/DocumentPacks.razor");
        var titles = new List<string>();

        foreach (var domain in DemoVariants.PackDomains)
        {
            var input = MergePackSample.Input(domain.Value);
            foreach (var document in input.Documents) document.File = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1));
            var pack = PdfFile.FromBytes(PdfPageReader.SaveBytes(MergePackSample.Build(input)));

            Assert.NotEmpty(input.Documents);
            Assert.All(input.Documents, d => Assert.Contains(d.Name, MergePackSample.DocumentNames));
            Assert.Equal(1 + input.Documents.Count, pack.PageCount);
            titles.Add(input.CoverTitle);
        }

        Assert.Equal(MergePackSample.DocumentNames.Order(), generated.Order());
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Each_variant_address_opens_that_variant_and_the_first_has_no_query()
    {
        foreach (var (variants, parameter) in new[] { (DemoVariants.InvoiceTypes, "type"), (DemoVariants.PackDomains, "domain") })
        {
            Assert.DoesNotContain('?', variants[0].Address);
            Assert.Equal(variants.Count, variants.Select(v => v.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var variant in variants)
            {
                var query = Regex.Match(variant.Address, $@"\?{parameter}=([^&]+)$");
                string? value = query.Success ? Uri.UnescapeDataString(query.Groups[1].Value) : null;

                Assert.Same(variant, DemoVariants.Find(variants, value));
                Assert.Equal(variant == variants[0], value is null);
            }
        }
    }

    [Fact]
    public void Every_demo_page_names_known_business_domains()
    {
        Assert.All(SampleCatalog.Pages, d =>
        {
            Assert.NotEmpty(d.Domains);
            Assert.Equal(d.Domains.Count, d.Domains.Distinct(StringComparer.Ordinal).Count());
            Assert.All(d.Domains, domain => Assert.Contains(domain, KnownDomains));
        });
    }

    [Fact]
    public void The_invoice_page_has_a_page_for_each_type_and_they_follow_the_catalog()
    {
        var types = DemoVariants.InvoiceTypes;

        Assert.Equal(new[] { SampleCatalog.Invoice }.Concat(SampleCatalog.InvoiceTypes).Select(d => d.Title), types.Select(t => t.Label));
        Assert.Equal(new[] { SampleCatalog.Invoice }.Concat(SampleCatalog.InvoiceTypes).Select(d => d.Route), types.Select(t => t.Address));
        Assert.Equal(types.Select(t => t.Value).Order(), PageKeys("Pages/Playground/Documents/InvoiceDocuments.razor").Order());
    }

    [Fact]
    public void The_pages_list_holds_every_demo_once_with_the_invoice_types_after_the_invoice()
    {
        var pages = SampleCatalog.Pages;
        int invoice = pages.ToList().IndexOf(SampleCatalog.Invoice);

        Assert.Equal(pages.Count, pages.Distinct().Count());
        Assert.Equal(SampleCatalog.InvoiceTypes, pages.Skip(invoice + 1).Take(SampleCatalog.InvoiceTypes.Count));
        Assert.Equal(SampleCatalog.Categories.SelectMany(c => c.Demos).Distinct().Count() + SampleCatalog.InvoiceTypes.Count, pages.Count);
    }

    [Theory]
    [InlineData(null, "invoice")]
    [InlineData("", "invoice")]
    [InlineData("bogus", "invoice")]
    [InlineData("Quotation", "quotation")]
    [InlineData(" PURCHASE-ORDER ", "purchase-order")]
    [InlineData("receipt", "receipt")]
    public void Unknown_types_open_the_invoice_and_case_is_ignored(string? value, string expected)
    {
        Assert.Equal(expected, DemoVariants.Find(DemoVariants.InvoiceTypes, value).Value);
    }

    [Theory]
    [InlineData(null, "finance")]
    [InlineData("payroll", "finance")]
    [InlineData("HR", "hr")]
    [InlineData("Healthcare", "healthcare")]
    public void Unknown_domains_open_the_finance_pack_and_case_is_ignored(string? value, string expected)
    {
        Assert.Equal(expected, DemoVariants.Find(DemoVariants.PackDomains, value).Value);
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    // The names the packs page generates: each case of its GenerateAsync switch.
    private static IEnumerable<string> GeneratorNames(string page) =>
        Regex.Matches(DemosFile(page), "\"([^\"]+)\" => MergeDemoFiles\\.").Select(m => m.Groups[1].Value);

    // The types the invoice page maps to a page: each key of its Pages map.
    private static IEnumerable<string> PageKeys(string page) =>
        Regex.Matches(DemosFile(page), "\\[\"([a-z-]+)\"\\] = typeof\\(").Select(m => m.Groups[1].Value);

    #endregion

    #endregion
}
