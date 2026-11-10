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
    public void Pages_only_open_boxes_would_decorate_also_stop_the_layout_early()
    {
        // F-54: the stop counts a page that an open bordered box will draw on, even before the box closes.
        var html = new StringBuilder(string.Concat(Enumerable.Repeat("<div style='border:1px solid'>", 250)));
        for (int i = 0; i < 4000; i++) html.Append("<div style='break-before:page;height:1pt'></div>");
        var doc = Document();
        doc.Body.MaxPages = 3;
        doc.Body.AddHtml(PdfHtml.FromString(html.ToString()));
        var watch = Stopwatch.StartNew();
        Assert.Throws<InvalidOperationException>(() => doc.Save(new MemoryStream()));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");
    }

    public static IEnumerable<object[]> ExactnessShapes() => new[]
    {
        "<p>x</p>" + Repeat("<br>", 300),
        "<p>x" + Repeat("<br>", 300) + "</p>",
        "<table><tr><td>x" + Repeat("<br>", 300) + "</td></tr></table>",
        "<p>x</p><table>" + Repeat("<tr><td style='height:30pt'></td></tr>", 120) + "</table>",
        "<p>x</p>" + Repeat("<p style='height:20pt'></p>", 200),
        "<p>x</p><pre>" + Repeat("\n", 300) + "</pre>",
        "<p>x</p>" + Repeat("<div style='break-before:page;height:1pt'></div>", 5),
        "<p>x</p>" + Repeat("<div style='padding-bottom:1000pt'></div>", 10),
        Repeat("<div style='padding-top:1000pt;padding-bottom:1000pt'>", 3) + "x",
        Repeat("<div style='border:1px solid;padding-bottom:400pt'>", 3) + "x",
        "<table><thead><tr><th>H</th></tr></thead>" + Repeat("<tr><td>r</td></tr>", 120) + "</table>",
        "<table border=1><tr><td>" + Repeat("line<br>", 90) + "</td></tr></table>",
        "<table><tr><td style='height:2000pt;border:1px solid'>x</td></tr></table><p>after</p>",
        "<p>x</p><div style='height:2000pt;break-after:page'></div>",
        "<div style='background:#eee'>" + Repeat("<p>t</p>", 80) + "</div>",
        Repeat("<p style='break-before:page'>x</p>", 5) + Repeat("<br>", 300),
        // F-56: boxes whose decoration draws only on their first or last page.
        "<div style='border-top:1px solid'><p>x</p>" + Repeat("<br>", 300) + "</div>",
        "<div style='border-bottom:1px solid'><p>x</p>" + Repeat("<br>", 300) + "</div>",
        Repeat("<div style='border-top:1px solid'>", 3) + "<p>x</p>" + Repeat("<div style='break-before:page;height:1pt'></div>", 4),
        "<table style='border-top:1px solid'><tr><td>x" + Repeat("<br>", 300) + "</td></tr></table>",
        "<table style='border-collapse:collapse;border-top:1px solid'><tr><td>x" + Repeat("<br>", 300) + "</td></tr></table>",
        "<table><tr><td><div style='border-top:1px solid'>x" + Repeat("<br>", 300) + "</div></td></tr></table>",
        "<div style='border-left:1px solid'><p>x</p>" + Repeat("<br>", 300) + "</div>",
        // F-59, F-60: a row that is clipped instead of split, and boxes that end pages after they start.
        "<p>x</p><table><tr><td style='padding-bottom:800pt;border:1px solid'><div style='padding-top:5pt'>x</div></td></tr></table><p>after</p>",
        Repeat("<p>t</p>", 20) + "<table><tr><td>a</td></tr><tr><td style='padding-bottom:800pt'><div style='padding-top:5pt'>x</div></td></tr><tr><td>b</td></tr></table>",
        Repeat("<div style='border-bottom:1px solid'>", 3) + "<p>x</p>" + Repeat("<div style='break-before:page;height:1pt'></div>", 4),
        Repeat("<div style='border-top:1px solid;border-bottom:1px solid'>", 3) + "<p>x</p>" + Repeat("<br>", 300),
        "<div style='border-bottom:1px solid;padding-bottom:" + (Top - Bottom).ToString(CultureInfo.InvariantCulture) + "pt'><p>x</p>" + Repeat("<br>", 60) + "</div>",
        // F-61, F-62: cells without room beside cells that split, with and without clipped content.
        "<table border=1><tr><td style='padding-bottom:800pt'>x</td><td>" + Repeat("line<br>", 90) + "</td></tr></table><p>after</p>",
        "<table border=1><tr><td style='padding-bottom:760pt'>" + Repeat("left<br>", 90) + "</td><td>" + Repeat("right<br>", 90) + "</td></tr></table>",
        "<p>x</p><table border=1><tr style='height:800pt'><td style='padding-bottom:765pt'><div style='margin-top:-300pt;padding-top:300pt'></div></td></tr></table><p>after</p>",
        // F-64: rows that move to a fresh page because a cell without room would be cut where they start.
        "<table border=1>" + Repeat("<tr><td style='padding:740pt 0 20pt'>x</td></tr>", 4) + "</table><p>after</p>",
        "<style>td{padding:440pt 4pt 16pt;border:1pt solid #888}</style><table><thead><tr><th style='height:300pt'>h</th></tr></thead>" + Repeat("<tr><td>x</td><td>y</td></tr>", 8) + "</table>",
        // F-65, F-67: rows judged by what they draw, and tables that start on the next page with their first row.
        "<table border=1>" + Repeat("<tr><td style='padding:735pt 0 22pt'><p>x</p></td></tr>", 4) + "</table><p>after</p>",
        "<p>before</p><table border=1><caption>c</caption><thead><tr><th>h</th></tr></thead><tr><td style='padding:700pt 0 60pt'>x</td></tr><tr><td>y</td></tr></table>",
        "<p>before</p><ol><li><table><tr><td style='padding:720pt 0 40pt'>x</td></tr></table></li></ol><p>after</p>",
    }.Select(html => new object[] { html });

    [Fact]
    public void Deep_plain_wrappers_with_many_page_moves_stay_cheap()
    {
        // F-57: an insert position per open box per page made 250 plain wrappers around 90,000 blank breaks take 4.4 s
        // and 1 GB although the document fits on one page.
        string html = "<style>.b{break-before:page;height:1pt}</style>" + Repeat("<div>", 250) + "<p>x</p>" + Repeat("<p class=b></p>", 90000);
        var doc = Document();
        doc.Body.MaxPages = 1;
        doc.Body.AddHtml(PdfHtml.FromString(html));
        var watch = Stopwatch.StartNew();
        Assert.Single(PdfPageReader.Save(doc));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
    }

    [Theory]
    [MemberData(nameof(ExactnessShapes))]
    public void A_page_limit_equal_to_the_page_count_saves_the_same_bytes_and_one_less_fails(string html)
    {
        // F-42, F-49, F-54: fitting documents must save exactly as without a limit, whatever blank pages layout used.
        byte[]? Save(int? maxPages)
        {
            var doc = Document();
            doc.Body.MaxPages = maxPages;
            doc.Body.AddHtml(PdfHtml.FromString(html));
            try
            {
                using var stream = new MemoryStream();
                doc.Save(stream);
                return stream.ToArray();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        var free = Save(null)!;
        int pages = PdfPageReader.Read(free).Count;
        Assert.Equal(free, Save(pages));
        if (pages > 1) Assert.Null(Save(pages - 1));
    }

    [Fact]
    public void The_selector_memo_holds_only_the_pages_it_writes()
    {
        // F-55, F-58: every selector up to 2 MB took a dense array, so 1,000 long rules held 2 GB for one pass.
        var root = new UnlimitedPDF.Models.Internal.Html.Parsing.HtmlElement("html", Array.Empty<KeyValuePair<string, string>>(), 1);
        var parent = root;
        for (int i = 0; i < 20; i++)
        {
            var div = new UnlimitedPDF.Models.Internal.Html.Parsing.HtmlElement("div", Array.Empty<KeyValuePair<string, string>>(), 1);
            parent.Append(div);
            parent = div;
        }
        var leaves = new List<UnlimitedPDF.Models.Internal.Html.Parsing.HtmlElement>();
        for (int i = 0; i < 100000; i++)
        {
            var br = new UnlimitedPDF.Models.Internal.Html.Parsing.HtmlElement("br", new[] { new KeyValuePair<string, string>("class", "c" + (i % 300).ToString(CultureInfo.InvariantCulture)) }, 1);
            parent.Append(br);
            leaves.Add(br);
        }
        var ordinals = new Dictionary<UnlimitedPDF.Models.Internal.Html.Parsing.HtmlElement, int> { [root] = 0 };
        for (var e = root; e.Children.FirstOrDefault() is UnlimitedPDF.Models.Internal.Html.Parsing.HtmlElement child && child.Name == "div"; e = child) ordinals[child] = ordinals.Count;
        foreach (var leaf in leaves) ordinals[leaf] = ordinals.Count;
        var memo = new UnlimitedPDF.Models.Internal.Html.Css.CssSelector.MatchMemo(ordinals);
        for (int r = 0; r < 300; r++)
        {
            Assert.True(UnlimitedPDF.Models.Internal.Html.Css.CssSelector.TryParseList(Repeat("div ", 79) + "br.c" + r.ToString(CultureInfo.InvariantCulture), out var selectors));
            Assert.False(selectors[0].Matches(leaves[r], memo));
        }
        // Rarely matching long rules touch a few 1 KB pages each, not two bits for every element (F-58).
        Assert.InRange(memo.Bytes, 1, 16L * 1024 * 1024);
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

    [Theory]
    [InlineData("<table><tr><td style='padding-bottom:800pt'><div style='padding-top:5pt'>x</div></td></tr></table>", false)]
    [InlineData("<table><tr><td style='border-top:800pt solid'><div style='padding-top:5pt'>x</div></td></tr></table>", true)]
    [InlineData("<table><thead><tr><th style='height:380pt'>h</th></tr></thead><tr><td style='padding:200pt 0'><div style='padding-top:5pt'>x</div></td></tr></table>", false)]
    [InlineData("<table><tr><td style='padding-bottom:800pt'><table><tr><td><div style='padding-top:5pt'>x</div></td></tr></table></td></tr></table>", false)]
    [InlineData("<table><tr><td style='padding-bottom:800pt'><ul><li>x</li></ul></td></tr></table>", false)]
    [InlineData("<table><tr><td style='padding-bottom:800pt'><hr>x</td></tr></table>", false)]
    [InlineData("<table><tr><td style='padding-bottom:760pt'><div style='padding-top:500000pt'>x</div></td></tr></table>", true)]
    public void A_cell_whose_padding_leaves_no_room_on_a_page_is_drawn_once_on_the_rows_first_page(string html, bool cut)
    {
        // F-59: the cell could never continue on a next page, so layout moved pages forever (5 GB after 15 s). The page
        // limit here only keeps a regression from hanging the run: with it, the old layout failed instead of saving.
        var source = PdfHtml.FromString(html);
        var doc = Document();
        doc.Body.MaxPages = 5;
        doc.Body.AddHtml(source);
        var watch = Stopwatch.StartNew();
        var pages = PdfPageReader.Save(doc);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");

        // Content that reaches past the body bottom is clipped there with a warning; content that fits is whole.
        var x = Assert.Single(Runs(pages[0]), r => r.Text == "x");
        Assert.Equal(cut, x.Y < Bottom);
        Assert.Equal(cut, source.Warnings.Any(w => w.Kind == PdfHtmlWarningKind.ContentClipped && w.Message.Contains("not continued", StringComparison.Ordinal)));
        if (cut) Assert.Contains(pages[0].ClipRectangles(), c => c.Y >= Bottom - 1.01);
    }

    [Fact]
    public void A_padded_cell_inside_a_split_cell_keeps_its_content_and_height()
    {
        // F-59, F-62: the same cell one table deeper. The outer row splits, and the inner row keeps its 800 pt.
        var html = PdfHtml.FromString("<table><tr><td>" + Repeat("line<br>", 90) + "<table><tr><td style='padding-bottom:800pt'><div style='padding-top:5pt'>x</div></td></tr></table>TAIL</td></tr></table>");
        var doc = Document();
        doc.Body.MaxPages = 5;
        doc.Body.AddHtml(html);
        var pages = PdfPageReader.Save(doc);
        Assert.Equal(3, pages.Count);
        Assert.Equal(90, pages.SelectMany(Runs).Count(r => r.Text == "line"));
        Assert.Contains(Runs(pages[1]), r => r.Text == "x");
        Assert.Contains(Runs(pages[2]), r => r.Text == "TAIL");
        Assert.Empty(html.Warnings);
    }

    [Theory]
    [InlineData("<div style='margin-top:-300pt;padding-top:300pt'></div>")]
    [InlineData("<div style='margin-bottom:-400pt'><table><tr><td style='height:300pt'></td></tr></table></div>")]
    public void A_cell_with_a_sliver_of_room_per_page_is_not_laid_out_a_sliver_at_a_time(string content)
    {
        // F-61: the padding leaves 0.01 pt per page. Content whose height a negative margin cancels counted as empty,
        // so the cell was split and 300 pt of blank space took 30,000 pages (about 5 s; 90,000 pt never finished).
        string padding = (Top - Bottom - 0.75 - 0.01).ToString(CultureInfo.InvariantCulture);
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString($"<table><tr style='height:800pt'><td style='padding-bottom:{padding}pt'>{content}</td></tr></table><p>AFTER</p>"));
        var watch = Stopwatch.StartNew();
        var pages = PdfPageReader.Save(doc);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");
        Assert.Equal(2, pages.Count);
        Assert.Contains(Runs(pages[1]), r => r.Text == "AFTER");
    }

    [Fact]
    public void A_cell_without_room_does_not_stop_the_other_cells_of_its_row_from_splitting()
    {
        // F-62: one such cell made the whole row a single clipped page, hiding lines the other cell had shown before.
        string lines = string.Concat(Enumerable.Range(1, 90).Select(i => $"line {i}<br>"));
        var html = PdfHtml.FromString($"<table><tr><td style='padding-bottom:800pt'>x</td><td>{lines}</td></tr></table>");
        var pages = Pages(html);
        Assert.Equal(2, pages.Count);
        var runs = pages.SelectMany(Runs).ToList();
        Assert.Equal(90, runs.Count(r => r.Text.StartsWith("line ", StringComparison.Ordinal) && r.Y >= Bottom));
        Assert.Contains(Runs(pages[0]), r => r.Text == "x" && r.Y >= Bottom);
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Tall_content_in_a_cell_without_room_is_clipped_alone()
    {
        // F-62: the padded cell cannot continue, so its lines past the first page are clipped with one warning; the
        // other cell still shows every line.
        string a = string.Concat(Enumerable.Range(1, 90).Select(i => $"left {i}<br>")), b = string.Concat(Enumerable.Range(1, 90).Select(i => $"right {i}<br>"));
        var html = PdfHtml.FromString($"<table><tr><td style='padding-bottom:760pt'>{a}</td><td>{b}</td></tr></table>");
        var pages = Pages(html);
        var runs = pages.SelectMany(Runs).ToList();
        Assert.Equal(90, runs.Count(r => r.Text.StartsWith("right ", StringComparison.Ordinal) && r.Y >= Bottom));
        Assert.All(pages.Skip(1), page => Assert.DoesNotContain(Runs(page), r => r.Text.StartsWith("left ", StringComparison.Ordinal)));
        Assert.Contains(Runs(pages[0]), r => r.Text == "left 1");
        Assert.Single(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped && w.Message.Contains("not continued", StringComparison.Ordinal));
    }

    [Fact]
    public void A_row_whose_cell_cannot_continue_moves_to_a_fresh_page_when_its_content_fits_there()
    {
        // F-64: each row is a few points taller than the body, so the next row started lower on its page each time; the
        // third text was cut at the page bottom and the fourth was clipped away, although each fits on a fresh page.
        string rows = string.Concat(Enumerable.Range(0, 4).Select(i => $"<tr><td style='padding:740pt 0 20pt'>T{i}</td></tr>"));
        var html = PdfHtml.FromString($"<table>{rows}</table>");
        var pages = Pages(html);
        for (int i = 0; i < 4; i++)
            Assert.Single(pages.SelectMany(page => Runs(page).Where(r => r.Text == $"T{i}" && r.Y >= Bottom + 3 && !Clipped(page, r))));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Padded_rows_below_a_repeated_header_keep_every_cell_text()
    {
        // F-64: with a repeated header the padding leaves the cells no room to continue; 16 of these 20 texts were cut.
        string rows = string.Concat(Enumerable.Range(0, 10).Select(i => $"<tr><td>A{i}</td><td>B{i}</td></tr>"));
        var html = PdfHtml.FromString("<style>td{padding:440pt 4pt 16pt;border:1pt solid #888}</style><table><thead><tr><th style='height:300pt'>HEAD</th><th>H2</th></tr></thead>" + rows + "</table><p>AFTER</p>");
        var pages = Pages(html);
        foreach (string text in Enumerable.Range(0, 10).SelectMany(i => new[] { $"A{i}", $"B{i}" }).Append("AFTER"))
            Assert.Single(pages.SelectMany(page => Runs(page).Where(r => r.Text == text && r.Y >= Bottom + 3 && !Clipped(page, r))));

        // Every row is below a header.
        Assert.All(pages, page => Assert.True(Runs(page).Count(r => r.Text == "HEAD") == 1 || Runs(page).All(r => r.Text == "AFTER")));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_row_whose_cell_content_fits_no_page_still_starts_where_it_is()
    {
        // F-64: moving the row would not save the content, so it stays below the text before it and is clipped.
        string lines = string.Concat(Enumerable.Range(1, 90).Select(i => $"left {i}<br>"));
        var html = PdfHtml.FromString($"<p>BEFORE</p><table><tr><td style='padding-bottom:760pt'>{lines}</td><td>RIGHT</td></tr></table>");
        var pages = Pages(html);
        var first = Runs(pages[0]).Select(r => r.Text).ToList();
        Assert.Contains("BEFORE", first);
        Assert.Contains("left 1", first);
        Assert.Contains("RIGHT", first);
        Assert.Single(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped && w.Message.Contains("not continued", StringComparison.Ordinal));
    }

    [Fact]
    public void Text_that_fits_a_fresh_page_is_shown_although_the_margin_below_it_does_not_fit()
    {
        // F-65: the move and the clip were judged by the content's layout height, so the paragraph's bottom margin
        // counted as content that fits no page: three of the four texts were clipped away.
        string rows = string.Concat(Enumerable.Range(0, 4).Select(i => $"<tr><td style='padding:735pt 0 22pt'><p>T{i}</p></td></tr>"));
        var html = PdfHtml.FromString($"<table>{rows}</table>");
        var pages = Pages(html);
        for (int i = 0; i < 4; i++)
            Assert.Single(pages.SelectMany(page => Runs(page).Where(r => r.Text == $"T{i}" && r.Y >= Bottom + 3 && !Clipped(page, r))));
        Assert.Empty(html.Warnings);
    }

    [Theory]
    [InlineData("<p>Intro</p><table style='border-spacing:0'><tr><td style='padding:700pt 0 56pt'><p>TEXT</p></td></tr></table><p>AFTER</p>", 2)]
    [InlineData("<table><tr><td style='padding:748.5pt 0 10pt'>TEXT</td></tr></table>", 1)]
    [InlineData("<p>Intro</p><table><tr><td>first</td></tr><tr><td style='padding:676pt 0 80pt'><p>TEXT</p></td></tr></table>",1)]
    public void A_row_stays_where_it_is_when_nothing_it_draws_is_cut(string source, int pageCount)
    {
        // F-65: only blank height (a last margin, a line's leading) reaches past the page bottom. The row moved to the
        // next page for it, leaving a blank or nearly blank page, or was clipped with a warning although nothing was cut.
        var html = PdfHtml.FromString(source);
        var pages = Pages(html);
        Assert.Equal(pageCount, pages.Count);
        Assert.Contains(Runs(pages[0]), r => r.Text == "TEXT" && r.Y >= Bottom);
        Assert.Empty(pages[0].ClipRectangles());
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_row_does_not_move_to_a_page_from_which_one_of_its_cells_could_no_longer_continue()
    {
        // F-66: page 3 has a small body. From page 1 the first cell continues on page 2; moved to page 2, it would have
        // to continue on page 3, where its padding leaves no room, so it was drawn once and lost 25 of its lines.
        string lines = string.Concat(Enumerable.Range(1, 60).Select(i => $"line {i}<br>"));
        var html = PdfHtml.FromString($"<div style='height:100pt'></div><table style='border-spacing:0'><tr><td style='padding:260pt 0 0'>{lines}</td><td style='padding:740pt 0 20pt'>SIDE</td></tr></table>");
        var doc = Document();
        doc.AddPageSettings(3).BodyMarginTop = 500;
        doc.Body.AddHtml(html);
        var pages = PdfPageReader.Save(doc);
        Assert.Equal(2, pages.Count);
        Assert.Equal(60, pages.SelectMany(Runs).Count(r => r.Text.StartsWith("line ", StringComparison.Ordinal) && r.Y >= Bottom));
        Assert.Contains(Runs(pages[0]), r => r.Text == "line 1");
    }

    [Fact]
    public void A_one_row_table_at_the_top_of_a_page_never_leaves_a_blank_page_before_it()
    {
        // F-67: the row moved to page 2 on its own, although the table already started at the top of page 1.
        var html = PdfHtml.FromString("<table><tr><td style='padding:754.5pt 0 10pt'>TEXT</td></tr></table>");
        var pages = Pages(html);
        Assert.Single(pages);
        Assert.Contains(Runs(pages[0]), r => r.Text == "TEXT");
        Assert.Single(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped && w.Message.Contains("not continued", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("<p>BEFORE</p><table border=1><caption>CAPTION</caption><thead><tr><th>HEAD</th></tr></thead><tr><td style='padding:700pt 0 60pt'>TEXT</td></tr><tr><td>LAST</td></tr></table>", "CAPTION", "HEAD")]
    [InlineData("<p>BEFORE</p><ol><li><table><tr><td style='padding:720pt 0 40pt'>TEXT</td></tr></table></li></ol>", "1.", "1.")]
    [InlineData("<p>BEFORE</p><table><tr><td style='padding:735pt 0 22pt'><p>TEXT</p></td></tr></table>", "TEXT", "TEXT")]
    public void A_first_row_that_moves_takes_the_start_of_its_table_with_it(string source, string first, string second)
    {
        // F-67: the row moved alone and left the caption, the header, an empty frame, or the list number on the page
        // before. The table now starts on the next page, where the cell's text is whole.
        var html = PdfHtml.FromString(source);
        var pages = Pages(html);
        Assert.Equal("BEFORE", Assert.Single(Runs(pages[0])).Text);
        var texts = Runs(pages[1]).Select(r => r.Text).ToList();
        Assert.Contains(first, texts);
        Assert.Contains(second, texts);
        Assert.Contains(Runs(pages[1]), r => r.Text == "TEXT" && r.Y >= Bottom + 3 && !Clipped(pages[1], r));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_first_row_moves_alone_when_its_text_fits_a_page_but_not_below_the_caption()
    {
        // F-67: the text fits on a bare page but not below the caption, so starting the table on the next page would
        // not show it. The table stays below the text before it and only the row moves, which keeps the text.
        var html = PdfHtml.FromString("<p>BEFORE</p><table><caption>CAPTION</caption><tr><td style='padding:745pt 0 20pt'>TEXT</td></tr></table>");
        var pages = Pages(html);
        var first = Runs(pages[0]).Select(r => r.Text).ToList();
        Assert.Contains("BEFORE", first);
        Assert.Contains("CAPTION", first);
        Assert.Contains(Runs(pages[1]), r => r.Text == "TEXT" && r.Y >= Bottom + 3 && !Clipped(pages[1], r));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_first_body_row_moves_below_a_header_that_is_not_repeated_also_at_the_top_of_a_page()
    {
        // F-67: the header takes more than half the page, so it is not repeated. The page the row leaves holds the
        // header, and the next page shows the cell's text, which was cut below the header.
        var html = PdfHtml.FromString("<table><thead><tr><th style='height:400pt'>HEAD</th></tr></thead><tr><td style='padding:555pt 0 204pt'>TEXT</td></tr></table>");
        var pages = Pages(html);
        Assert.Contains(Runs(pages[0]), r => r.Text == "HEAD");
        Assert.Contains(Runs(pages[1]), r => r.Text == "TEXT" && r.Y >= Bottom + 3 && !Clipped(pages[1], r));
        Assert.DoesNotContain(html.Warnings, w => w.Message.Contains("not continued", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("<table><caption>CAPTION</caption><tr><td style='padding:745pt 0 20pt'>TEXT</td></tr></table>")]
    [InlineData("<p style='break-after:page'>BEFORE</p><table><caption>CAPTION</caption><tr><td style='padding:745pt 0 20pt'>TEXT</td></tr></table>")]
    [InlineData("<table><caption>CAPTION</caption><thead><tr><th>HEAD</th></tr></thead><tr><td style='padding:728pt 0 20pt'>TEXT</td></tr></table>")]
    public void A_first_row_stays_with_a_table_that_starts_at_the_top_of_a_page(string source)
    {
        // F-67: moving the row would leave a page with nothing but the caption, so the row stays and is clipped.
        var html = PdfHtml.FromString(source);
        var pages = Pages(html);
        var captionPage = Assert.Single(pages, page => Runs(page).Any(r => r.Text == "CAPTION"));
        Assert.Contains(Runs(captionPage), r => r.Text == "TEXT");
        Assert.Single(html.Warnings, w => w.Kind == PdfHtmlWarningKind.ContentClipped && w.Message.Contains("not continued", StringComparison.Ordinal));
    }

    [Fact]
    public void A_table_whose_header_does_not_fit_here_is_not_moved_for_its_first_row()
    {
        // F-67: the header moves to the next page on its own, so the first row starts below it there and nothing of
        // it is cut. Moving the whole table for the left cell would put the caption above the header and cut the
        // right cell's text, which fits below the header alone.
        var html = PdfHtml.FromString("<div style='height:560pt'></div><table style='border-spacing:0'><caption>CAPTION</caption><thead><tr><th style='padding:0;height:368pt'>HEAD</th></tr></thead>"
            + "<tr><td style='padding:87pt 0 314pt'>LEFT</td><td style='padding:386pt 0 9.9pt'>RIGHT</td></tr></table>");
        var pages = Pages(html);
        Assert.Contains(Runs(pages[0]), r => r.Text == "CAPTION");
        Assert.Contains(Runs(pages[1]), r => r.Text == "LEFT" && r.Y >= Bottom);
        Assert.Contains(Runs(pages[1]), r => r.Text == "RIGHT" && r.Y >= Bottom + 3 && !Clipped(pages[1], r));
        Assert.Empty(html.Warnings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<tr><td>first</td></tr>")]
    public void A_tall_row_whose_cells_can_all_continue_still_splits_where_it_starts(string rowBefore)
    {
        // F-68: only a cell that cannot continue may move its row. This cell keeps 267 pt of room per page, and its
        // text would fit on a fresh page; moving the row for it would change every ordinary long table.
        string lines = string.Concat(Enumerable.Range(1, 20).Select(i => $"cell {i}<br>"));
        var html = PdfHtml.FromString(Repeat("BEFORE<br>", 10) + $"<table>{rowBefore}<tr><td style='padding:400pt 0 100pt'>{lines}</td></tr></table>");
        var pages = Pages(html);
        Assert.Contains(Runs(pages[0]), r => r.Text == "cell 1" && r.Y >= Bottom);
        Assert.Contains(Runs(pages[1]), r => r.Text == "cell 20");
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_row_moves_for_any_of_its_cells_that_cannot_continue()
    {
        // F-68: the cell that would be cut is the last of its row, after a cell that can continue and a cell that
        // cannot continue either but is not cut.
        var html = PdfHtml.FromString(Repeat("BEFORE<br>", 5) + "<table><tr><td>first</td></tr><tr><td>LEFT</td><td style='padding-bottom:760pt'>MIDDLE</td><td style='padding:740pt 0 20pt'>RIGHT</td></tr></table>");
        var pages = Pages(html);
        Assert.Contains(Runs(pages[0]), r => r.Text == "first");
        Assert.Contains(Runs(pages[1]), r => r.Text == "LEFT");
        Assert.Contains(Runs(pages[1]), r => r.Text == "MIDDLE");
        Assert.Contains(Runs(pages[1]), r => r.Text == "RIGHT" && r.Y >= Bottom + 3 && !Clipped(pages[1], r));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void A_cell_that_cannot_continue_and_draws_nothing_does_not_move_its_row()
    {
        // F-68: the padded cell is empty, so nothing of it can be cut; the row starts below the text before it.
        string lines = string.Concat(Enumerable.Range(1, 60).Select(i => $"cell {i}<br>"));
        var html = PdfHtml.FromString(Repeat("BEFORE<br>", 5) + $"<table><tr><td>first</td><td>first</td></tr><tr><td style='padding-top:756pt'></td><td>{lines}</td></tr></table>");
        var pages = Pages(html);
        Assert.Contains(Runs(pages[0]), r => r.Text == "cell 1" && r.Y >= Bottom);
        Assert.Equal(60, pages.SelectMany(Runs).Count(r => r.Text.StartsWith("cell ", StringComparison.Ordinal) && r.Y >= Bottom));
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Blank_height_inside_a_split_cell_costs_time_in_step_with_its_pages()
    {
        // F-63: drawing each page scanned every page fragment of the cell, so 4e7 pt took 10.7 s (0.23 s outside a table).
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString("<table><tr><td><div style='height:40000000pt'></div></td></tr></table>"));
        var watch = Stopwatch.StartNew();
        PdfPageReader.Save(doc);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
    }

    [Fact]
    public void An_empty_padded_cell_beside_tall_text_still_splits_with_its_row()
    {
        // F-59: only cells with content need room on each page; an empty cell keeps its height across the pages (F-39).
        var html = PdfHtml.FromString("<table><tr><td style='padding-bottom:1700pt'></td><td>" + Repeat("line<br>", 90) + "</td></tr></table><p>AFTER</p>");
        var pages = Pages(html);
        Assert.Equal(3, pages.Count);
        Assert.Equal(90, pages.SelectMany(Runs).Count(r => r.Text == "line"));
        Assert.Contains(Runs(pages[2]), r => r.Text == "AFTER");
        Assert.Empty(html.Warnings);
    }

    [Theory]
    [InlineData("border-top:1px solid", null)]
    [InlineData("border-top:1px solid", 1)]
    [InlineData("border-bottom:1px solid", 1)]
    public void Deep_wrappers_with_only_a_top_or_bottom_border_cost_no_more_than_plain_ones(string style, int? maxPages)
    {
        // F-60: such a box draws on its first and last pages only, yet every page it spanned was visited and given a
        // position: 250 wrappers around 90,000 blank breaks took about 70 s and 1.3 GB.
        string html = "<style>.b{break-before:page;height:1pt}</style>" + Repeat($"<div style='{style}'>", 250) + "<p>x</p>" + Repeat("<p class=b></p>", 90000);
        var doc = Document();
        doc.Body.MaxPages = maxPages;
        doc.Body.AddHtml(PdfHtml.FromString(html));
        var watch = Stopwatch.StartNew();

        // The top borders are all on the first page; a bottom border is drawn where the box ends, 90,000 pages on.
        if (style.StartsWith("border-top", StringComparison.Ordinal)) Assert.Single(PdfPageReader.Save(doc));
        else Assert.Throws<InvalidOperationException>(() => doc.Save(new MemoryStream()));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
    }

    #endregion

    #region Private Methods

    // Whether a run is inside a clip rectangle that ends above the body bottom: drawn, but cut at the page bottom.
    private static bool Clipped(PdfPageReader.Page page, Run run) =>
        page.ClipRectangles().Any(c => c.Y >= Bottom - 1.01 && run.Y < c.Y + c.Height && run.Y < Bottom);

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

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
