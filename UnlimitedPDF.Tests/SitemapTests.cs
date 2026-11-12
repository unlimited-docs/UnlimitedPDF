using System.Runtime.CompilerServices;
using System.Xml.Linq;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos site's <c>sitemap.xml</c> and <c>robots.txt</c>: the sitemap lists every page once on the site's own
/// address, and robots.txt lets crawlers in and points them at it (QF-26111203).
/// </summary>
public class SitemapTests
{
    #region Fields and Constants

    // The demos site's public address. Every sitemap URL and the robots.txt Sitemap line use it.
    private const string SiteAddress = "https://unlimitedpdf.dev/";

    private static readonly XNamespace Sitemaps = "http://www.sitemaps.org/schemas/sitemap/0.9";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Sitemap_lists_the_home_page_the_faq_and_every_demo_once()
    {
        var sitemap = XDocument.Parse(DemosFile("wwwroot/sitemap.xml"));
        var urls = sitemap.Root!.Elements(Sitemaps + "url").Select(u => u.Element(Sitemaps + "loc")!.Value).ToList();
        var expected = new[] { "", "faq" }
            .Concat(SampleCatalog.Categories.SelectMany(c => c.Demos).Select(d => d.Route))
            .Distinct()
            .Select(path => SiteAddress + path);

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
        Assert.Equal(new[] { "Sitemap: " + SiteAddress + "sitemap.xml" }, lines.Where(l => l.StartsWith("Sitemap:", StringComparison.Ordinal)));
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
