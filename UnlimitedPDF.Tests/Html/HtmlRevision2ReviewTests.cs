using System.Diagnostics;
using System.Text.RegularExpressions;
using static UnlimitedPDF.Tests.Html.HtmlLayoutTests;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Regression tests for the 14-html-to-pdf revision 2 review findings: long words in tables break only where the
/// table would not fit, selectors match in linear time, page limits stop HTML layout early, and list markers,
/// inline-blocks, table breaks, set heights, nesting limits, and zero heights behave (WR-038; review F-22 to F-24,
/// F-34 to F-41).
/// </summary>
// The timing checks and the process-wide limit checks run outside the parallel test runs.
[Collection(ProcessWideHtmlSettingsCollection.Name)]
public class HtmlRevision2ReviewTests
{
    #region Fields and Constants

    private const string Url = "https://example.com/downloads/reports/2026/annual/customer-portfolio-summary-with-appendices-and-tables-final.pdf";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_table_with_a_set_width_grows_to_its_words_instead_of_breaking_them()
    {
        // F-22: a 150 pt table broke "Supercalifr|agilistic" although the page had room for both words.
        var html = PdfHtml.FromString("<table style='width:150pt'><tr><td>Supercalifragilistic</td><td>Antidisestablishmentarianism</td></tr></table>");
        var texts = Texts(html);
        Assert.Contains("Supercalifragilistic", texts);
        Assert.Contains("Antidisestablishmentarianism", texts);
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_long_word_breaks_while_other_columns_keep_their_words_whole()
    {
        // F-23: one URL made every column break its words, including a non-breaking amount.
        var html = PdfHtml.FromString($"<table style='width:100%'><tr><td>{Url}</td><td>International shipping arrangements</td><td>Total:&nbsp;1&nbsp;250.00&nbsp;EUR</td></tr></table>");
        var pages = Pages(html);
        var runs = Runs(pages[0]);
        Assert.Contains(runs, r => r.Text == "Total: 1 250.00 EUR");
        var words = string.Join(" ", runs.Where(r => !r.Text.StartsWith("http", StringComparison.Ordinal) && !Url.Contains(r.Text) && !r.Text.StartsWith("Total", StringComparison.Ordinal)).Select(r => r.Text));
        Assert.Equal("International shipping arrangements", Regex.Replace(words, " +", " "));
        var first = runs.Single(r => r.Text.StartsWith("https://", StringComparison.Ordinal));
        var urlParts = runs.Where(r => Math.Abs(r.X - first.X) < 0.01).ToList();
        Assert.True(urlParts.Count >= 2);
        Assert.Equal(Url, string.Concat(urlParts.Select(r => r.Text)));
        Assert.All(runs, r => Assert.True(r.X + Width(r) <= Right + 0.01, r.Text));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Words_in_nested_tables_and_spans_stay_whole_beside_a_long_word()
    {
        // F-23: "O|ut|er", "In|n|er", and "Thi|rd" in nested tables and column spans.
        var texts = Texts(PdfHtml.FromString($"<table><tr><td>Outer words</td><td><table><tr><td>Inner words</td><td>{Url}</td></tr></table></td></tr>" +
            $"<tr><td colspan=2>{Url}</td></tr><tr><td>First column</td><td>Third column</td></tr></table>"));
        foreach (string word in new[] { "Outer", "Inner", "First", "Third" })
            Assert.Contains(texts, t => t.StartsWith(word, StringComparison.Ordinal));
    }

    [Fact]
    public void A_table_that_cannot_fit_even_with_broken_words_keeps_its_words_and_is_clipped()
    {
        // F-24: other columns dropped to one character wide and the table was clipped anyway.
        var html = PdfHtml.FromString($"<table><tr><td><pre>{new string('x', 120)}</pre></td><td>{Url}</td><td>Description of goods delivered</td></tr></table>");
        var texts = Texts(html);
        Assert.Contains(Url, texts);
        Assert.Contains(texts, t => t.StartsWith("Description", StringComparison.Ordinal));
        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped);
    }

    [Fact]
    public void One_long_word_in_a_long_table_breaks_no_other_row()
    {
        // F-23: a URL in row 2500 of 5,000 broke every "Row N" and tripled the page count.
        var rows = new StringBuilder("<table>");
        for (int i = 0; i < 1000; i++) rows.Append(i == 500 ? $"<tr><td>Row {i}</td><td>{Url}</td></tr>" : $"<tr><td>Row {i}</td><td>value</td></tr>");
        var texts = Texts(PdfHtml.FromString(rows + "</table>"));
        // The narrow first column may wrap "Row 100" at its space, but no word is broken.
        var cells = texts.Where(t => !Url.Contains(t)).ToList();
        Assert.All(cells, t => Assert.Matches(@"^(Row|\d+|Row \d+|value)$", t));
        Assert.Equal(1000, cells.Count(t => t == "value") + 1);
        Assert.Equal(1000, cells.Count(t => t.StartsWith("Row", StringComparison.Ordinal)));
    }

    [Fact]
    public void Long_descendant_selectors_over_many_nested_elements_load_quickly()
    {
        // F-34: the failed-match memory was per element, so 2,000 leaves under 250 nested divs took about 37 s.
        var html = new StringBuilder("<style>body " + string.Concat(Enumerable.Repeat("div ", 30)) + "span{color:red}</style>");
        html.Append(string.Concat(Enumerable.Repeat("<div>", 250)));
        for (int i = 0; i < 2000; i++) html.Append("<span>x</span>");
        var watch = Stopwatch.StartNew();
        var loaded = PdfHtml.FromString(html.ToString());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        // The selector still matches: the spans are red.
        Assert.Contains(" 1 0 0 rg 1 0 0 1 ", Pages(loaded)[0].Content);
    }

    [Fact]
    public void A_page_limit_stops_html_layout_before_planning_every_page()
    {
        // F-35: a 2,000,000 pt height planned 2,598 pages before Body.MaxPages was checked.
        var doc = Document();
        doc.Body.MaxPages = 5;
        doc.Body.AddHtml(PdfHtml.FromString("<div style='height:2000000pt'>x</div><p>after</p>"));
        var watch = Stopwatch.StartNew();
        var ex = Assert.Throws<InvalidOperationException>(() => doc.Save(new MemoryStream()));
        Assert.Contains("Body.MaxPages is 5", ex.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
    }

    [Fact]
    public void An_inside_marker_before_a_table_keeps_the_items_content()
    {
        // F-36: the item drew only its bullet; the table and the paragraph after it were lost.
        var texts = Render("<ul style='list-style-position:inside'><li><table><tr><td>CELL</td></tr></table><p>AFTER</p></li></ul>").SelectMany(Runs).Select(r => r.Text).ToList();
        Assert.Equal(new[] { "•", "CELL", "AFTER" }, texts.Select(t => t.Trim()));
    }

    [Fact]
    public void An_inline_block_taller_than_a_page_is_clipped_at_the_page_bottom()
    {
        // F-37: its lines were drawn below the page, down to negative y.
        string lines = string.Concat(Enumerable.Range(1, 120).Select(i => $"line {i}<br>"));
        var html = PdfHtml.FromString($"<span style='display:inline-block'>{lines}</span><p>AFTER</p>");
        var pages = Pages(html);
        foreach (var page in pages)
            Assert.All(Runs(page), r => Assert.True(Clipped(page, r) || r.Y - r.Size * 0.2 >= Bottom - 0.01, $"'{r.Text}' at {r.Y}"));
        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped);
        Assert.Contains(Runs(pages[pages.Count - 1]), r => r.Text == "AFTER");
    }

    [Fact]
    public void Break_inside_avoid_moves_a_table_that_fits_a_fresh_page()
    {
        // F-38: a 30-row table marked avoid split mid-page.
        string filler = string.Concat(Enumerable.Range(1, 40).Select(i => $"<div>filler {i}</div>"));
        string rows = string.Concat(Enumerable.Range(1, 30).Select(i => $"<tr><td>row {i}</td></tr>"));
        var pages = Render($"<body style='margin:0'>{filler}<table style='break-inside: avoid'>{rows}</table>");
        Assert.Equal(2, pages.Count);
        Assert.Equal(30, Runs(pages[1]).Count(r => r.Text.StartsWith("row ", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_set_cell_height_taller_than_a_page_continues_on_the_next_pages()
    {
        // F-39: the 2,000 pt height was dropped once the row split, so the row ended below its text.
        var pages = Render("<body style='margin:0'><table style='border-collapse:collapse'><tr><td style='height:2000pt; border:1px solid'>x</td></tr></table><p>AFTER</p>");
        Assert.True(pages.Count >= 3, $"{pages.Count} pages");
        Assert.All(pages.Take(pages.Count - 1), page => Assert.Contains(" l S", page.Content, StringComparison.Ordinal));
        Assert.Contains(Runs(pages[pages.Count - 1]), r => r.Text == "AFTER");
    }

    [Fact]
    public void Nesting_limits_above_the_safe_maximum_are_rejected()
    {
        // F-40: a limit high enough to let layout exhaust the stack was accepted.
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfHtmlLoadOptions { MaxNestingDepth = 513 });
        Assert.Equal(512, new PdfHtmlLoadOptions { MaxNestingDepth = 512 }.MaxNestingDepth);
        int before = PdfHtml.DefaultMaxNestingDepth;
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfHtml.DefaultMaxNestingDepth = 513);
        Assert.Equal(before, PdfHtml.DefaultMaxNestingDepth);
    }

    [Fact]
    public void Inline_blocks_nested_within_the_default_limit_lay_out_on_a_small_stack()
    {
        // F-40: 250 nested inline-blocks overflowed a 1 MB stack; beyond 64 levels they are laid out inline.
        string html = string.Concat(Enumerable.Repeat("<span style='display:inline-block'>", 250)) + "deep";
        Exception? failure = null;
        IReadOnlyList<PdfHtmlWarning> warnings = Array.Empty<PdfHtmlWarning>();
        string content = string.Empty;
        var thread = new Thread(() =>
        {
            try
            {
                var loaded = PdfHtml.FromString(html);
                content = Pages(loaded)[0].Content;
                warnings = loaded.Warnings;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.Contains("(deep) Tj", content, StringComparison.Ordinal);
        Assert.Contains(warnings, w => w.Kind == PdfHtmlWarningKind.UnsupportedValue && w.Source == "span");
    }

    [Fact]
    public void A_zero_height_empty_block_lets_margins_collapse_through_it()
    {
        // F-41: <div style='height:0'> stopped the margins collapsing, giving a 38.4 pt gap instead of 26.4 pt.
        double Gap(string html)
        {
            var runs = Runs(Render(html)[0]);
            return runs.Single(r => r.Text == "a").Y - runs.Single(r => r.Text == "b").Y;
        }
        Assert.Equal(Gap("<p>a</p><p>b</p>"), Gap("<p>a</p><div style='height:0'></div><p>b</p>"), 2);
    }

    [Theory]
    [InlineData(1, "<table><tr><td>cell</td></tr></table>")]
    [InlineData(1, "<p>hello</p><img src='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAFklEQVR42mPQmxdNEmIY1TCqYfhqAACofScQa/x9PAAAAABJRU5ErkJggg=='>")]
    [InlineData(1, "<p>a</p><div style='break-inside:avoid'>b</div>")]
    [InlineData(1, "<p style='break-after:page'>only</p>")]
    [InlineData(2, "LINES<table><tr><td>cell</td></tr></table>")]
    public void Documents_that_fit_the_page_limit_save(int maxPages, string html)
    {
        // F-42: layout looked at the next page's room before needing it, and the limit check failed documents that fit.
        var doc = Document();
        doc.Body.MaxPages = maxPages;
        doc.Body.AddHtml(PdfHtml.FromString(html.Replace("LINES", string.Concat(Enumerable.Range(1, 60).Select(i => $"<div>line {i}</div>")), StringComparison.Ordinal)));
        Assert.Equal(maxPages, PdfPageReader.Save(doc).Count);
    }

    [Fact]
    public void A_document_one_page_over_the_limit_gets_the_save_message_and_a_huge_one_stops_early()
    {
        // F-42, F-46: one page over is reported by the save's page check; a huge height stops layout with "1 page".
        var over = Document();
        over.Body.MaxPages = 1;
        over.Body.AddHtml(PdfHtml.FromString(string.Concat(Enumerable.Range(1, 60).Select(i => $"<div>line {i}</div>")) + "<table><tr><td>cell</td></tr></table>"));
        Assert.Contains("needs 2 pages, but Body.MaxPages is 1", Assert.Throws<InvalidOperationException>(() => over.Save(new MemoryStream())).Message, StringComparison.Ordinal);

        // Content after a huge set height fails fast; the blank space stops at the limit instead of making its pages.
        var huge = Document();
        huge.Body.MaxPages = 1;
        huge.Body.AddHtml(PdfHtml.FromString("<table><tr><td style='height:1000000000pt'>x</td></tr></table><p>after</p>"));
        var watch = Stopwatch.StartNew();
        var ex = Assert.Throws<InvalidOperationException>(() => huge.Save(new MemoryStream()));
        Assert.Contains("but Body.MaxPages is 1", ex.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
    }

    [Theory]
    [InlineData("<table><tr><td><div style='height:5000000000pt'>x</div></td></tr></table><p>after</p>")]
    [InlineData("<table><tr><td><div style='height:1000000000pt'>x</div></td><td><div style='height:1000000000pt'>y</div></td></tr></table><p>after</p>")]
    [InlineData("<table><tr><td><table><tr><td style='height:1000000000pt'>x</td></tr></table></td></tr></table><p>after</p>")]
    public void Tall_content_inside_split_table_cells_stops_at_the_page_limit(string html)
    {
        // F-48: cells split across pages were laid out without the limit: 5e9 pt took 8.5 s and 2.8 GB.
        var doc = Document();
        doc.Body.MaxPages = 3;
        doc.Body.AddHtml(PdfHtml.FromString(html));
        var watch = Stopwatch.StartNew();
        Assert.Throws<InvalidOperationException>(() => doc.Save(new MemoryStream()));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
    }

    [Theory]
    [InlineData("<p>x</p><div style='height:2000pt'></div>")]
    [InlineData("<table><tr><td rowspan=2 style='height:1800pt'>x</td><td>a</td></tr><tr><td>b</td></tr></table>")]
    public void Trailing_blank_height_saves_within_the_page_limit_as_without_one(string html)
    {
        // F-49: trailing blank pages are dropped without a limit, but a limit of 1 failed the same document.
        var free = Document();
        free.Body.AddHtml(PdfHtml.FromString(html));
        int pages = PdfPageReader.Save(free).Count;
        var limited = Document();
        limited.Body.MaxPages = pages;
        limited.Body.AddHtml(PdfHtml.FromString(html));
        Assert.Equal(pages, PdfPageReader.Save(limited).Count);
    }

    [Fact]
    public void Long_selectors_take_memory_only_for_the_elements_they_reach()
    {
        // F-50: each selector reserved two bits per element and compound for the whole document (5 GB for 200 rules of
        // 2,000 compounds over 50,000 elements).
        var html = new StringBuilder("<style>");
        for (int r = 0; r < 50; r++) html.Append(string.Concat(Enumerable.Repeat("div ", 1999))).Append(".k").Append(r).Append("{color:red}");
        html.Append("</style>");
        for (int i = 0; i < 50000; i++) html.Append(i < 50 ? "<i class='k" + i.ToString(CultureInfo.InvariantCulture) + "'>x</i>" : "<i>x</i>");
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = PdfHtml.FromString(html.ToString());
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // The old memo alone reserved about 1.25 GB here, on top of the parse.
        Assert.True(allocated < 1024L * 1024 * 1024, $"allocated {allocated / 1048576} MB");
    }

    [Fact]
    public void Ordinary_rule_sets_keep_the_compact_selector_memo()
    {
        // F-53: a dictionary entry per asked pair took 913 MB of allocations here, against 547 MB with two-bit arrays.
        var html = new StringBuilder("<style>");
        for (int r = 0; r < 200; r++) html.Append("div.a").Append(r).Append(" p span{color:red}");
        html.Append("</style>");
        for (int i = 0; i < 10000; i++) html.Append("<div class='a").Append(i % 200).Append("'><p><span>x</span></p></div>");
        _ = PdfHtml.FromString("<p>warm-up</p>");
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = PdfHtml.FromString(html.ToString());
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 750L * 1024 * 1024, $"allocated {allocated / 1048576} MB");
    }

    [Fact]
    public void Content_far_past_the_page_limit_fails_fast()
    {
        // F-52: with page moves never stopping, 4,000 forced breaks inside 250 bordered boxes took 3.4 s and 255 MB to
        // fail at a limit of 3 pages.
        var html = new StringBuilder(string.Concat(Enumerable.Repeat("<div style='border:1px solid'>", 250)));
        for (int i = 0; i < 4000; i++) html.Append("<p style='break-before:page'>x</p>");
        var doc = Document();
        doc.Body.MaxPages = 3;
        doc.Body.AddHtml(PdfHtml.FromString(html.ToString()));
        var watch = Stopwatch.StartNew();
        var ex = Assert.Throws<InvalidOperationException>(() => doc.Save(new MemoryStream()));
        Assert.Contains("needs more than 3 pages, but Body.MaxPages is 3", ex.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Failing_descendant_selectors_over_many_separate_deep_chains_load_quickly()
    {
        // F-43: 400 chains 250 deep with 20 failing rules took 17 s and 582 MB; each ancestor walk is now one lookup.
        var html = new StringBuilder("<style>");
        for (int r = 0; r < 20; r++) html.Append("p.r").Append(r).Append(" div div div span{color:red}");
        html.Append("</style>");
        for (int c = 0; c < 200; c++)
            html.Append(string.Concat(Enumerable.Repeat("<div>", 250))).Append("<span>x</span>").Append(string.Concat(Enumerable.Repeat("</div>", 250)));
        var watch = Stopwatch.StartNew();
        _ = PdfHtml.FromString(html.ToString());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Text_beside_an_inline_block_taller_than_a_page_stays_visible()
    {
        // F-44: "BEFORE" and "TAIL words" shared the tall line and were drawn at y -934, inside the clip.
        string lines = string.Concat(Enumerable.Range(1, 120).Select(i => $"line {i}<br>"));
        var pages = Render($"<body style='margin:0'><p>BEFORE <span style='display:inline-block'>{lines}</span> TAIL words</p><p>AFTER</p>");
        foreach (string text in new[] { "BEFORE", "TAIL words", "AFTER" })
        {
            var run = pages.SelectMany(Runs).Single(r => r.Text.Trim() == text);
            Assert.True(run.Y - run.Size * 0.2 >= Bottom - 0.01 && run.Y <= Top + 0.01, $"'{text}' at {run.Y}");
        }
    }

    [Fact]
    public void Columns_that_must_break_break_evenly()
    {
        // F-51: breaking one column at a time cut seven of eight non-breaking columns into 3-character pieces while one
        // stayed whole; the room is shared among the broken columns as browsers share a too-wide table.
        string run = string.Join("&nbsp;", Enumerable.Range(1, 10).Select(i => "word" + i.ToString(CultureInfo.InvariantCulture)));
        var texts = Texts(PdfHtml.FromString("<table><tr>" + string.Concat(Enumerable.Repeat($"<td>{run}</td>", 8)) + "</tr></table>"));
        Assert.True(texts.Min(t => t.Length) >= 8, string.Join(" | ", texts));
        Assert.True(texts.Max(t => t.Length) <= 2 * texts.Min(t => t.Length), string.Join(" | ", texts));
    }

    [Fact]
    public void A_long_non_breaking_run_beside_a_url_shares_the_room()
    {
        // F-45 (low, deferred): both columns break, in proportion to their content, as in a browser; nothing is clipped.
        string run = string.Join("&nbsp;", Enumerable.Range(1, 10).Select(i => "word" + i.ToString(CultureInfo.InvariantCulture)));
        var html = PdfHtml.FromString($"<table><tr><td>{run}</td><td>{Url}</td></tr></table>");
        var texts = Texts(html);
        Assert.Empty(html.Warnings);
        Assert.Equal(Url, string.Concat(texts.Where(t => Url.Contains(t))));
        Assert.DoesNotContain(Url, texts);
    }

    #endregion

    #region Private Methods

    // Whether a run is inside a clip rectangle that ends above the body bottom: drawn, but cut at the page bottom.
    private static bool Clipped(PdfPageReader.Page page, Run run) =>
        page.ClipRectangles().Any(c => c.Y >= Bottom - 1.01 && run.Y < c.Y + c.Height && run.Y < Bottom);

    private static IReadOnlyList<PdfPageReader.Page> Pages(PdfHtml html)
    {
        var doc = Document();
        doc.Body.AddHtml(html);
        return PdfPageReader.Save(doc);
    }

    private static List<string> Texts(PdfHtml html) => Pages(html).SelectMany(Runs).Select(r => r.Text).ToList();

    #endregion

    #endregion
}
