using System.Diagnostics;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests;

/// <summary>
/// <c>Body.Remaining</c> and <c>Body.MaxPages</c> (WR-033): the remaining height is exact at save or saving fails
/// naming what changed, and a page limit makes saving fail naming the content that does not fit.
/// </summary>
public class BodyRemainingTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Remaining_is_the_height_left_where_the_next_item_lands()
    {
        var document = new PdfDocument();
        Assert.Equal(A4Height, document.Body.Remaining, Precision);

        document.Body.AddText("a");
        double remaining = document.Body.Remaining;
        Assert.Equal(A4Height - 14.4, remaining, Precision);

        // A rectangle exactly that tall fills the page to the body bottom.
        document.Body.AddRectangle(100, remaining, PdfColor.Black);
        var pages = PdfPageReader.Save(document);
        Assert.Single(pages);
        Assert.Equal(0, pages[0].LayoutRectangles().Single().Y, Precision);
        Assert.Equal(0, document.Body.Remaining, Precision);
    }

    [Fact]
    public void Remaining_follows_the_flow_onto_new_pages_and_caller_pages()
    {
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Body.AddRectangle(100, 700, PdfColor.Black);
        document.Body.AddRectangle(100, 200, PdfColor.Black);
        Assert.Equal(A4Height - 40 - 200, document.Body.Remaining, Precision);

        document.AddPage();
        Assert.Equal(A4Height - 40, document.Body.Remaining, Precision);
        PdfPageReader.SaveBytes(document);
    }

    [Fact]
    public void A_flow_table_that_grows_after_a_read_makes_saving_fail_naming_it()
    {
        var document = new PdfDocument();
        var table = Table(document.Body.AddTable(200), 3);
        _ = document.Body.Remaining;
        table.AddRow().GetCell(0).Text = "late";

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        Assert.Contains("Body.Remaining was read as 781.89 pt on page 1 before flow item 2", error.Message);
        Assert.Contains("flow item 1 (table) changed its layout", error.Message);
    }

    [Fact]
    public void A_block_item_added_after_a_read_makes_saving_fail()
    {
        var document = new PdfDocument();
        var block = document.Body.AddBlock();
        block.AddText("one");
        _ = document.Body.Remaining;
        block.AddText("two");

        Assert.Contains("flow item 1 (block) changed its layout", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document)).Message);
    }

    [Fact]
    public void Layout_settings_changed_after_a_read_make_saving_fail()
    {
        void Check(Action<PdfDocument> change)
        {
            var document = new PdfDocument();
            document.Body.AddText("a");
            _ = document.Body.Remaining;
            change(document);
            Assert.Contains("page layout settings", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document)).Message);
        }

        Check(d => d.Header.Height = 50);
        Check(d => d.Footer.Height = 30);
        Check(d => d.Body.TopGap = 10);
        Check(d => d.AddPageRule(1).Header.Height = 30);
    }

    [Fact]
    public void Changes_that_move_nothing_are_allowed_after_a_read()
    {
        var document = new PdfDocument();
        var table = Table(document.Body.AddTable(200), 3);
        _ = document.Body.Remaining;
        table.GetCell(0, 0).Background = PdfColor.LightGray;
        table.GetCell(1, 0).Text = "renamed";
        document.LeftMargin = 20;

        PdfPageReader.SaveBytes(document);
    }

    [Fact]
    public void Reading_remaining_after_every_line_stays_fast()
    {
        var document = new PdfDocument();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            document.Body.AddText("line " + i);
            _ = document.Body.Remaining;
        }
        PdfPageReader.SaveBytes(document);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
    }

    [Fact]
    public void A_document_within_its_page_limit_saves()
    {
        var document = new PdfDocument();
        document.Body.MaxPages = 2;
        for (int i = 0; i < 100; i++) document.Body.AddText("line " + i);

        Assert.Equal(2, PdfPageReader.Save(document).Count);
    }

    [Fact]
    public void Exceeding_the_page_limit_fails_before_output_naming_what_does_not_fit()
    {
        var document = new PdfDocument();
        document.Body.MaxPages = 1;
        for (int i = 0; i < 60; i++) document.Body.AddText("line " + i);

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        // 58 lines of 14.4 pt fit the 841.89 pt body; lines 59 and 60 do not.
        Assert.Contains("The content needs 2 pages, but Body.MaxPages is 1", error.Message);
        Assert.Contains("flow items 59 to 60 (text) do not fit, 28.8 pt over", error.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Body.MaxPages = 0);
    }

    #endregion

    #region Private Methods

    private static PdfTable Table(PdfTable table, int rows)
    {
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = "R" + r;
        return table;
    }

    #endregion

    #endregion
}
