using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The site-wide search and the scenario gallery (26111701 AC-06, T-11): search finds every demo, FAQ answer, and a
/// demo's code, with the query matched as text and shown as text (A05); the gallery's capability and domain filters
/// together reach every scenario and keep their choice in the address; the layout has the search box, the phone menu
/// drawer, and Ctrl+K.
/// </summary>
public class SiteSearchTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_code_word_finds_the_demos_whose_code_holds_it_with_the_matching_line()
    {
        var results = SiteSearch.Find("AddPages", Source).Where(r => r.Kind == SearchKind.Code).ToList();

        Assert.NotEmpty(results);
        Assert.InRange(results.Count, 1, SiteSearch.MaxPerKind);
        Assert.All(results, r =>
        {
            Assert.Contains("AddPages", r.Detail, StringComparison.OrdinalIgnoreCase);
            var demo = SampleCatalog.Pages.First(d => d.Route == r.Address);
            Assert.Contains("AddPages", Source(demo.SourceFile), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void A_hostile_query_is_matched_and_shown_as_plain_text()
    {
        const string query = "<img src=x onerror=alert(1)>";

        var parts = SiteSearch.Highlight("before " + query + " after", query);

        // The parts rebuild the text exactly: nothing is added, removed, or turned into markup.
        Assert.Equal("before " + query + " after", string.Concat(parts.Select(p => p.Text)));
        Assert.Contains(parts, p => p.IsMatch && p.Text.Contains("<img", StringComparison.Ordinal));
        Assert.Empty(SiteSearch.Find(query, Source));

        foreach (var file in new[] { "Components/SiteSearchBox.razor", "Components/SiteSearchBox.razor.cs", "Components/HighlightedText.razor", "Pages/PlaygroundIndex.razor", "Layout/MainLayout.razor" })
            Assert.DoesNotContain("MarkupString", DemosFile(file), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_or_blank_query_finds_nothing()
    {
        Assert.Empty(SiteSearch.Find(null, Source));
        Assert.Empty(SiteSearch.Find("   \t ", Source));
    }

    [Fact]
    public void Every_demo_page_is_found_by_its_title()
    {
        Assert.All(SampleCatalog.Pages, demo =>
            Assert.Contains(SiteSearch.Find(demo.Title, Source), r => r.Kind == SearchKind.Demo && r.Address == demo.Route));
    }

    [Fact]
    public void Every_faq_question_is_found_by_its_text_and_opens_its_answer()
    {
        Assert.All(FaqQuestions.Questions, question =>
            Assert.Contains(SiteSearch.Find(question.Text, Source), r => r.Kind == SearchKind.Faq && r.Address == "faq#" + question.Id));
    }

    [Fact]
    public void Highlight_marks_every_word_ignoring_case_and_keeps_the_text()
    {
        var parts = SiteSearch.Highlight("Merge PDFs, then merge again", "MERGE pdf");

        Assert.Equal("Merge PDFs, then merge again", string.Concat(parts.Select(p => p.Text)));
        Assert.Equal(new[] { "Merge", "PDF", "merge" }, parts.Where(p => p.IsMatch).Select(p => p.Text));
        Assert.Equal(new[] { new TextPart("no match", false) }, SiteSearch.Highlight("no match", "zzz"));
        Assert.Equal(new[] { new TextPart("abc", true) }, SiteSearch.Highlight("abc", "ab bc"));
    }

    [Fact]
    public void Results_hold_every_word_and_come_grouped_demos_then_faq_then_code()
    {
        var results = SiteSearch.Find("bank statement", Source);
        var kinds = results.Select(r => r.Kind).ToList();

        Assert.Contains(results, r => r.Kind == SearchKind.Demo && r.Address == SampleCatalog.AccountStatement.Route);
        Assert.Equal(kinds.OrderBy(k => k).ToList(), kinds);
        Assert.All(results.GroupBy(r => r.Kind), group => Assert.InRange(group.Count(), 1, SiteSearch.MaxPerKind));
    }

    [Fact]
    public void The_layout_has_the_search_the_phone_menu_drawer_and_ctrl_k()
    {
        var layout = DemosFile("Layout/MainLayout.razor");
        var code = DemosFile("Layout/MainLayout.razor.cs");
        var script = DemosFile("wwwroot/js/app.js");

        Assert.Equal(2, Regex.Matches(layout, "<SiteSearchBox ").Count);
        Assert.Contains("<dialog id=\"site-menu\" class=\"site-menu\" aria-labelledby=\"site-menu-title\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-site-menu-open aria-haspopup=\"dialog\" aria-controls=\"site-menu\"", layout, StringComparison.Ordinal);
        Assert.Contains("Interop.ShowDialogAsync(_menu", code, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged += OnLocationChanged", code, StringComparison.Ordinal);
        Assert.Contains("export function initSiteSearch(root, listener)", script, StringComparison.Ordinal);
        Assert.Contains("event.key.toLowerCase() !== \"k\"", script, StringComparison.Ordinal);
        Assert.Contains("event.key === \"Escape\"", script, StringComparison.Ordinal);

        // Below 900 px the box and the links give way to Menu.
        Assert.Matches(@"@media \(max-width: 899\.98px\) \{\s*\.topbar-search, \.topnav \{ display: none; \}\s*\.topbar-menu \{ display: inline-flex; \}", DemosFile("wwwroot/css/app.css"));
    }

    [Fact]
    public void Words_are_limited_in_length_count_and_repeats()
    {
        Assert.Equal(new[] { new string('a', SiteSearch.MaxQueryLength) }, SiteSearch.Words(new string('a', 500)));
        Assert.Equal(SiteSearch.MaxWords, SiteSearch.Words(string.Join(' ', Enumerable.Range(0, 20).Select(i => "w" + i))).Length);
        Assert.Equal(new[] { "pdf" }, SiteSearch.Words("pdf PDF Pdf"));
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    // The demo's sample source, as the app embeds it.
    private static string Source(string file) => DemosFile("Samples/" + file).Replace("\r\n", "\n");

    #endregion

    #endregion
}
