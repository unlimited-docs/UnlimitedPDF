using System.Runtime.CompilerServices;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The free tools' pages and the ways to them: the hub and one page per tool at its own address, each with its title,
/// canonical link, three steps, and the hidden file input; the Tools item in the top bar and the phone menu; the home
/// strip; the links between scenarios and tools; and site search (26112201 AC-03, AC-04, AC-07, AC-12, AC-13).
/// </summary>
public class ToolPagesTests
{
    #region Fields and Constants

    private static readonly Dictionary<string, string> PageFiles = new()
    {
        ["merge"] = "MergeTool.razor",
        ["images-to-pdf"] = "ImagesToPdfTool.razor",
        ["split"] = "SplitTool.razor",
        ["extract-pages"] = "ExtractPagesTool.razor",
        ["remove-pages"] = "RemovePagesTool.razor",
        ["html-to-pdf"] = "HtmlToPdfTool.razor",
    };

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void The_hub_has_its_own_address_title_and_canonical_link()
    {
        string hub = DemosFile("Pages/Tools/ToolsIndex.razor");

        Assert.Contains("@page \"/tools\"", hub, StringComparison.Ordinal);
        Assert.Contains("<PageTitle>@ToolCatalog.HubDocumentTitle</PageTitle>", hub, StringComparison.Ordinal);
        Assert.Contains("<CanonicalLink Path=\"@ToolCatalog.HubRoute\" />", hub, StringComparison.Ordinal);
        Assert.Contains("ToolCatalog.InGroup(group)", hub, StringComparison.Ordinal);
        Assert.Contains("@tool.InputOutput", hub, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_tool_page_has_its_address_title_three_steps_and_a_hidden_file_input()
    {
        Assert.Equal(ToolCatalog.All.Select(t => t.Slug).Order(), PageFiles.Keys.Order());
        foreach (var tool in ToolCatalog.All)
        {
            string page = DemosFile($"Pages/Tools/{PageFiles[tool.Slug]}");
            Assert.Contains($"@page \"/{tool.Route}\"", page, StringComparison.Ordinal);
            Assert.Contains("<PageTitle>@Tool.DocumentTitle</PageTitle>", page, StringComparison.Ordinal);
            Assert.Contains("<PageDescription Text=\"@Tool.Subtitle\" />", page, StringComparison.Ordinal);
            Assert.Contains("<CanonicalLink Path=\"@Tool.Route\" />", page, StringComparison.Ordinal);
            Assert.Contains("<ToolLanding ", page, StringComparison.Ordinal);
            Assert.Contains("<ToolActionBar ", page, StringComparison.Ordinal);
            Assert.Contains("<ToolDone ", page, StringComparison.Ordinal);
            Assert.Contains("class=\"visually-hidden\" tabindex=\"-1\" aria-hidden=\"true\"", page, StringComparison.Ordinal);
            Assert.Contains("id=\"tool-heading\" tabindex=\"-1\"", page, StringComparison.Ordinal);
            Assert.DoesNotContain("MarkupString", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_shared_parts_show_the_privacy_line_and_never_show_visitor_text_as_markup()
    {
        string landing = DemosFile("Components/Tools/ToolLanding.razor");
        Assert.Contains("@ToolLimits.PrivacyLine", landing, StringComparison.Ordinal);
        Assert.Contains("Free, no sign-up. @LimitsLine", landing, StringComparison.Ordinal);
        Assert.Contains("@Tool.Scenario.Route", landing, StringComparison.Ordinal);
        Assert.Contains("@Scenario.Route", DemosFile("Components/Tools/ToolDone.razor"), StringComparison.Ordinal);

        foreach (string part in new[] { "ToolLanding", "ToolDone", "ToolActionBar", "FileCards", "PageGrid", "ToolFileErrors" })
            Assert.DoesNotContain("MarkupString", DemosFile($"Components/Tools/{part}.razor"), StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_sit_after_scenarios_in_the_top_bar_and_the_phone_menu()
    {
        string layout = DemosFile("Layout/MainLayout.razor");

        Assert.Contains(
            "<NavLink class=\"topnav-link\" href=\"playground\" Match=\"NavLinkMatch.Prefix\">Scenarios</NavLink>\n            <NavLink class=\"topnav-link\" href=\"tools\" Match=\"NavLinkMatch.Prefix\">Tools</NavLink>",
            layout.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains(
            "<NavLink class=\"site-menu-link\" href=\"playground\" Match=\"NavLinkMatch.Prefix\">Scenarios</NavLink>\n        <NavLink class=\"site-menu-link\" href=\"tools\" Match=\"NavLinkMatch.Prefix\">Tools</NavLink>",
            layout.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_home_page_shows_the_tools_path_after_the_banner_and_before_the_documents()
    {
        // 26112201 AC-07, now the "For your files" path beside the developer path (QF-26112201).
        string home = DemosFile("Pages/Home.razor");
        int banner = home.IndexOf("class=\"showcase\"", StringComparison.Ordinal);
        int strip = home.IndexOf("aria-labelledby=\"home-tools-title\"", StringComparison.Ordinal);
        int documents = home.IndexOf("aria-labelledby=\"documents-title\"", StringComparison.Ordinal);

        Assert.True(banner >= 0 && strip > banner && documents > strip, "The tools path sits after the banner and before the documents.");
        Assert.Contains("For your files", home, StringComparison.Ordinal);
        Assert.Contains("@ToolCatalog.HubHeading that run in your browser. Nothing is uploaded.", home, StringComparison.Ordinal);
        Assert.Contains("@foreach (var tool in ToolCatalog.All)", home, StringComparison.Ordinal);
        Assert.Contains("href=\"@ToolCatalog.HubRoute\">All tools", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_matching_scenario_links_to_its_tool_under_its_description()
    {
        foreach (string frame in new[] { "Components/SamplePage.razor", "Components/SplitPartsPage.razor" })
        {
            string text = DemosFile(frame).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("<p class=\"demo-lead\">@Demo.Description</p>\n        <ScenarioToolLink Demo=\"Demo\" />", text, StringComparison.Ordinal);
        }

        string link = DemosFile("Components/Tools/ScenarioToolLink.razor");
        Assert.Contains("ToolCatalog.ForScenario(Demo) is { } tool", link, StringComparison.Ordinal);
        Assert.Contains("href=\"@tool.Route\"", link, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("merge", "Merge PDFs")]
    [InlineData("combine pdf", "Merge PDFs")]
    [InlineData("jpg", "Images to PDF")]
    [InlineData("split", "Split PDF")]
    [InlineData("extract", "Extract pages")]
    [InlineData("remove pages", "Remove pages")]
    [InlineData("delete pages", "Remove pages")]
    [InlineData("html", "HTML to PDF")]
    public void Site_search_finds_each_tool_by_name_and_task_words(string query, string tool)
    {
        var results = SiteSearch.Find(query, _ => string.Empty);

        Assert.Contains(results, r => r.Kind == SearchKind.Tool && r.Title == tool && r.Address == ToolCatalog.All.Single(t => t.Name == tool).Route);
    }

    [Fact]
    public void Site_search_finds_every_tool_by_its_name()
    {
        foreach (var tool in ToolCatalog.All)
            Assert.Contains(SiteSearch.Find(tool.Name, _ => string.Empty), r => r.Kind == SearchKind.Tool && r.Address == tool.Route);
    }

    [Fact]
    public void Tool_addresses_open_directly_and_the_page_can_reach_no_other_site()
    {
        string config = DemosFile("wwwroot/staticwebapp.config.json");

        // Every tool address is served by the app on direct open or reload: none is excluded from the fallback.
        Assert.Contains("\"rewrite\": \"/index.html\"", config, StringComparison.Ordinal);
        Assert.DoesNotContain("\"/tools", config, StringComparison.Ordinal);
        // Files never leave the browser: the page may connect only to the site itself (AC-10; OWASP A02).
        Assert.Contains("connect-src 'self';", config, StringComparison.Ordinal);
        Assert.Contains("img-src 'self' data: blob:;", config, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    #endregion

    #endregion
}
