using System.Runtime.CompilerServices;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The free tools catalog lists the six shipped tools in their three groups, and each tool's matching scenarios are
/// real pages of the site (26112201 AC-03, AC-06, AC-07; PR-012).
/// </summary>
public class ToolCatalogTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_hub_lists_six_tools_in_three_groups()
    {
        Assert.Equal([ToolGroup.Combine, ToolGroup.SplitAndExtract, ToolGroup.Convert], ToolCatalog.Groups);
        Assert.Equal(["Merge PDFs", "Images to PDF"], ToolCatalog.InGroup(ToolGroup.Combine).Select(t => t.Name));
        Assert.Equal(["Split PDF", "Extract pages", "Remove pages"], ToolCatalog.InGroup(ToolGroup.SplitAndExtract).Select(t => t.Name));
        Assert.Equal(["HTML to PDF"], ToolCatalog.InGroup(ToolGroup.Convert).Select(t => t.Name));
        Assert.Equal(["Combine", "Split and extract", "Convert"], ToolCatalog.Groups.Select(ToolCatalog.GroupName));
    }

    [Fact]
    public void Each_tool_has_its_own_address_under_tools()
    {
        Assert.Equal(
            ["tools/merge", "tools/images-to-pdf", "tools/split", "tools/extract-pages", "tools/remove-pages", "tools/html-to-pdf"],
            ToolCatalog.All.Select(t => t.Route));
        Assert.Equal(ToolCatalog.All.Count, ToolCatalog.All.Select(t => t.Route).Distinct().Count());
    }

    [Fact]
    public void Each_tool_names_what_goes_in_and_what_comes_out_and_has_three_steps()
    {
        foreach (var tool in ToolCatalog.All)
        {
            Assert.Contains("result:", tool.InputOutput, StringComparison.Ordinal);
            Assert.Equal(3, tool.Steps.Count);
            Assert.NotEmpty(tool.SearchWords);
            Assert.True(tool.Subtitle.Length <= 160, $"{tool.Name}: the meta description is longer than 160 characters.");
            Assert.True(tool.DocumentTitle.Length <= 70, $"{tool.Name}: the page title is longer than 70 characters.");
        }
    }

    [Fact]
    public void Each_matching_scenario_is_a_page_of_the_site_and_links_back_to_its_tool()
    {
        foreach (var tool in ToolCatalog.All)
        {
            Assert.NotEmpty(tool.Scenarios);
            foreach (var scenario in tool.Scenarios)
            {
                Assert.Contains(scenario, SampleCatalog.Categories.SelectMany(c => c.Demos));
                Assert.Same(tool, ToolCatalog.ForScenario(scenario));
                Assert.True(PageExists(scenario.Route), $"{scenario.Route} has no @page.");
            }
        }

        Assert.Equal(7, ToolCatalog.All.Sum(t => t.Scenarios.Count));
        Assert.Null(ToolCatalog.ForScenario(SampleCatalog.Invoice));
    }

    [Fact]
    public void The_limits_the_pages_state_match_the_limits_enforced()
    {
        Assert.Equal(50L * 1024 * 1024, ToolLimits.MaxPdfBytes);
        Assert.Equal(20, ToolLimits.MaxFiles);
        Assert.Equal(200L * 1024 * 1024, ToolLimits.MaxTotalBytes);
        Assert.Equal(10L * 1024 * 1024, ToolLimits.MaxPictureBytes);
        Assert.Equal(50_000_000, ToolLimits.MaxJpegPixels);
        Assert.Equal(4096L * 4096, ToolLimits.MaxPngPixels);
        Assert.Equal("Up to 20 files, 50 MB each and 200 MB in total.", ToolLimits.PdfLimitsLine);
        Assert.Equal("These files add up to 150 MB. One run takes up to 200 MB in total.", ToolLimits.TotalTooLarge(150L * 1024 * 1024));
        Assert.Equal("1 MB", ToolLimits.Megabytes(1));
    }

    #endregion

    #region Private Methods

    private static bool PageExists(string route, [CallerFilePath] string source = "")
    {
        var pages = Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", "Pages");
        var directive = $"@page \"/{route}\"";
        return Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
            .Any(file => File.ReadAllText(file).Contains(directive, StringComparison.Ordinal));
    }

    #endregion

    #endregion
}
