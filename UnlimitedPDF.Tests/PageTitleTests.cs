using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos site's page titles, meta descriptions, and <c>llms.txt</c>: what search results and agents read about each
/// page stays specific, short enough to show in full, and in step with the demo list (QF-26111202).
/// </summary>
public class PageTitleTests
{
    #region Fields and Constants

    // Search results show about 60 characters of a title and about 160 of a description.
    private const int MaxTitleLength = 60;
    private const int MaxDescriptionLength = 160;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Demo_page_titles_name_the_task_are_unique_and_fit_search_results()
    {
        var demos = AllDemos();

        Assert.All(demos, d =>
        {
            Assert.Contains("PDF", d.PageTitle, StringComparison.Ordinal);
            Assert.EndsWith(" · UnlimitedPDF", d.DocumentTitle, StringComparison.Ordinal);
            Assert.True(d.DocumentTitle.Length <= MaxTitleLength, $"'{d.DocumentTitle}' is {d.DocumentTitle.Length} characters.");
        });
        Assert.Equal(demos.Count, demos.Select(d => d.PageTitle).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Demo_pages_use_the_demo_title_and_description()
    {
        foreach (var frame in new[] { "Components/SamplePage.razor", "Components/SplitPartsPage.razor" })
        {
            var source = DemosFile(frame);

            Assert.Contains("<PageTitle>@Demo.DocumentTitle</PageTitle>", source, StringComparison.Ordinal);
            Assert.Contains("<PageDescription Text=\"@Demo.Description\" />", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Site_pages_have_a_short_title_and_description_of_their_own()
    {
        var pages = new[] { "Pages/Home.razor", "Pages/Faq.razor", "Pages/NotFound.razor" }.Select(DemosFile).ToList();
        var titles = pages.Select(Title).ToList();

        Assert.All(titles, t => Assert.True(t.Length <= MaxTitleLength, $"'{t}' is {t.Length} characters."));
        Assert.All(pages.Select(Description), d => Assert.InRange(d.Length, 1, MaxDescriptionLength));
        var everyTitle = titles.Concat(AllDemos().Select(d => d.DocumentTitle)).ToList();
        Assert.Equal(everyTitle.Count, everyTitle.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("<meta name=\"robots\" content=\"noindex\" />", pages[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Index_html_has_one_title_and_description_and_they_match_the_home_page()
    {
        // Readers that don't run the app (agents, some crawlers) see only index.html, so it carries the home page's own
        // title and description, and PageDescription updates that one tag instead of adding a second.
        var index = DemosFile("wwwroot/index.html");
        var home = DemosFile("Pages/Home.razor");

        Assert.Single(Regex.Matches(index, "<title>"));
        Assert.Single(Regex.Matches(index, "<meta name=\"description\""));
        Assert.Equal(Title(home), Regex.Match(index, "<title>(.*?)</title>").Groups[1].Value);
        Assert.Equal(Description(home), Regex.Match(index, "<meta name=\"description\" content=\"(.*?)\" />").Groups[1].Value);
        Assert.Contains("<a href=\"llms.txt\">", index, StringComparison.Ordinal);
        Assert.Contains("export function setDescription(text)", DemosFile("wwwroot/js/app.js"), StringComparison.Ordinal);
    }

    [Fact]
    public void Pages_name_their_canonical_address_on_the_site()
    {
        // Each page adds its own canonical link as it opens. index.html has none: every address loads it first, so a
        // tag there would name the home page as the address of every page (QF-26111204).
        var canonical = new Dictionary<string, string>
        {
            ["Components/SamplePage.razor"] = "<CanonicalLink Path=\"@Demo.Route\" />",
            ["Components/SplitPartsPage.razor"] = "<CanonicalLink Path=\"@Demo.Route\" />",
            ["Pages/Home.razor"] = "<CanonicalLink Path=\"\" />",
            ["Pages/Faq.razor"] = "<CanonicalLink Path=\"faq\" />"
        };

        foreach (var (file, tag) in canonical)
        {
            var source = DemosFile(file);
            Assert.Contains(tag, source, StringComparison.Ordinal);

            // Only one HeadContent shows at a time, so a page with a canonical link adds no other.
            Assert.DoesNotContain("<HeadContent>", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CanonicalLink", DemosFile("Pages/NotFound.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"canonical\"", DemosFile("wwwroot/index.html"), StringComparison.Ordinal);
        Assert.Contains("<link rel=\"canonical\" href=\"@SiteLinks.For(Path)\" />", DemosFile("Components/CanonicalLink.razor"), StringComparison.Ordinal);
        Assert.Equal("https://unlimitedpdf.dev/", SiteLinks.For(""));
        Assert.Equal("https://unlimitedpdf.dev/playground/documents/invoice", SiteLinks.For("/" + SampleCatalog.Invoice.Route));
    }

    [Fact]
    public void Llms_txt_lists_every_demo_with_its_title_and_description()
    {
        var text = DemosFile("wwwroot/llms.txt").Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Split('\n');

        Assert.Equal("# UnlimitedPDF", lines[0]);
        Assert.StartsWith("> ", lines.First(l => l.Length > 0 && l != lines[0]), StringComparison.Ordinal);
        Assert.All(AllDemos(), d => Assert.Contains($"- [{d.PageTitle}]({SiteLinks.For(d.Route)}): {d.Description}\n", text, StringComparison.Ordinal));
        Assert.Contains($"]({SiteLinks.For("faq")})", text, StringComparison.Ordinal);

        // Every link is absolute, because agents often keep the text without the file's own address (QF-26111204).
        // Site links are pages the app has; the rest point at the project's own GitHub and NuGet pages.
        var routes = AllDemos().Select(d => d.Route).Concat(["faq", ""]).Select(SiteLinks.For).ToHashSet(StringComparer.Ordinal);
        foreach (Match link in Regex.Matches(text, @"\]\(([^)]+)\)"))
        {
            var target = link.Groups[1].Value;
            Assert.True(
                routes.Contains(target)
                || target.StartsWith("https://github.com/unlimited-docs/UnlimitedPDF", StringComparison.Ordinal)
                || target.StartsWith("https://www.nuget.org/packages/UnlimitedPDF", StringComparison.Ordinal),
                $"Unknown link target '{target}'.");
        }
    }

    [Fact]
    public void Llms_txt_installs_the_current_package_version()
    {
        // llms.txt is a static file, so its PackageReference is typed by hand; it must match the package version, the
        // way the readmes do (QF-26111306).
        var project = File.ReadAllText(RepositoryFile("UnlimitedPDF/UnlimitedPDF.csproj"));
        string version = Regex.Match(project, "<Version>([^<]+)</Version>").Groups[1].Value;

        Assert.NotEmpty(version);
        Assert.Contains($"<PackageReference Include=\"UnlimitedPDF\" Version=\"{version}\" />", DemosFile("wwwroot/llms.txt"), StringComparison.Ordinal);
        foreach (var readme in new[] { "README.md", "nuget/README.md" })
        {
            var text = File.ReadAllText(RepositoryFile(readme));
            Assert.Contains($"dotnet add package UnlimitedPDF --version {version}`", text, StringComparison.Ordinal);
            Assert.Contains($"<PackageReference Include=\"UnlimitedPDF\" Version=\"{version}\" />", text, StringComparison.Ordinal);
        }
    }

    #endregion

    #region Private Methods

    private static List<SampleDemo> AllDemos() => SampleCatalog.Categories.SelectMany(c => c.Demos).Distinct().ToList();

    private static string Description(string page) => Regex.Match(page, "<PageDescription Text=\"(.*?)\" />").Groups[1].Value;

    private static string DemosFile(string relative) =>
        File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + relative));

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    private static string Title(string page) => Regex.Match(page, "<PageTitle>(.*?)</PageTitle>").Groups[1].Value;

    #endregion

    #endregion
}
