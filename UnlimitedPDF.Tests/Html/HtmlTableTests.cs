using System.Text.RegularExpressions;
using static UnlimitedPDF.Tests.Html.HtmlLayoutTests;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// HTML tables: column widths, spans, nesting, header rows repeated on each page, rows moved whole, rows taller than a
/// page split cell by cell, fit to width, and clipping with a warning (WR-038, AC-11, AC-16, AC-17, AC-19, D-005).
/// </summary>
public class HtmlTableTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Columns_take_the_width_their_content_needs_in_order()
    {
        var runs = Runs(Assert.Single(Render("<table border=1><tr><td>a<td>much longer text here<td>c</table>")));
        var a = runs.Single(r => r.Text == "a");
        var b = runs.Single(r => r.Text == "much longer text here");
        var c = runs.Single(r => r.Text == "c");
        Assert.True(a.X < b.X && b.X < c.X);
        Assert.Equal(a.Y, b.Y, 3);
        Assert.True(c.X - b.X > Width(b));
    }

    [Fact]
    public void Column_and_row_spans_place_cells_on_the_grid()
    {
        var runs = Runs(Assert.Single(Render("<table><tr><td colspan=2>wide<td rowspan=2>tall<tr><td>x<td>y<tr><td>p<td>q<td>r</table>")));
        var wide = runs.Single(r => r.Text == "wide");
        var tall = runs.Single(r => r.Text == "tall");
        var x = runs.Single(r => r.Text == "x");
        var y = runs.Single(r => r.Text == "y");
        var r3 = runs.Single(r => r.Text == "r");
        Assert.Equal(x.X, wide.X, 3);
        Assert.True(y.X > x.X);
        Assert.True(tall.X > y.X);
        Assert.Equal(tall.X, r3.X, 3);
        Assert.True(x.Y < wide.Y);
    }

    [Fact]
    public void Tables_nest_three_levels_deep_with_spans_inside()
    {
        string html = "<table border=1><tr><td>outer<td><table border=1><tr><td colspan=2>mid<tr><td>m1<td><table border=1><tr><td rowspan=2>deep<td>d1<tr><td>d2</table></table></table>";
        var runs = Runs(Assert.Single(Render(html)));
        foreach (var text in new[] { "outer", "mid", "m1", "deep", "d1", "d2" }) Assert.Contains(runs, r => r.Text == text);
        Assert.True(runs.Single(r => r.Text == "deep").X > runs.Single(r => r.Text == "m1").X);
        Assert.True(runs.Single(r => r.Text == "d2").Y < runs.Single(r => r.Text == "d1").Y);
    }

    [Fact]
    public void Header_rows_repeat_on_every_page_and_rows_move_whole()
    {
        string rows = string.Concat(Enumerable.Range(1, 120).Select(i => $"<tr><td>row {i}<td>{i * 10}</tr>"));
        var pages = Render($"<table border=1><thead><tr><th>Item<th>Qty</thead><tbody>{rows}</table>");
        Assert.True(pages.Count >= 3);
        foreach (var page in pages)
        {
            var runs = Runs(page);
            Assert.Equal("Item", runs[0].Text);
            Assert.All(runs, r => Assert.True(r.Y - r.Size * 0.2 >= Bottom - 0.01));
        }
        var all = pages.SelectMany(Runs).Where(r => r.Text.StartsWith("row ", StringComparison.Ordinal)).Select(r => r.Text).ToList();
        Assert.Equal(Enumerable.Range(1, 120).Select(i => $"row {i}"), all);
    }

    [Fact]
    public void A_row_taller_than_the_page_splits_between_lines_cell_by_cell()
    {
        string list = "<ul>" + string.Concat(Enumerable.Range(1, 90).Select(i => $"<li>line {i}</li>")) + "</ul>";
        string html = $"<table border=1><thead><tr><th>Item<th>Notes</thead><tr><td>first<td>short</tr><tr><td>tall row<td>{list}</tr><tr><td>after<td>end</tr></table>";
        var pages = Render(html);
        Assert.True(pages.Count >= 2);
        var lines = pages.SelectMany(Runs).Where(r => r.Text.StartsWith("line ", StringComparison.Ordinal)).Select(r => r.Text).ToList();
        Assert.Equal(Enumerable.Range(1, 90).Select(i => $"line {i}"), lines);
        Assert.Contains(Runs(pages[0]), r => r.Text == "tall row");
        Assert.All(pages, p => Assert.Equal("Item", Runs(p)[0].Text));
        Assert.All(pages.SelectMany(Runs), r => Assert.True(r.Y - r.Size * 0.2 >= Bottom - 0.01));
        Assert.Contains(Runs(pages[pages.Count - 1]), r => r.Text == "after");
    }

    [Fact]
    public void An_800_pixel_table_narrows_to_the_body_without_clipping()
    {
        var html = PdfHtml.FromString("<table width=800 border=1><tr><td width=500>Description of the item sold<td width=300>Amount</table>");
        var doc = Document();
        doc.Body.AddHtml(html);
        var runs = Runs(Assert.Single(PdfPageReader.Save(doc)));
        Assert.All(runs, r => Assert.True(r.X + Width(r) <= Right + 0.01));
        Assert.Empty(html.Warnings);
        var description = runs.Single(r => r.Text.StartsWith("Description", StringComparison.Ordinal));
        var amount = runs.Single(r => r.Text == "Amount");
        Assert.True(amount.X > description.X + Width(description));
    }

    [Fact]
    public void Content_that_cannot_narrow_is_clipped_with_a_warning()
    {
        string cells = string.Concat(Enumerable.Range(1, 12).Select(i => $"<td style='white-space:nowrap'>column-number-{i}"));
        var html = PdfHtml.FromString($"<table><tr>{cells}</table><pre>{new string('x', 200)}</pre>");
        var doc = Document();
        doc.Body.AddHtml(html);
        var page = Assert.Single(PdfPageReader.Save(doc));
        Assert.Contains(" re W n", page.Content);
        Assert.Equal(new[] { PdfHtmlWarningKind.ContentClipped, PdfHtmlWarningKind.ContentClipped }, html.Warnings.Select(w => w.Kind));
        Assert.All(html.Warnings, w => Assert.Equal(1, w.PageNumber));
    }

    [Fact]
    public void Strict_mode_fails_save_on_a_layout_warning_before_writing()
    {
        string cells = string.Concat(Enumerable.Range(1, 12).Select(i => $"<td style='white-space:nowrap'>column-number-{i}"));
        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString($"<table><tr>{cells}</table>", new PdfHtmlLoadOptions { FailOnWarning = true }));
        using var stream = new MemoryStream();
        var ex = Assert.Throws<PdfHtmlException>(() => doc.Save(stream));
        Assert.Equal(PdfHtmlWarningKind.ContentClipped, ex.Warning!.Kind);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Collapsed_borders_are_drawn_once_on_the_grid_lines()
    {
        var page = Assert.Single(Render("<table style='border-collapse: collapse'><tr><td style='border:1px solid black'>a<td style='border:1px solid black'>b</table>"));
        var runs = Runs(page);
        Assert.Equal(2, runs.Count);
        Assert.Equal(8, Regex.Matches(page.Content, @"0 0 0 RG 0.75 w ").Count);
    }

    [Fact]
    public void Centered_tables_and_cell_alignment_follow_attributes_and_styles()
    {
        var runs = Runs(Assert.Single(Render("<table align=center width=200 border=0><tr><td align=right>r<td valign=bottom style='height:40px'>b</table>", bodyMargin: false)));
        var r = runs.Single(x => x.Text == "r");
        Assert.True(r.X > Left + (Right - Left - 150) / 2);
        Assert.True(runs.Single(x => x.Text == "b").Y < r.Y);
    }

    [Fact]
    public void Lists_in_cells_and_tables_in_list_items_lay_out()
    {
        var runs = Runs(Assert.Single(Render("<table border=1><tr><td><ol><li>one<li>two</ol><td>x</table><ul><li>item<table border=1><tr><td>in list</table></ul>")));
        foreach (var text in new[] { "1.", "one", "2.", "two", "x", "item", "in list" }) Assert.Contains(runs, r => r.Text == text);
        Assert.True(runs.Single(r => r.Text == "in list").X > Left + 20);
    }

    [Fact]
    public void Row_backgrounds_fill_cells_and_captions_sit_above()
    {
        var page = Assert.Single(Render("<table><caption>Totals</caption><tr style='background:#ff0000'><td>a<td style='background:#00ff00'>b</table>"));
        Assert.Contains("1 0 0 rg", page.Content);
        Assert.Contains("0 1 0 rg", page.Content);
        var runs = Runs(page);
        Assert.True(runs.Single(r => r.Text == "Totals").Y > runs.Single(r => r.Text == "a").Y);
    }

    #endregion

    #endregion
}
