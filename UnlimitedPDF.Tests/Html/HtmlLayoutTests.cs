using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// HTML laid out in the body flow: text, fonts, wrapping, alignment, boxes, lists, and pagination at line and block
/// boundaries (WR-038, AC-01, AC-08 to AC-10, AC-13, AC-16).
/// </summary>
public class HtmlLayoutTests
{
    #region Fields and Constants

    // A4 with 36 pt margins: the body runs from x 36 to 559.28 and from y 805.89 down to 36.
    internal const double Left = 36;
    internal const double Right = 595.28 - 36;
    internal const double Top = 841.89 - 36;
    internal const double Bottom = 36;

    private static readonly Regex RunPattern = new(@"q BT /(HF\d+) ([\d.]+) Tf ([\d. ]+) rg (?:([-\d.]+) Tw )?(?:([-\d.]+) Tc )?1 0 0 1 ([-\d.]+) ([-\d.]+) Tm \(((?:\\.|[^\\)])*)\) Tj ET Q", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_paragraph_uses_the_serif_default_and_starts_inside_the_body_margin()
    {
        var page = Assert.Single(Render("<p>Hello world</p>"));
        var run = Assert.Single(Runs(page));
        Assert.Equal("HF5", run.Font); // Times-Roman
        Assert.Equal(12, run.Size);
        Assert.Equal("Hello world", run.Text);
        Assert.Equal(Left + 6, run.X, 3); // body margin 8 px
        Assert.InRange(run.Y, Top - 30, Top - 6);
        Assert.Contains("/HF5 ", page.Content);
    }

    [Fact]
    public void Long_text_wraps_at_spaces_inside_the_body_width()
    {
        string words = string.Join(" ", Enumerable.Range(1, 120).Select(i => "word" + i));
        var runs = Runs(Assert.Single(Render($"<p style='margin:0'>{words}</p>", bodyMargin: false)));
        Assert.True(runs.Count > 5);
        Assert.All(runs, r => Assert.Equal(Left, r.X, 3));
        Assert.All(runs, r => Assert.True(r.X + Width(r) <= Right + 0.01, $"{r.Text} overflows"));
        Assert.Equal(string.Join(" ", runs.Select(r => r.Text)), words);
        var baselines = runs.Select(r => r.Y).ToList();
        Assert.Equal(baselines.OrderByDescending(y => y), baselines);
        Assert.Equal(14.4, baselines[0] - baselines[1], 3); // normal line height 1.2
    }

    [Fact]
    public void A_word_wider_than_the_line_breaks_inside_itself()
    {
        string word = new string('W', 80);
        var runs = Runs(Assert.Single(Render($"<p style='margin:0'>{word}</p>", bodyMargin: false)));
        Assert.True(runs.Count >= 2);
        Assert.Equal(word, string.Concat(runs.Select(r => r.Text)));
        Assert.All(runs, r => Assert.True(r.X + Width(r) <= Right + 0.01));
    }

    [Fact]
    public void Bold_italic_monospace_and_headings_select_fonts_and_sizes()
    {
        var runs = Runs(Assert.Single(Render("<h1>Title</h1><p><b>b</b> <i>i</i> <b><i>bi</i></b> <code>c</code> <span style='font-family: Arial'>a</span></p>")));
        Assert.Equal(("HF6", 24d), (runs[0].Font, runs[0].Size)); // Times-Bold, 2em
        Assert.Equal(new[] { "HF6", "HF7", "HF8", "HF9", "HF1" }, runs.Skip(1).Where(r => r.Text.Trim().Length > 0).Select(r => r.Font).Take(5));
    }

    [Fact]
    public void Western_european_characters_are_written_as_WinAnsi_and_others_as_question_marks()
    {
        var page = Assert.Single(Render("<p>Café €5 ₹9</p>"));
        Assert.Contains(@"(Caf\351 \2005 ?9) Tj", page.Content);
    }

    [Theory]
    [InlineData("left", 0)]
    [InlineData("right", 1)]
    [InlineData("center", 0.5)]
    public void Text_aligns_within_the_block(string align, double share)
    {
        var run = Assert.Single(Runs(Assert.Single(Render($"<p style='margin:0; text-align:{align}'>Short</p>", bodyMargin: false))));
        double free = Right - Left - Width(run);
        Assert.Equal(Left + free * share, run.X, 2);
    }

    [Fact]
    public void Justified_lines_spread_their_spaces_except_the_last()
    {
        string words = string.Join(" ", Enumerable.Range(1, 60).Select(i => "lorem" + i));
        var runs = Runs(Assert.Single(Render($"<p style='margin:0; text-align: justify'>{words}</p>", bodyMargin: false)));
        Assert.True(runs[0].WordSpacing > 0);
        Assert.Equal(Right, runs[0].X + Width(runs[0]) + runs[0].WordSpacing * runs[0].Text.Count(c => c == ' '), 1);
        Assert.Equal(0, runs[runs.Count - 1].WordSpacing);
    }

    [Fact]
    public void White_space_collapses_except_in_pre()
    {
        var runs = Runs(Assert.Single(Render("<p>a   \n  b</p><pre>x    y\n  z</pre>")));
        Assert.Equal("a b", runs[0].Text);
        Assert.Equal("x    y", runs[1].Text);
        Assert.Equal("  z", runs[2].Text);
        Assert.Equal("HF9", runs[1].Font);
    }

    [Fact]
    public void Line_breaks_letter_spacing_and_decorations_are_drawn()
    {
        var page = Assert.Single(Render("<p>one<br>two</p><p style='letter-spacing: 2px'><u>under</u> <s>strike</s></p>"));
        var runs = Runs(page);
        Assert.Equal(new[] { "one", "two" }, runs.Take(2).Select(r => r.Text));
        Assert.True(runs[0].Y > runs[1].Y);
        Assert.Equal(1.5, runs[2].CharacterSpacing);
        Assert.Equal(2, Regex.Matches(page.Content, @" w [\d.]+ [\d.]+ m [\d.]+ [\d.]+ l S Q").Count);
    }

    [Fact]
    public void Boxes_draw_backgrounds_before_their_text_and_borders_around_them()
    {
        var page = Assert.Single(Render("<div style='background-color:#eeeeee; border: 2px dashed red; padding: 10px; margin: 0'>Boxed</div>", bodyMargin: false));
        int fill = page.Content.IndexOf(" rg " + "36 ", StringComparison.Ordinal);
        int text = page.Content.IndexOf("(Boxed) Tj", StringComparison.Ordinal);
        Assert.True(fill >= 0 && fill < text);
        Assert.Equal(4, Regex.Matches(page.Content, @"1 0 0 RG 1.5 w \[4.5 4.5\] 0 d").Count);
        var run = Assert.Single(Runs(page));
        Assert.Equal(Left + 1.5 + 7.5, run.X, 3);
    }

    [Fact]
    public void Widths_and_auto_margins_center_a_block()
    {
        var run = Assert.Single(Runs(Assert.Single(Render("<div style='width: 200pt; margin: 0 auto'>x</div>", bodyMargin: false))));
        Assert.Equal(Left + (Right - Left - 200) / 2, run.X, 3);
    }

    [Fact]
    public void Vertical_margins_collapse_between_siblings()
    {
        var runs = Runs(Assert.Single(Render("<p style='margin: 0 0 20pt'>a</p><p style='margin: 10pt 0 0'>b</p>", bodyMargin: false)));
        Assert.Equal(14.4 + 20, runs[0].Y - runs[1].Y, 3);
    }

    [Fact]
    public void Lists_number_and_mark_their_items()
    {
        var page = Assert.Single(Render("<ol start=3><li>three<li>four</ol><ol type=I><li>Scope<ol type=a><li>Phase</ol></ol><ol reversed><li>b<li>a</ol><ul><li>dot</ul><ul style='list-style: square'><li>sq</ul>"));
        var texts = Runs(page).Select(r => r.Text).ToList();
        Assert.Contains("3.", texts);
        Assert.Contains("4.", texts);
        Assert.Contains("I.", texts);
        Assert.Contains("a.", texts);
        Assert.Contains("2.", texts);
        Assert.Contains("1.", texts);
        Assert.Contains(" c ", page.Content); // the disc is a filled circle
        var three = Runs(page).First(r => r.Text == "three");
        var marker = Runs(page).First(r => r.Text == "3.");
        Assert.Equal(three.Y, marker.Y, 3);
        Assert.True(marker.X + Width(marker) < three.X);
    }

    [Fact]
    public void Lists_nest_six_levels_with_their_markers_indented()
    {
        string nested = "<ul><li>1<ol><li>2<ul><li>3<ol type=i><li>4<ul><li>5<ol type=A><li>6</ol></ul></ol></ul></ol></ul>";
        var runs = Runs(Assert.Single(Render(nested)));
        var items = Enumerable.Range(1, 6).Select(n => runs.First(r => r.Text == n.ToString(CultureInfo.InvariantCulture))).ToList();
        for (int i = 1; i < 6; i++) Assert.True(items[i].X > items[i - 1].X);
        Assert.Contains(runs, r => r.Text == "i.");
        Assert.Contains(runs, r => r.Text == "A.");
    }

    [Fact]
    public void Html_mixes_with_other_body_content_in_order()
    {
        var doc = Document();
        doc.Body.AddText("Before", fontSize: 10);
        doc.Body.AddHtml(PdfHtml.FromString("<p style='margin:0'>Middle</p><body style='margin:0'>"), spacing: 6);
        doc.Body.AddText("After", spacing: 6);
        var page = Assert.Single(PdfPageReader.Save(doc));
        int before = page.Content.IndexOf("(Before) Tj", StringComparison.Ordinal);
        int middle = page.Content.IndexOf("(Middle) Tj", StringComparison.Ordinal);
        int after = page.Content.IndexOf("(After) Tj", StringComparison.Ordinal);
        Assert.True(before < middle && middle < after);
        var texts = page.LayoutTexts();
        Assert.True(texts.First(t => t.Text == "Before").Baseline > Runs(page).Single().Y);
        Assert.True(Runs(page).Single().Y > texts.First(t => t.Text == "After").Baseline);
    }

    [Fact]
    public void Long_html_continues_on_new_pages_with_headers_and_footers_and_never_splits_a_line()
    {
        var doc = Document();
        doc.Footer.Height = 20;
        doc.Footer.AddText("Page {page} of {pages}", 0, 0);
        string paragraphs = string.Concat(Enumerable.Range(1, 120).Select(i => $"<p>Paragraph {i} with some text in it.</p>"));
        doc.Body.AddHtml(PdfHtml.FromString(paragraphs));
        var pdf = PdfPageReader.SaveBytes(doc);
        PdfStructureChecker.AssertValid(pdf);
        var pages = PdfPageReader.Read(pdf);
        Assert.True(pages.Count >= 3);
        int count = 0;
        for (int i = 0; i < pages.Count; i++)
        {
            Assert.True(pages[i].Shows($"Page {i + 1} of {pages.Count}"));
            foreach (var run in Runs(pages[i]))
            {
                Assert.True(run.Y - run.Size * 0.2 >= Bottom + 20 - 0.01, $"line below the body on page {i + 1}");
                Assert.True(run.Y + run.Size * 0.8 <= Top + 0.01);
                count++;
            }
        }
        Assert.Equal(120, count);
        Assert.Equal(Enumerable.Range(1, 120).Select(i => $"Paragraph {i} with some text in it."), pages.SelectMany(Runs).Select(r => r.Text));
    }

    [Fact]
    public void Forced_breaks_start_new_pages()
    {
        var pages = Render("<p>one</p><p style='break-before: page'>two</p><p style='page-break-after: always'>three</p><p>four</p>");
        Assert.Equal(3, pages.Count);
        Assert.Equal(new[] { "one" }, Runs(pages[0]).Select(r => r.Text));
        Assert.Equal(new[] { "two", "three" }, Runs(pages[1]).Select(r => r.Text));
        Assert.Equal(new[] { "four" }, Runs(pages[2]).Select(r => r.Text));
    }

    [Fact]
    public void Break_inside_avoid_moves_a_block_that_fits_on_the_next_page()
    {
        string filler = string.Concat(Enumerable.Range(1, 48).Select(i => $"<p style='margin:0'>line {i}</p>"));
        string kept = "<div style='break-inside: avoid'>" + string.Concat(Enumerable.Range(1, 6).Select(i => $"<p style='margin:0'>kept {i}</p>")) + "</div>";
        var pages = Render(filler + kept, bodyMargin: false);
        Assert.Equal(2, pages.Count);
        Assert.DoesNotContain(Runs(pages[0]), r => r.Text.StartsWith("kept", StringComparison.Ordinal));
        Assert.Equal(6, Runs(pages[1]).Count(r => r.Text.StartsWith("kept", StringComparison.Ordinal)));
    }

    [Fact]
    public void Display_none_is_skipped_and_empty_html_adds_nothing()
    {
        Assert.Empty(Runs(Assert.Single(Render("<p style='display:none'>hidden</p>"))));
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString(""));
        var pdf = PdfPageReader.SaveBytes(doc);
        PdfStructureChecker.AssertValid(pdf);
        Assert.Single(PdfPageReader.Read(pdf));
    }

    [Fact]
    public void Remaining_height_counts_the_html()
    {
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString("<p style='margin:0'>a</p><p style='margin:0'>b</p><body style='margin:0'>"));
        Assert.Equal(Top - Bottom - 2 * 14.4, doc.Body.RemainingHeight, 3);
    }

    [Fact]
    public void Pages_without_html_text_get_no_font_resources()
    {
        var doc = Document();
        doc.Body.AddText("plain");
        doc.Body.PageBreak();
        doc.Body.AddHtml(PdfHtml.FromString("<p>html</p>"));
        var pages = PdfPageReader.Save(doc);
        string pdf = Encoding.ASCII.GetString(PdfPageReader.SaveBytes(doc));
        Assert.Equal(2, pages.Count);
        Assert.Contains("/Font << /F1 3 0 R /HF5 ", pdf);
        Assert.Single(Regex.Matches(pdf, "/Font <<"));
        Assert.Contains("/BaseFont /Times-Roman", pdf);
    }

    #endregion

    #region Internal Methods

    internal static PdfDocument Document() => new() { MarginTop = 36, MarginBottom = 36, MarginLeft = 36, MarginRight = 36 };

    internal static IReadOnlyList<PdfPageReader.Page> Render(string html, bool bodyMargin = true)
    {
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString(bodyMargin ? html : "<body style='margin:0'>" + html));
        var pdf = PdfPageReader.SaveBytes(doc);
        PdfStructureChecker.AssertValid(pdf);
        return PdfPageReader.Read(pdf);
    }

    internal static List<Run> Runs(PdfPageReader.Page page) => RunPattern.Matches(page.Content).Cast<Match>().Select(m => new Run(
        m.Groups[1].Value,
        double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
        m.Groups[4].Success ? double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0,
        m.Groups[5].Success ? double.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : 0,
        double.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture),
        double.Parse(m.Groups[7].Value, CultureInfo.InvariantCulture),
        Unescape(m.Groups[8].Value))).ToList();

    internal static double Width(Run run)
    {
        var font = (UnlimitedPDF.Models.Internal.Fonts.StandardFont)(int.Parse(run.Font.Substring(2), CultureInfo.InvariantCulture) - 1);
        return UnlimitedPDF.Models.Internal.Fonts.StandardFontMetrics.Measure(font, run.Text, run.Size) + run.CharacterSpacing * run.Text.Length;
    }

    #endregion

    #region Private Methods

    private static string Unescape(string text)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                if (char.IsDigit(text[i + 1]) && i + 3 < text.Length)
                {
                    bytes.Add(Convert.ToByte(text.Substring(i + 1, 3), 8));
                    i += 3;
                    continue;
                }
                bytes.Add((byte)text[++i]);
                continue;
            }
            bytes.Add((byte)text[i]);
        }
        return UnlimitedPDF.Models.Internal.Fonts.WinAnsiEncoding.Decode(bytes.ToArray(), 0, bytes.Count);
    }

    #endregion

    #endregion

    #region Nested Types

    internal sealed record Run(string Font, double Size, double WordSpacing, double CharacterSpacing, double X, double Y, string Text);

    #endregion
}
