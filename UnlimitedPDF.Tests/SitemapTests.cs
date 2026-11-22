using System.Runtime.CompilerServices;
using System.Xml.Linq;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos site's <c>sitemap.xml</c> and <c>robots.txt</c>: the sitemap lists every page once on the site's own
/// address, and robots.txt lets crawlers in and points them at it (QF-26111203); <c>sitemap.md</c> lists the same pages
/// for people and agents, and index.html gives link previews their title, description, and picture (QF-26111702).
/// </summary>
public class SitemapTests
{
    #region Fields and Constants

    private static readonly XNamespace Sitemaps = "http://www.sitemaps.org/schemas/sitemap/0.9";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Sitemap_lists_the_home_page_the_faq_and_every_demo_once()
    {
        var sitemap = XDocument.Parse(DemosFile("wwwroot/sitemap.xml"));
        var urls = sitemap.Root!.Elements(Sitemaps + "url").Select(u => u.Element(Sitemaps + "loc")!.Value).ToList();
        var expected = new[] { "", "playground", "faq", ToolCatalog.HubRoute }
            .Concat(SampleCatalog.Pages.Select(d => d.Route))
            .Concat(ToolCatalog.All.Select(t => t.Route))
            .Distinct()
            .Select(SiteLinks.For);

        Assert.Equal(Sitemaps + "urlset", sitemap.Root.Name);
        Assert.Equal(expected, urls);
        Assert.DoesNotContain(urls, u => u.Contains("not-found", StringComparison.Ordinal));
    }

    [Fact]
    public void Robots_txt_allows_crawling_and_points_at_the_sitemap()
    {
        // Crawlers need the app files under _framework/ to render the pages, so nothing is disallowed.
        var lines = DemosFile("wwwroot/robots.txt").Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        Assert.Contains("User-agent: *", lines);
        Assert.Contains("Allow: /", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { "Sitemap: " + SiteLinks.For("sitemap.xml") }, lines.Where(l => l.StartsWith("Sitemap:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Index_html_loads_only_files_the_app_serves()
    {
        // Every page starts from index.html, so a file it names that doesn't exist fails on every page load. The app
        // has no scoped CSS (.razor.css), so there is no generated styles bundle to link (QF-26111205).
        var index = DemosFile("wwwroot/index.html");
        var targets = System.Text.RegularExpressions.Regex.Matches(index, "(?:href|src)=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .Where(t => !t.StartsWith("http", StringComparison.Ordinal) && t != "/" && t != "." && !t.StartsWith("_framework/", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(targets);
        Assert.All(targets, t => Assert.True(File.Exists(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/wwwroot/" + t)), $"index.html names '{t}', which wwwroot does not have."));
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        Assert.DoesNotContain(
            Directory.GetFiles(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm"), "*.razor.css", SearchOption.AllDirectories),
            f => !f.Contains(obj, StringComparison.Ordinal));
    }

    [Fact]
    public void Sitemap_md_lists_every_sitemap_page_once_with_its_title_and_description()
    {
        // The Markdown site map is for people and agents: the same pages as sitemap.xml, in the same order, each as
        // "- [title](address): description", with each demo's own title and description (QF-26111702).
        var sitemap = XDocument.Parse(DemosFile("wwwroot/sitemap.xml"));
        var urls = sitemap.Root!.Elements(Sitemaps + "url").Select(u => u.Element(Sitemaps + "loc")!.Value).ToList();
        var entries = System.Text.RegularExpressions.Regex.Matches(DemosFile("wwwroot/sitemap.md"), @"^- \[([^\]]+)\]\(([^)]+)\): (.+?)\r?$", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => (Title: m.Groups[1].Value, Url: m.Groups[2].Value, Description: m.Groups[3].Value))
            .ToList();

        Assert.Equal(urls, entries.Select(e => e.Url));
        foreach (var demo in SampleCatalog.Pages)
        {
            var entry = entries.Single(e => e.Url == SiteLinks.For(demo.Route));
            Assert.Equal(demo.PageTitle, entry.Title);
            Assert.Equal(demo.Description, entry.Description);
        }
        foreach (var tool in ToolCatalog.All)
        {
            var entry = entries.Single(e => e.Url == SiteLinks.For(tool.Route));
            Assert.Equal(tool.PageTitle, entry.Title);
            Assert.Equal(tool.Subtitle, entry.Description);
        }
        Assert.Equal(ToolCatalog.HubSubtitle, entries.Single(e => e.Url == SiteLinks.For(ToolCatalog.HubRoute)).Description);
        foreach (var (path, page) in new[] { ("", "Pages/Home.razor"), ("playground", "Pages/PlaygroundIndex.razor"), ("faq", "Pages/Faq.razor") })
        {
            var entry = entries.Single(e => e.Url == SiteLinks.For(path));
            Assert.Contains($"<PageDescription Text=\"{entry.Description}\" />", DemosFile(page), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Robots_txt_llms_txt_and_index_html_point_at_the_markdown_site_map()
    {
        Assert.Contains("# Every page as a Markdown list with titles and descriptions: " + SiteLinks.For("sitemap.md"), DemosFile("wwwroot/robots.txt"), StringComparison.Ordinal);
        Assert.Contains("- [Site map](" + SiteLinks.For("sitemap.md") + "):", DemosFile("wwwroot/llms.txt"), StringComparison.Ordinal);
        Assert.Contains("<a href=\"sitemap.md\">Site map</a>", DemosFile("wwwroot/index.html"), StringComparison.Ordinal);
        Assert.Contains("\".md\": \"text/markdown; charset=utf-8\"", DemosFile("wwwroot/staticwebapp.config.json"), StringComparison.Ordinal);
    }

    [Fact]
    public void Index_html_gives_link_previews_the_home_page_title_description_and_picture()
    {
        // Link previews read index.html only; they show the home page's own title and description and its picture.
        var index = DemosFile("wwwroot/index.html");
        string Meta(string attribute, string name) =>
            System.Text.RegularExpressions.Regex.Match(index, $"<meta {attribute}=\"{name}\" content=\"([^\"]*)\" />").Groups[1].Value;
        string title = System.Text.RegularExpressions.Regex.Match(index, "<title>([^<]*)</title>").Groups[1].Value;

        Assert.Equal(title, Meta("property", "og:title"));
        Assert.Equal(Meta("name", "description"), Meta("property", "og:description"));
        Assert.Equal("website", Meta("property", "og:type"));
        Assert.Equal("summary_large_image", Meta("name", "twitter:card"));
        string image = Meta("property", "og:image");
        Assert.StartsWith(SiteLinks.Address, image, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/wwwroot/" + image[SiteLinks.Address.Length..])), image);
        Assert.Contains($"alt=\"{Meta("property", "og:image:alt")}\"", DemosFile("Pages/Home.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain("og:url", index, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative) =>
        File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + relative));

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
