using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Internal;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The body flow (WR-033), checked on the saved output: items stack with their spacing, move whole to library-added
/// pages, tables split by rows, blocks and keep-with-next keep content together, oversize content fails before output,
/// and flow continues below table rows carried onto a shared continuation page.
/// </summary>
public class BodyFlowTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
    private const double A4Width = 595.28;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Items_stack_below_each_other_with_their_spacing()
    {
        var document = new PdfDocument();
        document.Body.AddText("a");
        document.Body.AddText("b", spacing: 6);
        document.Body.AddRectangle(100, 10, PdfColor.Black, spacing: 4, x: 20);

        var page = PdfPageReader.Save(document).Single();
        var texts = page.LayoutTexts();

        Assert.Equal(A4Height - LineOffset(12), texts[0].Baseline, Precision);
        Assert.Equal(A4Height - 14.4 - 6 - LineOffset(12), texts[1].Baseline, Precision);
        AssertRect((20, A4Height - 14.4 - 6 - 14.4 - 4 - 10, 100, 10), page.LayoutRectangles().Single());
    }

    [Fact]
    public void An_item_that_does_not_fit_moves_to_a_new_page_without_its_spacing()
    {
        var document = new PdfDocument();
        document.Body.AddRectangle(100, 400, PdfColor.Black);
        document.Body.AddRectangle(100, 400, PdfColor.Black);
        document.Body.AddRectangle(100, 100, PdfColor.LightGray, spacing: 50);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.Equal(2, pages[0].LayoutRectangles().Count);
        AssertRect((0, A4Height - 100, 100, 100), pages[1].LayoutRectangles().Single());
    }

    [Fact]
    public void Flow_pages_get_the_header_and_footer_and_flow_tables_repeat_their_header_rows()
    {
        var document = new PdfDocument { LeftMargin = 40 };
        document.Header.Height = 30;
        document.Header.AddText("Directory", 0, 0);
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 0);
        document.Body.AddText("Employees", keepWithNext: true);
        var table = document.Body.AddTable(300, spacing: 8);
        table.AddColumn();
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        table.AddRow().GetCell(0).Text = "Name";
        for (int r = 0; r < 60; r++) table.AddRow().GetCell(0).Text = "E" + r;

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[1].Shows("Directory"));
        Assert.True(pages[1].Shows("Page 2 of 2"));
        Assert.True(pages[1].Shows("Name"));
        Assert.True(pages[1].Shows("E59"));
        // The repeated header row starts at the body top of page 2, at the left margin.
        AssertRect((40, A4Height - 30 - 20, 300, 20), pages[1].FilledRectangles()[0]);
    }

    [Fact]
    public void AddPage_and_PageBreak_start_new_pages_and_pages_are_created_only_for_content()
    {
        var document = new PdfDocument();
        document.Body.AddText("a");
        var second = document.AddPage();
        second.AddText("caller", 10, 10);
        document.Body.AddText("b");
        document.Body.PageBreak();
        document.Body.AddText("c");

        var pages = PdfPageReader.Save(document);

        Assert.Equal(3, pages.Count);
        Assert.True(pages[0].Shows("a"));
        Assert.True(pages[1].Shows("caller"));
        Assert.True(pages[1].Shows("b"));
        Assert.True(pages[2].Shows("c"));

        var noFlow = new PdfDocument();
        noFlow.AddPage();
        Assert.Single(PdfPageReader.Save(noFlow));
    }

    [Fact]
    public void Flow_text_is_aligned_within_the_body_width()
    {
        var document = new PdfDocument { LeftMargin = 50, RightMargin = 50 };
        document.Body.AddText("Right", alignment: PdfTextAlignment.Right);
        document.Body.AddText("Center", alignment: PdfTextAlignment.Center);

        var texts = PdfPageReader.Save(document).Single().LayoutTexts();

        Assert.Equal(A4Width - 50, texts[0].X + HelveticaMetrics.Width("Right", 12), Precision);
        Assert.Equal(A4Width / 2, texts[1].X + HelveticaMetrics.Width("Center", 12) / 2, Precision);
    }

    [Fact]
    public void Flow_images_are_drawn_below_the_previous_item()
    {
        var document = new PdfDocument();
        document.Body.AddText("caption");
        document.Body.AddImage(PdfImage.FromBytes(new JpegBuilder { Width = 40, Height = 20 }.Build()), width: 100, spacing: 5, x: 10);

        var page = PdfPageReader.Save(document).Single();

        Assert.Contains(FormattableString.Invariant($"q 100 0 0 50 10 {A4Height - 14.4 - 5 - 50:0.######} cm /Im1 Do Q"), page.Content);
    }

    [Fact]
    public void A_block_moves_to_the_next_page_as_one_piece()
    {
        var document = new PdfDocument();
        document.Body.AddRectangle(100, 820, PdfColor.Black);
        var block = document.Body.AddBlock(spacing: 4);
        block.AddText("Total");
        block.AddText("Signed", spacing: 2);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[1].Shows("Total"));
        Assert.True(pages[1].Shows("Signed"));
        Assert.Equal(A4Height - 14.4 - 2 - LineOffset(12), pages[1].LayoutTexts()[1].Baseline, Precision);
    }

    [Fact]
    public void A_heading_kept_with_a_table_moves_with_its_first_row()
    {
        var document = new PdfDocument();
        document.Body.AddRectangle(100, 810, PdfColor.Black);
        document.Body.AddText("Heading", keepWithNext: true);
        var table = document.Body.AddTable(200);
        table.AddColumn();
        for (int r = 0; r < 3; r++) table.AddRow().GetCell(0).Text = "R" + r;

        var pages = PdfPageReader.Save(document);

        // 31.89 pt are left: the heading (14.4) fits alone, but not with the first row (20).
        Assert.Equal(2, pages.Count);
        Assert.False(pages[0].Shows("Heading"));
        Assert.True(pages[1].Shows("Heading"));
        Assert.True(pages[1].Shows("R0"));
    }

    [Fact]
    public void A_kept_table_moves_its_last_row_to_the_page_of_the_next_item()
    {
        var document = new PdfDocument();
        document.Body.AddRectangle(100, 760, PdfColor.Black);
        var table = document.Body.AddTable(200, keepWithNext: true);
        table.AddColumn();
        for (int r = 0; r < 3; r++) table.AddRow().GetCell(0).Text = "R" + r;
        document.Body.AddText("Total", spacing: 10);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[0].Shows("R1"));
        Assert.False(pages[0].Shows("R2"));
        Assert.True(pages[1].Shows("R2"));
        Assert.True(pages[1].Shows("Total"));
    }

    [Fact]
    public void Content_taller_than_an_empty_body_fails_before_output_naming_it()
    {
        var item = new PdfDocument();
        item.Body.AddRectangle(100, 900, PdfColor.Black);
        Assert.Contains("Flow item 1 (rectangle) is 900 pt tall", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(item)).Message);

        var block = new PdfDocument();
        var b = block.Body.AddBlock();
        b.AddRectangle(100, 500, PdfColor.Black);
        b.AddRectangle(100, 500, PdfColor.Black);
        Assert.Contains("Flow item 1 (block)", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(block)).Message);

        var run = new PdfDocument();
        run.Body.AddText("Heading", keepWithNext: true);
        run.Body.AddRectangle(100, 830, PdfColor.Black);
        Assert.Contains("kept-together run of flow items 1 to 2", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(run)).Message);
    }

    [Fact]
    public void Flow_continues_below_rows_carried_onto_a_shared_continuation_page()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        NumberedTable(page, 50, 200, 100, 20, "A");  // carries 10 rows: ends at 841.89 - 200
        NumberedTable(page, 400, 200, 100, 25, "B"); // carries 15 rows: ends at 841.89 - 300
        document.Body.AddRectangle(30, 400, PdfColor.Black);
        document.Body.AddRectangle(30, 400, PdfColor.Black);
        document.Body.AddRectangle(30, 100, PdfColor.LightGray, spacing: 5);
        document.AddPage().AddText("next caller page", 10, 10);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(3, pages.Count);
        Assert.True(pages[1].Shows("A19"));
        Assert.True(pages[1].Shows("B24"));
        AssertRect((0, A4Height - 300 - 5 - 100, 30, 100), pages[1].LayoutRectangles().Single());
        Assert.True(pages[2].Shows("next caller page"));
    }

    [Fact]
    public void An_empty_kept_table_saves_and_takes_no_room()
    {
        // Review F-03: a kept table without rows made Save throw KeyNotFoundException.
        var document = new PdfDocument();
        document.Body.AddTable(300, keepWithNext: true);
        document.Body.AddText("after");

        var page = PdfPageReader.Save(document).Single();

        Assert.Equal(A4Height - LineOffset(12), page.LayoutTexts().Single().Baseline, Precision);
    }

    [Fact]
    public void A_kept_table_keeps_its_last_row_with_a_kept_run_after_it()
    {
        // Review F-04: 39 rows end 61.89 pt above the bottom; the kept heading fits there, but not with the rectangle.
        var document = new PdfDocument();
        Rows(document.Body.AddTable(200, keepWithNext: true), 39, "K");
        document.Body.AddText("Heading", keepWithNext: true);
        document.Body.AddRectangle(100, 300, PdfColor.Black);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[0].Shows("K37"));
        Assert.False(pages[0].Shows("K38"));
        Assert.True(pages[1].Shows("K38"));
        Assert.True(pages[1].Shows("Heading"));
    }

    [Fact]
    public void A_moved_last_row_goes_to_the_first_page_with_room()
    {
        // Review F-05: page 2 is full of carried rows down to 21.89; the moved row was drawn into the footer band.
        var document = new PdfDocument();
        document.Footer.Height = 20;
        var page = document.AddPage();
        var placed = page.AddTable(300, A4Height, 200);
        placed.AddColumn();
        for (int r = 0; r < 82; r++) placed.AddRow().GetCell(0).Text = "P" + r;
        Rows(document.Body.AddTable(200, keepWithNext: true), 39, "K");
        document.Body.AddRectangle(100, 100, PdfColor.Black);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(3, pages.Count);
        Assert.True(pages[1].Shows("P81"));
        Assert.False(pages[1].Shows("K38"));
        Assert.True(pages[2].Shows("K38"));
        Assert.All(pages[1].FilledRectangles(), r => Assert.True(r.Y >= 20 - 1e-6, $"Rectangle {r} is in the footer band."));
    }

    [Theory]
    [InlineData("end")]
    [InlineData("break")]
    [InlineData("page")]
    public void A_kept_table_keeps_its_last_row_with_a_kept_run_that_ends_the_flow(string ending)
    {
        // Review F-13: 41 rows end 21.89 pt above the bottom, and the kept 24 pt heading after them is the last item
        // before the document's end, a page break, or AddPage; K40 stayed behind on page 1.
        var document = new PdfDocument();
        Rows(document.Body.AddTable(200, keepWithNext: true), 41, "K");
        document.Body.AddText("Heading", fontSize: 20, keepWithNext: true);
        if (ending == "break")
        {
            document.Body.PageBreak();
            document.Body.AddText("after");
        }
        else if (ending == "page")
        {
            document.AddPage().AddText("next caller page", 10, 10);
        }

        var pages = PdfPageReader.Save(document);

        Assert.Equal(ending == "end" ? 2 : 3, pages.Count);
        Assert.True(pages[0].Shows("K39"));
        Assert.False(pages[0].Shows("K40"));
        Assert.True(pages[1].Shows("K40"));
        Assert.True(pages[1].Shows("Heading"));
    }

    [Fact]
    public void A_moved_last_row_lands_only_where_the_kept_run_fits_with_it()
    {
        // Review F-14: page 2 is carried down to 61.89 pt; the moved row fits there, but not with the 60 pt rectangle
        // that follows it, so the row and the rectangle go to page 3 together.
        var document = new PdfDocument();
        var page = document.AddPage();
        var placed = page.AddTable(300, A4Height, 200);
        placed.AddColumn();
        for (int r = 0; r < 81; r++) placed.AddRow().GetCell(0).Text = "P" + r;
        Rows(document.Body.AddTable(200, keepWithNext: true), 40, "K");
        document.Body.AddRectangle(100, 60, PdfColor.Black);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(3, pages.Count);
        Assert.True(pages[0].Shows("K38"));
        Assert.True(pages[1].Shows("P80"));
        Assert.False(pages[1].Shows("K39"));
        Assert.True(pages[2].Shows("K39"));
        AssertRect((0, A4Height - 20 - 60, 100, 60), pages[2].LayoutRectangles().Single());
    }

    [Fact]
    public void A_kept_table_row_and_run_taller_than_an_empty_body_fail_naming_the_run()
    {
        var document = new PdfDocument();
        Rows(document.Body.AddTable(200, keepWithNext: true), 39, "K");
        document.Body.AddRectangle(100, 830, PdfColor.Black);

        var message = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document)).Message;

        Assert.Contains("kept-together run of flow items 1 to 2", message);
    }

    [Theory]
    [InlineData("title and one row")]
    [InlineData("title and header row")]
    [InlineData("kept table before")]
    public void What_a_kept_table_keeps_with_moves_when_the_whole_table_moves(string lead)
    {
        // Review F-23: 70 pt are left on page 1. The lead and the small kept table fit there, but the table does not fit
        // with the 100 pt rectangle after it, so the whole table moves, and what it keeps with must move with it.
        var document = new PdfDocument();
        if (lead == "kept table before")
        {
            // 38 rows leave 81.89 pt; table B's only row fits there, but not with the rectangle.
            Rows(document.Body.AddTable(200, keepWithNext: true), 38, "A");
        }
        else
        {
            document.Body.AddRectangle(100, A4Height - 70, PdfColor.Black);
            document.Body.AddText("Title", keepWithNext: true);
        }

        var table = document.Body.AddTable(200, keepWithNext: true);
        table.AddColumn();
        if (lead == "title and header row")
        {
            table.HeaderRowCount = 1;
            table.RepeatHeaderRows = true;
            table.AddRow().GetCell(0).Text = "H";
        }
        table.AddRow().GetCell(0).Text = "B0";
        document.Body.AddRectangle(100, 100, PdfColor.Black);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[1].Shows("B0"));
        Assert.False(pages[0].Shows("B0"));
        if (lead == "kept table before")
        {
            Assert.True(pages[0].Shows("A36"));
            Assert.False(pages[0].Shows("A37"));
            Assert.True(pages[1].Shows("A37"));
        }
        else
        {
            Assert.False(pages[0].Shows("Title"));
            Assert.True(pages[1].Shows("Title"));
        }
        Assert.All(pages, page => Assert.False(page.Shows("H") && !page.Shows("B0")));
    }

    [Theory]
    [InlineData(true, "kept-together run of flow items 2 to 4 is 344.4 pt tall")]
    [InlineData(false, "kept-together run of flow items 2 to 3 is 350 pt tall")]
    public void A_kept_table_and_its_run_starting_at_the_top_of_an_empty_page_that_cannot_hold_them_fail(bool withTitle, string expected)
    {
        // Review F-28: page 2's body is 341.89 pt. The run starts at its top and does not fit; it moved on to page 3 and
        // left page 2 blank, where a run of the same height without a table fails.
        var document = new PdfDocument();
        document.AddPageRule(2).BodyTopGap = 500;
        document.Body.AddRectangle(150, A4Height, PdfColor.Black);
        if (withTitle) document.Body.AddText("T", keepWithNext: true);
        Rows(document.Body.AddTable(200, keepWithNext: true), 1, "A");
        document.Body.AddRectangle(160, withTitle ? 310 : 330, PdfColor.Black);

        var message = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document)).Message;

        Assert.Contains(expected, message);
        Assert.Contains("341.89 pt body height", message);
    }

    [Fact]
    public void A_kept_table_without_rows_leaves_its_kept_run_together()
    {
        // Review F-29: the empty kept table ended the run, so the rectangle went alone to page 2.
        var document = new PdfDocument();
        document.Body.AddRectangle(100, A4Height - 70, PdfColor.Black);
        document.Body.AddText("T", keepWithNext: true);
        Rows(document.Body.AddTable(200, keepWithNext: true), 1, "A");
        document.Body.AddTable(200, keepWithNext: true);
        document.Body.AddRectangle(100, 100, PdfColor.Black);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.False(pages[0].Shows("T"));
        Assert.False(pages[0].Shows("A0"));
        Assert.True(pages[1].Shows("T"));
        Assert.True(pages[1].Shows("A0"));
        Assert.Single(pages[0].LayoutRectangles());
        AssertRect((0, A4Height - 14.4 - 20 - 100, 100, 100), pages[1].LayoutRectangles().Single());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_kept_tables_last_row_alone_at_the_top_of_an_empty_page_that_cannot_hold_its_run_fails(bool repeatedHeader)
    {
        // Review F-30: page 2's body is 341.89 pt. The table's last row starts alone at its top (below the repeated
        // header row when there is one) and does not fit with the rectangle; it moved to page 3 and left page 2 blank.
        var document = new PdfDocument();
        document.AddPageRule(2).BodyTopGap = 500;
        document.Body.AddRectangle(150, repeatedHeader ? 790 : 810, PdfColor.Black);
        var table = document.Body.AddTable(200, keepWithNext: true);
        table.AddColumn();
        if (repeatedHeader)
        {
            table.HeaderRowCount = 1;
            table.RepeatHeaderRows = true;
            table.AddRow().GetCell(0).Text = "H";
        }
        table.AddRow().GetCell(0).Text = "A0";
        table.AddRow().GetCell(0).Text = "A1";
        document.Body.AddRectangle(160, repeatedHeader ? 310 : 330, PdfColor.Black);

        var message = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document)).Message;

        Assert.Contains("kept-together run of flow items 2 to 3 is 350 pt tall", message);
        Assert.Contains("341.89 pt body height", message);
    }

    [Fact]
    public void A_table_without_rows_takes_no_room_when_it_ends_a_kept_run()
    {
        // Review F-31: the empty table's 20 pt spacing was reserved after the kept line, so the line moved to page 2.
        var document = new PdfDocument();
        document.Body.AddRectangle(150, 815, PdfColor.Black);
        document.Body.AddText("K", keepWithNext: true);
        document.Body.AddTable(200, spacing: 20);
        document.Body.AddText("after");

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[0].Shows("K"));
        Assert.True(pages[1].Shows("after"));
    }

    [Fact]
    public void A_row_only_has_to_fit_the_page_it_lands_on()
    {
        // Review F-06: a 500 pt row fits page 1 (its header is hidden by a First rule) but not later pages.
        static PdfDocument Build()
        {
            var document = new PdfDocument();
            document.Header.Height = 400;
            document.AddPageRule(PdfPageRuleTarget.First).Header.IsHidden = true;
            return document;
        }

        var flow = Build();
        var flowTable = flow.Body.AddTable(200);
        flowTable.AddColumn();
        var flowRow = flowTable.AddRow();
        flowRow.Height = 500;
        flowRow.GetCell(0).Text = "tall";
        Assert.Single(PdfPageReader.Save(flow));

        var placed = Build();
        var table = placed.AddPage().AddTable(50, 800, 200);
        table.AddColumn();
        var row = table.AddRow();
        row.Height = 500;
        row.GetCell(0).Text = "tall";
        Assert.Single(PdfPageReader.Save(placed));
    }

    [Fact]
    public void Readme_headers_footers_and_flow_sample_runs_as_written()
    {
        // The code of README.md and nuget/README.md "Headers, footers & flow", saved to memory instead of a file (PR-012).
        var pdfDocument = new PdfDocument { TopMargin = 30, BottomMargin = 30, LeftMargin = 40, RightMargin = 40 };

        pdfDocument.Header.Height = 40;
        pdfDocument.Header.AddText("Quarterly report", 0, 8, 14);
        pdfDocument.Header.AddRectangle(0, 36, 515, 1, PdfColor.Black);
        pdfDocument.Footer.Height = 20;
        pdfDocument.Footer.AddText("Page {page} of {pages}", 0, 4, 9, PdfTextAlignment.Right);

        var first = pdfDocument.AddPageRule(PdfPageRuleTarget.First);
        first.Header.Height = 70;
        first.Header.AddText("Quarterly report", 0, 10, 24, PdfTextAlignment.Center);

        var body = pdfDocument.Body;
        body.TopGap = 12;
        body.AddText("Employees", fontSize: 14, keepWithNext: true);

        var table = body.AddTable(515, spacing: 6);
        table.SetColumnWidths(60, 255, 200);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        table.GetOrCreateCell(0, 0).Text = "No.";
        table.GetOrCreateCell(0, 1).Text = "Name";
        table.GetOrCreateCell(0, 2).Text = "Department";

        for (int i = 1; i <= 120; i++)
        {
            table.GetOrCreateCell(i, 0).Text = i.ToString();
            table.GetOrCreateCell(i, 1).Text = "Employee " + i;
            table.GetOrCreateCell(i, 2).Text = "Engineering";
        }

        var signOff = body.AddBlock(spacing: 20);
        signOff.AddRectangle(515, 1, PdfColor.Black);
        signOff.AddText("Approved by: ____________________", spacing: 10);

        var pages = PdfPageReader.Save(pdfDocument);
        Assert.Equal(4, pages.Count);
        for (int p = 0; p < pages.Count; p++) Assert.True(pages[p].Shows($"Page {p + 1} of 4"));
        Assert.All(pages.Skip(1), page => Assert.True(page.Shows("Department")));
        Assert.True(pages[pages.Count - 1].Shows("Approved by: ____________________"));
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

    private static double LineOffset(double size) => size * 1.2 / 2 + size / 3;

    private static PdfTable Rows(PdfTable table, int rows, string prefix)
    {
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = prefix + r;
        return table;
    }

    private static void NumberedTable(PdfPageBuilder page, double x, double y, double width, int rows, string prefix)
    {
        var table = page.AddTable(x, y, width);
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = prefix + r;
    }

    #endregion

    #endregion
}
