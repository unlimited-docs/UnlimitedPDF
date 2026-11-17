using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The scenario gallery at <c>/playground</c> (26111701 AC-06, D-022, T-11): the capability tabs and the domain chips
/// each reach every scenario, a filtered view's address opens it again, and unknown values show everything.
/// </summary>
public class ScenarioGalleryTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Each_filter_address_opens_its_filters_again()
    {
        var capabilities = SampleCatalog.Categories.Select(c => c.Id).Prepend(null);
        var domains = ScenarioGallery.Domains.Prepend(null);

        Assert.Equal("playground", ScenarioGallery.Address(null, null));
        foreach (var capability in capabilities)
        {
            foreach (var domain in domains)
            {
                string address = ScenarioGallery.Address(capability, domain);

                Assert.Equal(capability, ScenarioGallery.FindCapability(Query(address, "capability")));
                Assert.Equal(domain, ScenarioGallery.FindDomain(Query(address, "domain")));
            }
        }
    }

    [Fact]
    public void Every_scenario_has_a_category_and_a_known_domain()
    {
        Assert.All(SampleCatalog.Pages, demo =>
        {
            Assert.NotNull(ScenarioGallery.CategoryOf(demo).Id);
            Assert.All(demo.Domains, domain => Assert.Contains(domain, ScenarioGallery.Domains));
        });
        Assert.All(ScenarioGallery.Scenarios, demo => Assert.NotEmpty(ScenarioGallery.DomainsOf(demo)));
    }

    [Fact]
    public void The_capability_tabs_and_the_domain_chips_each_reach_every_scenario()
    {
        var all = ScenarioGallery.Scenarios;
        var byCapability = SampleCatalog.Categories.SelectMany(c => ScenarioGallery.Filter(c.Id, null)).ToList();
        var byDomain = ScenarioGallery.Domains.SelectMany(d => ScenarioGallery.Filter(null, d)).Distinct().ToList();

        Assert.Equal(all, ScenarioGallery.Filter(null, null));
        Assert.Equal(all.Count, byCapability.Count);
        Assert.Equal(all.ToHashSet(), byCapability.ToHashSet());
        Assert.Equal(all.ToHashSet(), byDomain.ToHashSet());
        Assert.All(ScenarioGallery.Domains, d => Assert.NotEmpty(ScenarioGallery.Filter(null, d)));
    }

    [Fact]
    public void The_gallery_page_reads_both_filters_from_the_address_and_names_one_canonical_address()
    {
        var page = DemosFile("Pages/PlaygroundIndex.razor");

        Assert.Contains("@page \"/playground\"", page, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"capability\")]", page, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"domain\")]", page, StringComparison.Ordinal);
        Assert.Contains("<CanonicalLink Path=\"playground\" />", page, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@Current(", page, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_invoice_card_carries_the_domains_of_every_type_it_makes()
    {
        var domains = ScenarioGallery.DomainsOf(SampleCatalog.Invoice);

        Assert.All(SampleCatalog.InvoiceTypes.SelectMany(t => t.Domains), d => Assert.Contains(d, domains));
        Assert.Contains(SampleCatalog.Invoice, ScenarioGallery.Filter("documents", "Logistics"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("payroll")]
    [InlineData("<script>")]
    public void Unknown_filter_values_show_every_scenario(string? value)
    {
        Assert.Null(ScenarioGallery.FindCapability(value));
        Assert.Null(ScenarioGallery.FindDomain(value));
        Assert.Equal("merge", ScenarioGallery.FindCapability(" MERGE "));
        Assert.Equal("HR", ScenarioGallery.FindDomain("hr"));
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    private static string? Query(string address, string name)
    {
        var match = Regex.Match(address, $@"[?&]{name}=([^&]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    #endregion

    #endregion
}
