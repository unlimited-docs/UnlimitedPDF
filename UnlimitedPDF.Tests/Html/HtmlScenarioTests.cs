using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using static UnlimitedPDF.Tests.Html.HtmlLayoutTests;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The known HTML to PDF problem scenarios (WR-038): the catalogue is complete and well formed, every scenario builds
/// through the playground's sample code with exactly its expected warnings, and each shows its stated outcome on the
/// drawn content (14-html-to-pdf AC-27, V-26, V-27).
/// </summary>
public class HtmlScenarioTests
{
    #region Fields and Constants

    // The sample's body: 36 pt margins, and an 18 pt footer above the bottom margin.
    private const double BodyBottom = 36 + 18;

    #endregion

    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> Ids() => HtmlScenarios.All.Select(s => new object[] { s.Id });

    [Fact]
    public void The_catalogue_has_28_scenarios_in_eight_groups()
    {
        Assert.Equal(28, HtmlScenarios.All.Count);
        Assert.Equal(HtmlScenarios.All.Count, HtmlScenarios.All.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(HtmlScenarios.All, s => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", s.Id));
        Assert.Equal(HtmlScenarios.Groups, HtmlScenarios.All.Select(s => s.Group).Distinct());
        Assert.Equal(new[] { 8, 4, 3, 4, 3, 2, 3, 1 }, HtmlScenarios.Groups.Select(g => HtmlScenarios.All.Count(s => s.Group == g)));
        Assert.All(HtmlScenarios.All, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.False(string.IsNullOrWhiteSpace(s.Problem));
            Assert.False(string.IsNullOrWhiteSpace(s.Outcome));
            Assert.False(string.IsNullOrWhiteSpace(s.Html));
        });
        Assert.Equal(new[] { "wide-pre", "non-latin-characters", "svg-and-webp", "flex-and-grid", "backgrounds" },
            HtmlScenarios.All.Where(s => s.IsLimitation).Select(s => s.Id));
    }

    [Fact]
    public void Scenarios_name_only_images_the_demos_and_tests_supply()
    {
        // The playground and the tests resolve logo.png and banner.png; everything else is inline or meant to be missing.
        var sources = HtmlScenarios.All.SelectMany(s => Regex.Matches(s.Html, "src=\"([^\"]*)\"").Select(m => m.Groups[1].Value));
        Assert.All(sources, src => Assert.True(src is "logo.png" or "banner.png" or "photos/team-photo.jpg" || src.StartsWith("data:", StringComparison.Ordinal), src));
    }

    [Fact]
    public void Find_matches_ids_exactly()
    {
        Assert.Same(HtmlScenarios.All[3], HtmlScenarios.Find(HtmlScenarios.All[3].Id));
        Assert.Null(HtmlScenarios.Find(null));
        Assert.Null(HtmlScenarios.Find("ROW-CUT-IN-HALF"));
        Assert.Null(HtmlScenarios.Find("<b>row-cut-in-half</b>"));
        Assert.Null(HtmlScenarios.Find(" row-cut-in-half"));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public async Task Every_scenario_builds_with_its_expected_warnings(string id)
    {
        var result = await Convert(id);
        var scenario = HtmlScenarios.Find(id)!;
        Assert.Equal(scenario.ExpectedWarnings.Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal),
            result.Html.Warnings.Select(w => w.Kind.ToString()).OrderBy(k => k, StringComparer.Ordinal));
        AssertInsideBody(result.Pages, allowWide: id == "wide-pre");
    }

    // Page breaks

    [Fact]
    public async Task Row_cut_in_half_rows_stay_whole()
    {
        var result = await Convert("row-cut-in-half");
        Assert.Equal(2, result.Pages.Count);
        foreach (var runs in result.Runs)
        {
            int orders = runs.Count(r => r.Text.StartsWith("SO-", StringComparison.Ordinal));
            Assert.Equal(orders, runs.Count(r => r.Text.StartsWith("Replacement filter", StringComparison.Ordinal)));
            Assert.Equal(orders, runs.Count(r => r.Text.StartsWith("Delivered to", StringComparison.Ordinal)));
        }
        Assert.Equal(45, result.Runs.Sum(runs => runs.Count(r => r.Text.StartsWith("SO-", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task Header_not_repeating_header_starts_every_page()
    {
        var result = await Convert("header-not-repeating");
        Assert.True(result.Pages.Count >= 2);
        foreach (var runs in result.Runs)
        {
            var header = Assert.Single(runs, r => r.Text == "Date");
            Assert.All(runs.Where(r => r.Text.StartsWith("Card payment", StringComparison.Ordinal)), r => Assert.True(r.Y < header.Y));
        }
    }

    [Fact]
    public async Task Row_taller_than_page_splits_between_lines()
    {
        var result = await Convert("row-taller-than-page");
        Assert.True(result.Pages.Count >= 2);
        var clauses = result.Runs.SelectMany(r => r).Where(r => r.Text.StartsWith("Clause ", StringComparison.Ordinal)).Select(r => r.Text).ToList();
        Assert.Equal(Enumerable.Range(1, 90).Select(i => $"Clause {i}: the supplier keeps the goods insured until delivery."), clauses);
        Assert.All(result.Pages, page => Assert.NotEmpty(Regex.Matches(page.Content, " l S Q")));
    }

    [Fact]
    public async Task Break_inside_avoid_block_moves_whole()
    {
        var result = await Convert("break-inside-avoid");
        Assert.Equal(2, result.Pages.Count);
        Assert.Contains(result.Runs[0], r => r.Text == "Line 57 of the agreement text.");
        foreach (string line in new[] { "Signed for the buyer", "Name: ____________________", "Place: ____________________" })
            Assert.Contains(result.Runs[1], r => r.Text == line);
    }

    [Fact]
    public async Task Forced_page_breaks_start_new_pages()
    {
        var result = await Convert("forced-page-breaks");
        Assert.Equal(4, result.Pages.Count);
        string[] headings = { "Chapter 1", "Chapter 2", "Chapter 3", "Appendix" };
        for (int i = 0; i < 4; i++) Assert.Contains(result.Runs[i], r => r.Text == headings[i]);
    }

    [Fact]
    public async Task Line_sliced_lines_stay_on_one_page()
    {
        var result = await Convert("line-sliced");
        Assert.Equal(2, result.Pages.Count);
        // Each line lies wholly above the body bottom, and the paragraph continues on page 2.
        Assert.All(result.Runs[0], r => Assert.True(r.Y - r.Size * 0.25 >= BodyBottom - 0.01, $"{r.Text} at {r.Y}"));
        Assert.Contains(result.Runs[1], r => r.Text.StartsWith("Sentence", StringComparison.Ordinal) || r.Text.Contains("Sentence"));
    }

    [Fact]
    public async Task Image_split_image_moves_whole()
    {
        var result = await Convert("image-split");
        Assert.Empty(HtmlEntryPointTests.Draws(result.Pages[0]));
        var draw = Assert.Single(HtmlEntryPointTests.Draws(result.Pages[1]));
        Assert.Equal(300 * 0.75, draw.Height, 2);
        Assert.Equal(400 * 0.75, draw.Width, 2);
    }

    [Fact]
    public async Task Blank_page_before_tall_content_leaves_no_empty_page()
    {
        var result = await Convert("blank-page-before-tall-content");
        Assert.All(result.Runs, runs => Assert.NotEmpty(runs));
        Assert.Contains(result.Runs[0], r => r.Text == "Item 1");
        Assert.Contains(result.Runs[0], r => r.Text == "Report with a long table");
    }

    // Tables

    [Fact]
    public async Task Nested_tables_keep_cell_text_inside_cells()
    {
        var result = await Convert("nested-tables");
        var page = Assert.Single(result.Pages);
        var runs = result.Runs[0];
        string[] texts = { "Level 1 spans two columns", "Level 1 left", "Level 2 spans two rows", "Level 2 top", "Level 3 header", "Level 3 A", "Level 3 B" };
        Assert.All(texts, t => Assert.Single(runs, r => r.Text == t));
        // The shaded spanning cells contain their own text.
        var fills = Fills(page);
        foreach (string shaded in new[] { "Level 1 spans two columns", "Level 2 spans two rows", "Level 3 header" })
        {
            var run = runs.Single(r => r.Text == shaded);
            Assert.Contains(fills, f => run.X >= f.X - 0.01 && run.X + Width(run) <= f.X + f.Width + 0.01 && run.Y >= f.Y && run.Y <= f.Y + f.Height);
        }
    }

    [Fact]
    public async Task Email_table_800px_fits_the_page()
    {
        var result = await Convert("email-table-800px");
        Assert.All(result.Runs[0], r => Assert.True(r.X + Width(r) <= Right + 0.01, $"{r.Text} ends at {r.X + Width(r)}"));
        Assert.All(Fills(result.Pages[0]), f => Assert.True(f.X + f.Width <= Right + 0.01));
        Assert.Contains(result.Runs[0], r => r.Text.StartsWith("Visit us", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Long_word_in_cell_breaks_inside_its_cell()
    {
        var result = await Convert("long-word-in-cell");
        Assert.Empty(BodyClips(result.Pages[0]));
        var first = result.Runs[0].Single(r => r.Text.StartsWith("REF-", StringComparison.Ordinal));
        var pieces = result.Runs[0].Where(r => Math.Abs(r.X - first.X) < 0.01).ToList();
        Assert.True(pieces.Count >= 2);
        Assert.Equal("REF-2026-ABCDEFGHIJKLMNOPQRSTUVWXYZ-0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ-0123456789", string.Concat(pieces.Select(p => p.Text)));
        var paid = result.Runs[0].Single(r => r.Text.StartsWith("Paid", StringComparison.Ordinal));
        Assert.All(pieces, p => Assert.True(p.X + Width(p) <= paid.X, $"{p.Text} reaches the next cell"));
    }

    [Fact]
    public async Task Cell_styles_at_break_draw_on_both_parts()
    {
        var result = await Convert("cell-styles-at-break");
        Assert.Equal(2, result.Pages.Count);
        Assert.All(result.Pages, page => Assert.True(Fills(page).Count >= 2));
        Assert.Contains(result.Runs[1], r => r.Text.StartsWith("Note 75", StringComparison.Ordinal));
    }

    // Lists

    [Fact]
    public async Task Numbering_runs_on_across_pages()
    {
        var result = await Convert("numbering-across-pages");
        Assert.Equal(2, result.Pages.Count);
        var markers = result.Runs.SelectMany(r => r).Where(r => Regex.IsMatch(r.Text, @"^\d+\.$")).Select(r => r.Text);
        Assert.Equal(Enumerable.Range(1, 60).Select(i => $"{i}."), markers);
        Assert.Contains(result.Runs[1], r => Regex.IsMatch(r.Text, @"^\d+\.$"));
    }

    [Fact]
    public async Task Mixed_nested_lists_show_their_markers()
    {
        var result = await Convert("mixed-nested-lists");
        var markers = result.Runs[0].Where(r => Regex.IsMatch(r.Text, @"^([0-9]+|[a-z]+)\.$")).Select(r => r.Text);
        Assert.Equal(new[] { "1.", "c.", "iii.", "ii.", "10.", "11.", "i.", "d.", "2." }, markers);
    }

    [Fact]
    public async Task Lists_in_cells_keep_markers_and_indent()
    {
        var result = await Convert("lists-in-cells");
        var runs = result.Runs[0];
        var plan = runs.Single(r => r.Text == "Plan");
        foreach (var (marker, item) in new[] { ("1.", "Order parts"), ("2.", "Assemble"), ("3.", "Ship") })
        {
            var m = runs.Single(r => r.Text == marker);
            var t = runs.Single(r => r.Text == item);
            Assert.True(m.X > plan.X && t.X > m.X, item);
        }
        // Bullets are drawn shapes; the bulleted items are indented like the numbered ones.
        Assert.Equal(runs.Single(r => r.Text == "Order parts").X - plan.X, runs.Single(r => r.Text == "Count parts").X - runs.Single(r => r.Text == "Checks").X, 1);
    }

    // Text

    [Fact]
    public async Task Long_url_wraps_and_stays_one_link()
    {
        const string url = "https://example.com/downloads/reports/2026/annual/customer-portfolio-summary-with-appendices-and-tables-final-version-v12.pdf";
        var result = await Convert("long-url");
        var parts = result.Runs[0].Where(r => r.Text.StartsWith("https://", StringComparison.Ordinal) || (r.Text.Length > 0 && url.Contains(r.Text) && r.Text != "Download:")).ToList();
        Assert.True(parts.Count >= 2);
        Assert.Equal(url, string.Concat(parts.Select(p => p.Text)));
        Assert.All(parts, p => Assert.True(p.X + Width(p) <= Right + 0.01));
        Assert.Equal(parts.Count, Regex.Matches(result.Pdf, Regex.Escape("/URI (" + url + ")")).Count);
    }

    [Fact]
    public async Task Wide_pre_is_clipped_and_pre_wrap_wraps()
    {
        var result = await Convert("wide-pre");
        var warning = Assert.Single(result.Html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.ContentClipped, warning.Kind);
        var clip = Assert.Single(BodyClips(result.Pages[0]));
        Assert.True(clip.X + clip.Width <= Right + 0.01);
        var lines = result.Runs[0].Where(r => r.Text.StartsWith("2026-03-01", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, lines.Count);
        Assert.True(lines[0].X + Width(lines[0]) > Right);
        Assert.True(lines[1].X + Width(lines[1]) <= Right + 0.01);
        Assert.Contains(result.Runs[0], r => r.Text.Contains("2 notes") && r.Y < lines[1].Y);
    }

    [Fact]
    public async Task Non_latin_characters_draw_western_european_and_question_marks()
    {
        var result = await Convert("non-latin-characters");
        string text = string.Join(" ", result.Runs[0].Select(r => r.Text));
        Assert.Contains("Café Müller, Señora Ñúñez, Øresund: 25,00 € – “quoted”", text);
        Assert.Contains("??????, ??, ?", text);
    }

    [Fact]
    public async Task Nbsp_keeps_words_together_and_whitespace_collapses()
    {
        var result = await Convert("nbsp-and-whitespace");
        Assert.Contains(result.Runs[0], r => r.Text == "Total: 1 250.00 EUR");
        Assert.Contains(result.Runs[0], r => r.Text == "Spaced out source text");
    }

    // Images

    [Fact]
    public async Task Relative_and_data_images_draw_and_missing_shows_alt()
    {
        var result = await Convert("relative-and-data-images");
        var draws = HtmlEntryPointTests.Draws(result.Pages[0]);
        Assert.Equal(new[] { (120 * 0.75, 40 * 0.75), (40 * 0.75, 40 * 0.75) }, draws.Select(d => (Math.Round(d.Width, 2), Math.Round(d.Height, 2))));
        Assert.Contains(result.Runs[0], r => r.Text == "Team photo");
        Assert.Equal("photos/team-photo.jpg", Assert.Single(result.Html.Warnings).Source);
    }

    [Fact]
    public async Task Image_wider_than_page_scales_to_the_page_width()
    {
        var result = await Convert("image-wider-than-page");
        var draw = Assert.Single(HtmlEntryPointTests.Draws(result.Pages[0]));
        Assert.True(draw.X >= Left - 0.01 && draw.X + draw.Width <= Right + 0.01);
        Assert.True(draw.Width > 450);
        Assert.Equal(4.0, draw.Width / draw.Height, 2);
    }

    [Fact]
    public async Task Svg_and_webp_show_their_alt_text()
    {
        var result = await Convert("svg-and-webp");
        Assert.Empty(HtmlEntryPointTests.Draws(result.Pages[0]));
        Assert.Contains(result.Runs[0], r => r.Text == "[Sales chart]");
        Assert.Contains(result.Runs[0], r => r.Text == "[Product photo]");
    }

    // Links

    [Fact]
    public async Task Anchor_links_jump_to_their_target()
    {
        var result = await Convert("anchor-links");
        Assert.Equal(2, result.Pages.Count);
        var target = result.Runs[1].Single(r => r.Text == "Terms and conditions");
        var dest = Regex.Match(result.Pdf, @"/Dest \[(\d+) 0 R /XYZ ([\d.]+) ([\d.]+) null\]");
        Assert.True(dest.Success);
        var kids = Regex.Match(result.Pdf, @"/Kids \[([^\]]*)\]").Groups[1].Value.Split(new[] { " 0 R" }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();
        Assert.Equal(1, kids.IndexOf(dest.Groups[1].Value));
        double y = double.Parse(dest.Groups[3].Value, CultureInfo.InvariantCulture);
        Assert.True(y > target.Y && y - target.Y < 30, $"destination {y}, heading baseline {target.Y}");
    }

    [Fact]
    public async Task External_links_are_clickable_and_javascript_is_dropped()
    {
        var result = await Convert("external-links");
        Assert.Contains("/URI (https://example.com/pricing)", result.Pdf);
        Assert.Contains("/URI (mailto:sales@example.com)", result.Pdf);
        Assert.DoesNotContain("javascript", result.Pdf, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, Regex.Matches(result.Pdf, "/Subtype /Link").Count);
        Assert.Contains(result.Runs[0], r => r.Text == "Run a script");
    }

    // CSS

    [Fact]
    public async Task Print_media_applies_print_rules_only()
    {
        var result = await Convert("print-media");
        Assert.Contains(result.Runs[0], r => r.Text == "Printed copy: check the website for updates.");
        Assert.DoesNotContain(result.Runs[0], r => r.Text.Contains("Products"));
        Assert.Contains(result.Runs[0], r => r.Text == "This line is not red.");
        Assert.DoesNotContain(" 1 0 0 rg", result.Pages[0].Content);
    }

    [Fact]
    public async Task Flex_and_grid_items_stack_without_overlap()
    {
        var result = await Convert("flex-and-grid");
        var fills = Fills(result.Pages[0]).OrderByDescending(f => f.Y).ToList();
        Assert.Equal(5, fills.Count);
        for (int i = 1; i < fills.Count; i++) Assert.True(fills[i].Y + fills[i].Height <= fills[i - 1].Y + 0.01, $"fill {i} overlaps the one above");
        string[] items = { "Card one", "Card two", "Card three", "Grid cell A", "Grid cell B" };
        var ys = items.Select(t => result.Runs[0].Single(r => r.Text == t).Y).ToList();
        Assert.Equal(ys.OrderByDescending(y => y), ys);
    }

    [Fact]
    public async Task Backgrounds_draw_behind_their_content()
    {
        var result = await Convert("backgrounds");
        Assert.Equal("background-image", Assert.Single(result.Html.Warnings).Source);
        string content = result.Pages[0].Content;
        Assert.True(Fills(result.Pages[0]).Count >= 2);
        int firstFill = Regex.Match(content, " re f Q").Index;
        Assert.True(firstFill < content.IndexOf("(Panel with a color and an image) Tj", StringComparison.Ordinal));
        int cellFill = Regex.Matches(content, " re f Q").Last().Index;
        Assert.True(cellFill < content.IndexOf("(Shaded cell) Tj", StringComparison.Ordinal));
    }

    // Malformed HTML

    [Fact]
    public async Task Malformed_html_keeps_all_text()
    {
        var result = await Convert("malformed-html");
        var texts = result.Runs[0].Select(r => r.Text).ToList();
        string[] expected = { "Sloppy HTML", "First", "bold", "bold italic", "italic", "plain", "Second paragraph", "Item one", "Item two", "Cell A", "Cell B", "Trailing text" };
        int at = 0;
        foreach (string word in expected)
        {
            int found = texts.FindIndex(at, t => t.Trim() == word);
            Assert.True(found >= 0, $"'{word}' not drawn in order: {string.Join(" | ", texts)}");
            at = found + 1;
        }
    }

    #endregion

    #region Private Methods

    private static async Task<Result> Convert(string id)
    {
        var scenario = HtmlScenarios.Find(id) ?? throw new ArgumentException("Unknown scenario " + id, nameof(id));
        var (document, html) = await HtmlToPdfSample.BuildAsync(scenario.Html, (reference, _) => Task.FromResult(HtmlGoldenTests.Images(reference)));
        var bytes = PdfPageReader.SaveBytes(document);
        PdfStructureChecker.AssertValid(bytes);
        var pages = PdfPageReader.Read(bytes);
        return new Result(pages, pages.Select(Runs).ToList(), Encoding.Latin1.GetString(bytes), html);
    }

    // Background fills HTML draws, as (x, y, width, height).
    private static IReadOnlyList<(double X, double Y, double Width, double Height)> Fills(PdfPageReader.Page page) => page.LayoutRectangles();

    // Clips HTML sets inside the body; the footer band sets its own clip below it.
    private static IReadOnlyList<(double X, double Y, double Width, double Height)> BodyClips(PdfPageReader.Page page) =>
        page.ClipRectangles().Where(c => c.Y + c.Height > BodyBottom + 1).ToList();

    private static void AssertInsideBody(IReadOnlyList<PdfPageReader.Page> pages, bool allowWide)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            foreach (var run in Runs(pages[i]))
            {
                Assert.True(run.Y - run.Size * 0.2 >= BodyBottom - 0.01 && run.Y <= Top + 0.01, $"'{run.Text}' at y {run.Y} on page {i + 1}");
                Assert.True(run.X >= Left - 0.01, $"'{run.Text}' at x {run.X} on page {i + 1}");
                if (!allowWide) Assert.True(run.X + Width(run) <= Right + 0.01, $"'{run.Text}' ends at x {run.X + Width(run)} on page {i + 1}");
            }
            foreach (var draw in HtmlEntryPointTests.Draws(pages[i]))
                Assert.True(draw.Y >= BodyBottom - 0.01 && draw.Y + draw.Height <= Top + 0.01 && draw.X + draw.Width <= Right + 0.01, $"image at y {draw.Y} on page {i + 1}");
        }
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed record Result(IReadOnlyList<PdfPageReader.Page> Pages, IReadOnlyList<List<Run>> Runs, string Pdf, PdfHtml Html);

    #endregion
}
