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

    private static void NumberedTable(PdfPageBuilder page, double x, double y, double width, int rows, string prefix)
    {
        var table = page.AddTable(x, y, width);
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = prefix + r;
    }

    #endregion

    #endregion
}
