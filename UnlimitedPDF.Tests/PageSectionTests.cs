using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Header and footer sections (WR-030, WR-031), checked on the saved output: bands on every saved page, offsets from the
/// band's top-left corner, clipping, tables that never paginate, page numbers, the per-page callback, and escaping.
/// </summary>
public class PageSectionTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
    private const double A4Width = 595.28;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Header_and_footer_appear_on_every_page_with_page_numbers()
    {
        var document = Sectioned();
        for (int i = 0; i < 120; i++) document.Body.AddText("Line " + i);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(3, pages.Count);
        for (int n = 1; n <= 3; n++)
        {
            Assert.True(pages[n - 1].Shows("Report"));
            Assert.True(pages[n - 1].Shows($"Page {n} of 3"));
        }
    }

    [Fact]
    public void Bands_are_clipped_and_content_is_placed_from_the_band_top_left()
    {
        var document = Sectioned();
        document.Header.AddRectangle(5, 40, 100, 30, PdfColor.LightGray); // 20 pt past the band's bottom edge
        document.AddPage();

        var page = PdfPageReader.Save(document).Single();
        double headerTop = A4Height - 20;

        var clips = page.ClipRectangles();
        Assert.Equal(2, clips.Count);
        AssertRect((40, headerTop - 50, A4Width - 80, 50), clips[0]);
        AssertRect((40, 20, A4Width - 80, 30), clips[1]);

        var rectangle = page.LayoutRectangles().Single();
        AssertRect((45, headerTop - 70, 100, 30), rectangle);
        Assert.True(page.Content.IndexOf("re W n", StringComparison.Ordinal) < page.Content.IndexOf("re f Q", StringComparison.Ordinal));

        var title = page.LayoutTexts().First(t => t.Text == "Report");
        Assert.Equal(40, title.X, Precision);
        Assert.Equal(headerTop - 10 - 12 * 1.2 / 2 - 12 / 3.0, title.Baseline, Precision);
    }

    [Fact]
    public void Right_aligned_footer_text_ends_at_the_right_margin()
    {
        var document = Sectioned();
        document.AddPage();

        var text = PdfPageReader.Save(document).Single().LayoutTexts().Single(t => t.Text == "Page 1 of 1");

        Assert.Equal(A4Width - 40, text.X + Models.Internal.HelveticaMetrics.Width("Page 1 of 1", 10), Precision);
    }

    [Fact]
    public void A_table_in_a_band_is_drawn_whole_and_adds_no_page()
    {
        var document = Sectioned();
        var table = document.Header.AddTable(0, 0, 200);
        table.AddColumn();
        for (int r = 0; r < 10; r++) table.AddRow().GetCell(0).Text = "H" + r;
        document.AddPage();

        var pages = PdfPageReader.Save(document);

        Assert.Single(pages);
        Assert.True(pages[0].Shows("H9"));
        AssertRect((40, A4Height - 20 - 20, 200, 20), pages[0].FilledRectangles()[0]);
    }

    [Fact]
    public void Page_coordinate_tables_break_at_the_body_bottom_and_resume_at_the_body_top()
    {
        var document = Sectioned();
        document.Body.TopGap = 10;
        document.Body.BottomGap = 10;
        var table = document.AddPage().AddTable(50, 300, 300);
        table.AddColumn();
        for (int r = 0; r < 20; r++) table.AddRow().GetCell(0).Text = "R" + r;

        var pages = PdfPageReader.Save(document);

        // Body bottom = 20 + 30 + 10 = 60, so rows R0 to R11 fit above it; body top = 841.89 - 20 - 50 - 10.
        Assert.Equal(2, pages.Count);
        Assert.True(pages[0].Shows("R11"));
        Assert.False(pages[0].Shows("R12"));
        Assert.True(pages[1].Shows("R12"));
        AssertRect((50, A4Height - 80 - 20, 300, 20), pages[1].FilledRectangles()[0]);
        Assert.True(pages[1].Shows("Report"));
        Assert.True(pages[1].Shows("Page 2 of 2"));
    }

    [Fact]
    public void A_page_without_body_height_fails_before_output_naming_it()
    {
        var document = new PdfDocument();
        document.Header.Height = 500;
        document.Footer.Height = 400;
        document.AddPage();

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        Assert.Contains("Page 1 has no body height", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Invalid_band_heights_are_rejected(double value)
    {
        var document = new PdfDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Header.Height = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Body.TopGap = value);
        Assert.Throws<ArgumentNullException>(() => document.Header.AddText(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => document.Footer.Build(null!));
    }

    [Fact]
    public void A_hidden_band_draws_nothing_and_gives_its_space_to_the_body()
    {
        var document = Sectioned();
        document.Header.IsHidden = true;
        document.Body.AddText("first");

        var page = PdfPageReader.Save(document).Single();

        Assert.False(page.Shows("Report"));
        var first = page.LayoutTexts().Single(t => t.Text == "first");
        Assert.Equal(A4Height - 20 - 12 * 1.2 / 2 - 12 / 3.0, first.Baseline, Precision);
    }

    [Fact]
    public void Placeholders_fill_and_double_braces_write_literal_braces()
    {
        var document = new PdfDocument();
        document.Footer.Height = 20;
        document.Footer.AddText("{{page}} {page}/{pages} {other}", 0, 0);
        document.AddPage();
        document.AddPage();

        var pages = PdfPageReader.Save(document);

        Assert.True(pages[0].Shows("{page} 1/2 {other}"));
        Assert.True(pages[1].Shows("{page} 2/2 {other}"));
    }

    [Fact]
    public void The_callback_builds_content_for_each_page_with_its_number_and_position()
    {
        var document = new PdfDocument();
        document.Header.Height = 20;
        document.Header.Build((content, info) => content.AddText($"{info.PageNumber}/{info.PageCount} first={info.IsFirst} last={info.IsLast}", 0, 0));
        document.AddPage();
        document.AddPage();
        document.AddPage();

        var pages = PdfPageReader.Save(document);

        Assert.True(pages[0].Shows("1/3 first=True last=False"));
        Assert.True(pages[1].Shows("2/3 first=False last=False"));
        Assert.True(pages[2].Shows("3/3 first=False last=True"));
    }

    [Fact]
    public void A_callback_exception_propagates_and_leaves_an_existing_file_unchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        File.WriteAllText(path, "existing");
        try
        {
            var document = new PdfDocument();
            document.Footer.Height = 20;
            document.Footer.Build((_, _) => throw new FormatException("boom"));
            document.AddPage();

            Assert.Throws<FormatException>(() => document.Save(path));
            Assert.Equal("existing", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_failing_callback_or_band_table_adds_no_page_object()
    {
        // Review F-08: band content was built after continuation pages were added, so a corrected document kept their
        // unused objects. Each document needs continuation pages until rows are removed after the failed save.
        static (PdfDocument Document, PdfTable Table) Build(Func<bool> fail, bool badBandTable)
        {
            var document = new PdfDocument();
            document.Footer.Height = 20;
            document.Footer.Build((content, info) =>
            {
                if (fail()) throw new FormatException("callback failed");
                content.AddText("Page " + info.PageNumber, 0, 0);
            });
            if (badBandTable)
            {
                var band = document.Header.AddTable(0, 0, 4);
                document.Header.Height = 20;
                band.AddColumn();
                band.AddRow().GetCell(0).AddContent(PdfImage.FromBytes(new JpegBuilder { Width = 8, Height = 8 }.Build()));
            }
            var table = document.AddPage().AddTable(50, 700, 200);
            table.AddColumn();
            for (int r = 0; r < 60; r++) table.AddRow().GetCell(0).Text = "R" + r;
            return (document, table);
        }

        bool failing = true;
        var (withCallback, rows) = Build(() => failing, badBandTable: false);
        Assert.Throws<FormatException>(() => PdfPageReader.SaveBytes(withCallback));
        while (rows.RowCount > 5) rows.RemoveRow(rows.RowCount - 1);
        failing = false;
        var (fresh, freshRows) = Build(() => false, badBandTable: false);
        while (freshRows.RowCount > 5) freshRows.RemoveRow(freshRows.RowCount - 1);
        Assert.Equal(PdfPageReader.SaveBytes(fresh), PdfPageReader.SaveBytes(withCallback));

        var (withBandTable, bandRows) = Build(() => false, badBandTable: true);
        Assert.Contains("no width left", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(withBandTable)).Message);
        while (bandRows.RowCount > 5) bandRows.RemoveRow(bandRows.RowCount - 1);
        withBandTable.Header.Height = 0;
        var (freshBand, freshBandRows) = Build(() => false, badBandTable: true);
        while (freshBandRows.RowCount > 5) freshBandRows.RemoveRow(freshBandRows.RowCount - 1);
        freshBand.Header.Height = 0;
        Assert.Equal(PdfPageReader.SaveBytes(freshBand), PdfPageReader.SaveBytes(withBandTable));
    }

    [Fact]
    public void Repeated_saves_of_a_sectioned_document_are_identical()
    {
        var document = Sectioned();
        for (int i = 0; i < 80; i++) document.Body.AddText("Line " + i);

        Assert.Equal(PdfPageReader.SaveBytes(document), PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void Security_section_and_placeholder_text_is_escaped()
    {
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Header.AddText(") Tj ET BT (x", 0, 0);
        document.Header.Build((content, info) => content.AddText("\\{page})", 0, 20));
        document.AddPage();

        var page = PdfPageReader.Save(document).Single();

        Assert.Contains("(\\) Tj ET BT \\(x) Tj", page.Content);
        Assert.Contains("(\\\\1\\)) Tj", page.Content);
    }

    #endregion

    #region Private Methods

    private static void AssertRect((double X, double Y, double Width, double Height) expected, (double X, double Y, double Width, double Height) actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
        Assert.Equal(expected.Width, actual.Width, Precision);
        Assert.Equal(expected.Height, actual.Height, Precision);
    }

    // Margins 20 top and bottom, 40 left and right; a 50 pt header with a title and a 30 pt footer with page numbers.
    private static PdfDocument Sectioned()
    {
        var document = new PdfDocument { TopMargin = 20, BottomMargin = 20, LeftMargin = 40, RightMargin = 40 };
        document.Header.Height = 50;
        document.Header.AddText("Report", 0, 10);
        document.Footer.Height = 30;
        document.Footer.AddText("Page {page} of {pages}", 0, 10, fontSize: 10, alignment: PdfTextAlignment.Right);
        return document;
    }

    #endregion

    #endregion
}
