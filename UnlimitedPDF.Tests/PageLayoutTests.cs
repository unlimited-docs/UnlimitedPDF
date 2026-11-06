using UnlimitedPDF;
using UnlimitedPDF.Models.Internal;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Page layout (WR-030) and its building blocks: per-page body edges in the table paginator, side margins, planning
/// pages before any page object exists, filled rectangles, Helvetica widths, and escaped, culture-independent text.
/// </summary>
public class PageLayoutTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
    private const double A4Width = 595.28;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Paginator_breaks_and_resumes_at_the_body_edges_of_each_page()
    {
        var table = new PdfTable { Width = 100 };
        table.AddColumn();
        for (int r = 0; r < 6; r++) table.AddRow().GetCell(0).Text = "R" + r;
        var layout = PdfTableRenderer.Layout(table, 1, 1);

        // Page 0 has room for two rows above y = 100, page 1 for one row, page 2 for the rest.
        (double, double, bool) Edges(int offset) => offset switch
        {
            0 => (140, 100, true),
            1 => (500, 470, true),
            _ => (800, 0, true)
        };

        var (parts, _) = TablePaginator.Paginate(table, layout, 140, Edges, () => "table 1");

        Assert.Equal(3, parts.Count);
        Assert.Equal((0, 2), (parts[0].FirstRow, parts[0].EndRow));
        Assert.Equal((2, 3), (parts[1].FirstRow, parts[1].EndRow));
        Assert.Equal(500, parts[1].Top, Precision);
        Assert.Equal(480, parts[1].Bottom, Precision);
        Assert.Equal((3, 6), (parts[2].FirstRow, parts[2].EndRow));
        Assert.Equal(800, parts[2].Top, Precision);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_side_margins_are_rejected(double value)
    {
        var document = new PdfDocument();

        Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => document.MarginLeft = value).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => document.MarginRight = value).ParamName);
    }

    [Fact]
    public void Side_margins_do_not_move_page_coordinate_content()
    {
        var plain = new PdfDocument();
        plain.AddPage().AddText("Hello", 50, 700);
        var withMargins = new PdfDocument { MarginLeft = 40, MarginRight = 40 };
        withMargins.AddPage().AddText("Hello", 50, 700);

        Assert.Equal(PdfPageReader.Save(plain)[0].Content, PdfPageReader.Save(withMargins)[0].Content);
    }

    [Fact]
    public void Side_margins_that_leave_no_width_fail_before_output_naming_the_page()
    {
        var document = new PdfDocument { MarginLeft = 300, MarginRight = 300 };
        document.Body.AddText("x");

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        Assert.Contains("Page 1 has no body width", error.Message);
    }

    [Fact]
    public void A_failed_save_adds_no_page_and_a_corrected_save_equals_a_fresh_document()
    {
        // 26110101 review F-02: page 1's table needs continuation pages, page 2's table cannot fit any page.
        static PdfDocument Build(out PdfTable bad)
        {
            var document = new PdfDocument { MarginBottom = 50 };
            NumberedTable(document.AddPage(), 300, 40);
            bad = NumberedTable(document.AddPage(), 700, 2);
            return document;
        }

        var document = Build(out var bad);
        bad.Rows[1].Height = 900;
        Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));
        bad.Rows[1].Height = null;

        var fresh = Build(out _);
        Assert.Equal(PdfPageReader.SaveBytes(fresh), PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void A_page_rectangle_is_filled_where_placed_and_never_adds_a_page()
    {
        var document = new PdfDocument { MarginBottom = 100 };
        document.AddPage().AddRectangle(50, 10, 495, 3, new PdfColor(1, 0.5, 0));

        var pages = PdfPageReader.Save(document);

        Assert.Single(pages);
        Assert.Contains("q 1 0.5 0 rg 50 10 495 3 re f Q", pages[0].Content);
    }

    [Fact]
    public void Invalid_page_rectangles_are_rejected()
    {
        var page = new PdfDocument().AddPage();

        Assert.Throws<ArgumentNullException>(() => page.AddRectangle(0, 0, 1, 1, null!));
        Assert.Equal("width", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddRectangle(0, 0, 0, 1, PdfColor.Black)).ParamName);
        Assert.Equal("y", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddRectangle(0, double.NaN, 1, 1, PdfColor.Black)).ParamName);
    }

    [Theory]
    [InlineData("Hello", 12, 27.336)]
    [InlineData("Page 1 of 3", 10, 51.15)]
    [InlineData("WWW", 10, 28.32)]
    [InlineData("é", 10, 5.56)]
    public void Helvetica_widths_follow_the_font_metrics(string text, double size, double expected)
    {
        Assert.Equal(expected, HelveticaMetrics.Width(text, size), Precision);
    }

    [Fact]
    public void Layout_text_is_escaped_and_written_with_invariant_numbers()
    {
        using var culture = new CultureScope("de-DE");
        var document = new PdfDocument { MarginLeft = 40.5 };
        document.Body.AddText("a) b \\ (c", fontSize: 10.5);

        var page = PdfPageReader.Save(document).Single();

        Assert.Contains("(a\\) b \\\\ \\(c) Tj", page.Content);
        Assert.Contains("/F1 10.5 Tf", page.Content);
        Assert.Contains("1 0 0 1 40.5 ", page.Content);
    }

    [Fact]
    public void Security_page_text_is_escaped_and_valid_text_keeps_its_bytes()
    {
        // WR-005 (AC-29): page text goes through the same escaping as band, flow, and cell text.
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText(") Tj ET BT (injected", 50, 700);
        page.AddText("C:\\temp", 50, 680);
        page.AddText("Rate (INR)", 50, 660);

        var content = PdfPageReader.Save(document).Single().Content;

        Assert.Contains("(\\) Tj ET BT \\(injected) Tj", content);
        Assert.Contains("(C:\\\\temp) Tj", content);
        Assert.Contains("BT /F1 12 Tf 50 660 Td (Rate (INR)) Tj ET", content);
    }

    #endregion

    #region Private Methods

    private static PdfTable NumberedTable(PdfPage page, double y, int rows)
    {
        var table = page.AddTable(50, y, 300);
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = "R" + r;
        return table;
    }

    #endregion

    #endregion
}
