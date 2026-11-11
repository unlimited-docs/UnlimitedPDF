using System.Diagnostics;
using System.Text.RegularExpressions;
using static UnlimitedPDF.Tests.Html.HtmlLayoutTests;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Regression tests for the 14-html-to-pdf review findings on layout: content always stays inside the page body,
/// nesting and large tables lay out in linear time, and warnings, markers, links, and bookmarks are right
/// (WR-038; review F-05, F-08 to F-18, F-20, F-21).
/// </summary>
// The timing checks run outside the parallel test runs, so CPU load from other tests does not skew them.
[Collection(ProcessWideHtmlSettingsCollection.Name)]
public class HtmlLayoutRegressionTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("<div style='height:500pt'></div><div style='height:500pt'></div><p>AFTER</p>", "AFTER")]
    [InlineData("<div style='padding-top:900pt'>AFTER</div>", "AFTER")]
    [InlineData("<div style='height:100pt;border:1px solid'></div><div style='page-break-before:always'>AFTER</div>", "AFTER")]
    public void Blocks_without_text_move_and_break_like_other_content(string html, string text)
    {
        // F-08: heights, padding, and borders never moved to the next page, so later content ran off the page.
        var pages = Render(html);
        Assert.True(pages.Count >= 2);
        var after = Assert.Single(pages.SelectMany(Runs), r => r.Text == text);
        AssertInsideBody(pages);
        Assert.DoesNotContain(Runs(pages[0]), r => r.Text == text);
        _ = after;
    }

    [Fact]
    public void Html_starting_low_on_a_page_moves_its_first_line_to_the_next_page()
    {
        // F-08: HTML that started mid-page drew its first line below the body.
        var doc = Document();
        for (int i = 0; i < 52; i++) doc.Body.AddText("filler " + i.ToString(CultureInfo.InvariantCulture));
        doc.Body.AddHtml(PdfHtml.FromString("<body style='margin:0'><p style='margin:0; font-size:30px'>Big first line</p>"));
        var pages = PdfPageReader.Save(doc);
        Assert.Equal(2, pages.Count);
        Assert.Contains(Runs(pages[1]), r => r.Text == "Big first line");
        AssertInsideBody(pages);
    }

    [Fact]
    public void A_tall_image_starting_low_on_a_page_moves_instead_of_shrinking()
    {
        // F-08: an image was scaled to the little room left on the first page.
        var doc = Document();
        for (int i = 0; i < 20; i++) doc.Body.AddText("filler " + i.ToString(CultureInfo.InvariantCulture));
        string png = HtmlEntryPointTests.Data(new PngBuilder { ColorType = 6, Width = 100, Height = 900 }.Build());
        doc.Body.AddHtml(PdfHtml.FromString($"<body style='margin:0'><img src='{png}'>"));
        var pages = PdfPageReader.Save(doc);
        var draw = Assert.Single(HtmlEntryPointTests.Draws(pages[1]));
        Assert.Equal(675, draw.Height, 1);
        Assert.True(draw.Y >= Bottom - 0.01);
    }

    [Fact]
    public void A_header_taller_than_the_body_is_not_repeated_and_nothing_runs_off_the_page()
    {
        // F-09: a 900 pt header row was repeated on every page and the body rows ran off the page.
        var html = PdfHtml.FromString("<table border=1><thead><tr><th style='height:900pt'>Head</th></tr></thead><tr><td>one</td></tr><tr><td>two</td></tr><tr><td>three</td></tr></table>");
        var doc = Document();
        doc.Body.AddHtml(html);
        var pages = PdfPageReader.Save(doc);
        AssertInsideBody(pages);
        var texts = pages.SelectMany(Runs).Select(r => r.Text).ToList();
        Assert.Single(texts, t => t == "Head");
        Assert.Equal(new[] { "one", "two", "three" }, texts.Where(t => t is "one" or "two" or "three"));
        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped && w.Source == "thead");
    }

    [Fact]
    public void A_row_span_group_taller_than_the_body_stays_inside_the_pages()
    {
        // F-10: a rowspan group was drawn whole and its text reached y = -352.
        string lines = string.Join("<br>", Enumerable.Range(1, 80).Select(i => "line " + i.ToString(CultureInfo.InvariantCulture)));
        var pages = Render($"<table border=1><tr><td rowspan=2>{lines}</td><td>a</td></tr><tr><td>b</td></tr></table>");
        AssertInsideBody(pages);
        Assert.Equal(Enumerable.Range(1, 80).Select(i => "line " + i.ToString(CultureInfo.InvariantCulture)), pages.SelectMany(Runs).Where(r => r.Text.StartsWith("line ", StringComparison.Ordinal)).Select(r => r.Text));
    }

    [Fact]
    public void A_tall_row_starting_near_the_bottom_starts_on_the_next_page()
    {
        // F-11: with a few points left, the first line of a tall cell was drawn below the body.
        var doc = Document();
        for (int i = 0; i < 52; i++) doc.Body.AddText("filler " + i.ToString(CultureInfo.InvariantCulture));
        string lines = string.Join("<br>", Enumerable.Range(1, 90).Select(i => "row line " + i.ToString(CultureInfo.InvariantCulture)));
        doc.Body.AddHtml(PdfHtml.FromString($"<body style='margin:0'><table><tr><td>{lines}</td></tr></table>"));
        var pages = PdfPageReader.Save(doc);
        AssertInsideBody(pages);
        Assert.Equal(90, pages.SelectMany(Runs).Count(r => r.Text.StartsWith("row line", StringComparison.Ordinal)));
    }

    [Fact]
    public void Break_inside_avoid_is_judged_against_a_fresh_page()
    {
        // F-11: a block that fits a fresh page still split because it was compared with the room left mid-page.
        var doc = Document();
        for (int i = 0; i < 30; i++) doc.Body.AddText("filler " + i.ToString(CultureInfo.InvariantCulture));
        string kept = "<div style='break-inside: avoid'>" + string.Concat(Enumerable.Range(1, 30).Select(i => $"<p style='margin:0'>kept {i}</p>")) + "</div>";
        doc.Body.AddHtml(PdfHtml.FromString("<body style='margin:0'>" + kept));
        var pages = PdfPageReader.Save(doc);
        Assert.Equal(2, pages.Count);
        Assert.Equal(30, Runs(pages[1]).Count(r => r.Text.StartsWith("kept", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("table", 30)]
    [InlineData("inline-block", 30)]
    public void Deep_nesting_lays_out_in_linear_time(string kind, int depth)
    {
        // F-05: each level laid its content out several times; depth 22 took 37 s.
        string html = "x";
        for (int i = 0; i < depth; i++)
            html = kind == "table" ? "<table><tr><td>" + html + "</td></tr></table>" : "<span style='display:inline-block'>" + html + "</span>";
        var watch = Stopwatch.StartNew();
        var pages = Render(html);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
        Assert.Contains(pages.SelectMany(Runs), r => r.Text == "x");
    }

    [Fact]
    public void Large_tables_lay_out_in_linear_time()
    {
        // F-12: every row searched all cells; 24,000 rows took 31 s.
        string rows = string.Concat(Enumerable.Range(1, 10000).Select(i => $"<tr><td>{i}</td><td>v</td></tr>"));
        var watch = Stopwatch.StartNew();
        using var stream = new MemoryStream();
        PdfOperations.ConvertHtml(PdfHtml.FromString("<table>" + rows + "</table>"), stream);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Inside_markers_appear_once_in_cells_headers_and_kept_items()
    {
        // F-13: inside markers were added again on every layout pass ("• • ITEM").
        var pages = Render("<table><thead><tr><th><ol style='list-style-position:inside'><li>HEAD</ol></th></tr></thead><tr><td><ul style='list-style-position:inside'><li>ITEM</ul></td></tr></table><ul style='list-style-position:inside'><li style='break-inside:avoid'>KEPT</ul>");
        // The marker is its own text run just before the item's text, once per item.
        var texts = pages.SelectMany(Runs).Select(r => r.Text).ToList();
        Assert.Equal(new[] { "1. ", "HEAD", "• ", "ITEM", "• ", "KEPT" }, texts);
    }

    [Fact]
    public void An_outside_marker_before_a_long_table_is_drawn_on_the_first_page()
    {
        // F-13: the bullet was drawn on page 2 at page 1's height.
        string rows = string.Concat(Enumerable.Range(1, 80).Select(i => $"<tr><td>r{i}</td></tr>"));
        var pages = Render($"<ul><li><table>{rows}</table></li></ul>");
        Assert.True(pages.Count >= 2);
        Assert.Contains(" c ", pages[0].Content);
        Assert.DoesNotContain(" c f Q", pages[1].Content);
    }

    [Fact]
    public void Warnings_inside_table_cells_are_reported_with_their_page()
    {
        // F-14, F-15: links in cells were never reported; split-row warnings named the row's first page.
        var html = PdfHtml.FromString("<p style='break-after:page'>first</p><table><tr><td><a href='ftp://x'>bad</a></td></tr></table>");
        var doc = Document();
        doc.Body.AddHtml(html);
        PdfPageReader.Save(doc);
        var warning = Assert.Single(html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.LinkDropped, warning.Kind);
        Assert.Equal(2, warning.PageNumber);
    }

    [Fact]
    public void A_clipped_table_warning_names_the_page_it_is_drawn_on()
    {
        // F-15: the warning was raised before the table moved to the next page.
        string cells = string.Concat(Enumerable.Range(1, 12).Select(i => $"<td style='white-space:nowrap'>column-number-{i}"));
        string filler = string.Concat(Enumerable.Range(1, 52).Select(i => $"<p style='margin:0'>f{i}</p>"));
        var html = PdfHtml.FromString($"<body style='margin:0'>{filler}<table><thead><tr>{cells}</tr></thead><tr>{cells}</tr></table>");
        var doc = Document();
        doc.Body.AddHtml(html);
        var pages = PdfPageReader.Save(doc);
        var clip = Assert.Single(html.Warnings, w => w.Source == "table");
        Assert.Equal(2, clip.PageNumber);
        Assert.Contains(" re W n", pages[clip.PageNumber!.Value - 1].Content, StringComparison.Ordinal);
        Assert.Contains(Runs(pages[clip.PageNumber.Value - 1]), r => r.Text == "column-number-1");
    }

    [Fact]
    public void A_clipped_table_background_stays_inside_the_clip()
    {
        // F-16: the table's own background was drawn before the clip began and bled past the body edge.
        string cells = string.Concat(Enumerable.Range(1, 12).Select(i => $"<td style='white-space:nowrap'>column-number-{i}"));
        var page = Assert.Single(Render($"<table style='background-color:#ff0000; border-collapse:collapse; border:2px solid blue'><tr>{cells}</tr></table>"));
        int clip = page.Content.IndexOf(" re W n", StringComparison.Ordinal);
        int fill = page.Content.IndexOf("q 1 0 0 rg", StringComparison.Ordinal);
        int border = page.Content.IndexOf("0 0 1 RG", StringComparison.Ordinal);
        Assert.True(clip >= 0 && fill > clip && border > clip, "background and border follow the clip");
        Assert.Equal(Regex.Matches(page.Content, @"(?<![A-Za-z])q(?![A-Za-z])").Count, Regex.Matches(page.Content, @"(?<![A-Za-z])Q(?![A-Za-z])").Count);
    }

    [Fact]
    public void An_id_on_a_word_split_across_lines_keeps_its_link_target()
    {
        // F-17: the first piece of a split word dropped the ids that start there.
        string word = new string('W', 90);
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString($"<p><a href='#long'>jump</a></p><p><span id='long'>{word}</span></p>"));
        string pdf = Encoding.ASCII.GetString(PdfPageReader.SaveBytes(doc));
        Assert.Contains("/Dest [", pdf);
    }

    [Fact]
    public void Headings_in_a_repeated_header_are_bookmarked_once()
    {
        // F-18: heading marks were copied onto every page the header repeated on.
        string rows = string.Concat(Enumerable.Range(1, 120).Select(i => $"<tr><td>r{i}</td></tr>"));
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString($"<table><thead><tr><th><h3>Ledger</h3></th></tr></thead>{rows}</table>"), headingBookmarks: true);
        string pdf = Encoding.ASCII.GetString(PdfPageReader.SaveBytes(doc));
        Assert.Contains("/Count 1", pdf);
        Assert.Single(Regex.Matches(pdf, "/Title <4C6564676572>"));
    }

    [Fact]
    public void Margins_collapse_through_an_empty_block()
    {
        // F-20: an empty div stopped margins collapsing and doubled the gap.
        var runs = Runs(Assert.Single(Render("<p>a</p><div></div><p>b</p>")));
        Assert.Equal(14.4 + 12, runs[0].Y - runs[1].Y, 3);
    }

    [Fact]
    public void A_second_save_uses_the_current_margins()
    {
        // F-21: the layout width was fixed at the first planning pass.
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString("<body style='margin:0'><p style='margin:0'>" + string.Join(" ", Enumerable.Range(1, 60).Select(i => "word" + i.ToString(CultureInfo.InvariantCulture))) + "</p>"));
        PdfPageReader.Save(doc);
        doc.MarginLeft = 150;
        var runs = Runs(Assert.Single(PdfPageReader.Save(doc)));
        Assert.All(runs, r => Assert.True(r.X >= 150 - 0.01 && r.X + Width(r) <= Right + 0.01));
    }

    #endregion

    #region Private Methods

    // Every text run's baseline and every image lies inside the page body (A4, 36 pt margins).
    private static void AssertInsideBody(IReadOnlyList<PdfPageReader.Page> pages)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            foreach (var run in Runs(pages[i]))
                Assert.True(run.Y - run.Size * 0.2 >= Bottom - 0.01 && run.Y <= Top + 0.01, $"'{run.Text}' at y {run.Y} on page {i + 1}");
            foreach (var draw in HtmlEntryPointTests.Draws(pages[i]))
                Assert.True(draw.Y >= Bottom - 0.01 && draw.Y + draw.Height <= Top + 0.01, $"image at y {draw.Y} on page {i + 1}");
        }
    }

    #endregion

    #endregion
}
