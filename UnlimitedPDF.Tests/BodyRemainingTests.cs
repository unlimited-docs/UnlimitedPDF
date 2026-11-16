using System.Diagnostics;
using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// <c>Body.RemainingHeight</c> and <c>Body.MaxPages</c> (WR-033): the remaining height is exact at save or saving fails
/// naming what changed, and a page limit makes saving fail naming the content that does not fit.
/// </summary>
[Collection(nameof(TimingTestsCollection))]
public class BodyRemainingTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;

    // 4x the work should take about 4x the time; quadratic code takes about 16x. 8 sits safely between the two.
    private const double MaxGrowthForFourTimesTheWork = 8;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Remaining_is_the_height_left_where_the_next_item_lands()
    {
        var document = new PdfDocument();
        Assert.Equal(A4Height, document.Body.RemainingHeight, Precision);

        document.Body.AddText("a");
        double remaining = document.Body.RemainingHeight;
        Assert.Equal(A4Height - 14.4, remaining, Precision);

        // A rectangle exactly that tall fills the page to the body bottom.
        document.Body.AddRectangle(100, remaining, PdfColor.Black);
        var pages = PdfPageReader.Save(document);
        Assert.Single(pages);
        Assert.Equal(0, pages[0].LayoutRectangles().Single().Y, Precision);
        Assert.Equal(0, document.Body.RemainingHeight, Precision);
    }

    [Fact]
    public void Remaining_follows_the_flow_onto_new_pages_and_caller_pages()
    {
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Body.AddRectangle(100, 700, PdfColor.Black);
        document.Body.AddRectangle(100, 200, PdfColor.Black);
        Assert.Equal(A4Height - 40 - 200, document.Body.RemainingHeight, Precision);

        document.AddPage();
        Assert.Equal(A4Height - 40, document.Body.RemainingHeight, Precision);
        PdfPageReader.SaveBytes(document);
    }

    [Fact]
    public void A_flow_table_that_grows_after_a_read_makes_saving_fail_naming_it()
    {
        var document = new PdfDocument();
        var table = Table(document.Body.AddTable(200), 3);
        _ = document.Body.RemainingHeight;
        table.AddRow().GetCell(0).Text = "late";

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        Assert.Contains("Body.RemainingHeight was read as 781.89 pt on page 1 before flow item 2", error.Message);
        Assert.Contains("flow item 1 (table) changed its layout", error.Message);
    }

    [Fact]
    public void A_block_item_added_after_a_read_makes_saving_fail()
    {
        var document = new PdfDocument();
        var block = document.Body.AddBlock();
        block.AddText("one");
        _ = document.Body.RemainingHeight;
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
            _ = document.Body.RemainingHeight;
            change(document);
            Assert.Contains("page layout settings", Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document)).Message);
        }

        Check(d => d.Header.Height = 50);
        Check(d => d.Footer.Height = 30);
        Check(d => d.BodyMarginTop = 10);
        Check(d => d.AddPageSettings(1).Header.Height = 30);
    }

    [Fact]
    public void Changes_that_move_nothing_are_allowed_after_a_read()
    {
        var document = new PdfDocument();
        var table = Table(document.Body.AddTable(200), 3);
        _ = document.Body.RemainingHeight;
        table.GetCell(0, 0).BackgroundColor = PdfColor.LightGray;
        table.GetCell(1, 0).Text = "renamed";
        document.MarginLeft = 20;

        PdfPageReader.SaveBytes(document);
    }

    [Fact]
    public void A_read_after_page_coordinate_tables_change_is_exact()
    {
        // Review F-02: a table placed on the page after an earlier read was ignored by later reads.
        var document = new PdfDocument();
        document.Footer.Height = 20;
        var page = document.AddPage();
        _ = document.Body.RemainingHeight;
        var placed = page.AddTable(300, 400, 200);
        placed.AddColumn();
        for (int r = 0; r < 40; r++) placed.AddRow().GetCell(0).Text = "P" + r;
        for (int i = 0; i < 60; i++) document.Body.AddText("x" + i);

        // 19 table rows fit page 1 above the 20 pt footer; page 2 carries the other 21 down to 421.89. After 60 lines
        // (57 on page 1) the cursor is 3 lines below the carried rows.
        Assert.Equal(421.89 - 3 * 14.4 - 20, document.Body.RemainingHeight, Precision);
        PdfPageReader.SaveBytes(document);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("x")]
    [InlineData("header")]
    public void A_read_after_a_page_coordinate_table_moves_or_changes_its_header_rows_is_exact(string change)
    {
        // Review F-12: the live plan was reused when a carried table's position or header rows changed, so the read
        // returned 358.69 pt where a fresh plan gives 558.69 pt, and the save then failed.
        static (PdfDocument Document, PdfTable Table) Build()
        {
            var document = new PdfDocument();
            document.Footer.Height = 20;
            var table = document.AddPage().AddTable(300, 400, 200);
            table.AddColumn();
            for (int r = 0; r < 40; r++) table.AddRow().GetCell(0).Text = "P" + r;
            for (int i = 0; i < 5; i++) document.Body.AddText("x" + i);
            return (document, table);
        }

        static void Change(PdfTable table, string change)
        {
            if (change == "y") table.Y = 600;
            else if (change == "x") table.X = 320;
            else
            {
                table.HeaderRowCount = 1;
                table.RepeatHeaderRows = true;
            }
        }

        var (document, table) = Build();
        _ = document.Body.RemainingHeight;
        Change(table, change);
        for (int i = 5; i < 60; i++) document.Body.AddText("x" + i);

        var (fresh, freshTable) = Build();
        Change(freshTable, change);
        for (int i = 5; i < 60; i++) fresh.Body.AddText("x" + i);

        Assert.Equal(fresh.Body.RemainingHeight, document.Body.RemainingHeight, Precision);
        if (change == "y") Assert.Equal(558.69, document.Body.RemainingHeight, Precision);
        PdfPageReader.SaveBytes(document);
    }

    [Fact]
    public void A_read_that_throws_does_not_leave_a_stale_plan()
    {
        // Review F-07: the failed read advanced the live plan past the oversize item, so the next read returned a value.
        var document = new PdfDocument();
        document.Body.AddText("a");
        document.Body.AddRectangle(100, 2000, PdfColor.Black);

        Assert.Throws<InvalidOperationException>(() => document.Body.RemainingHeight);
        Assert.Throws<InvalidOperationException>(() => document.Body.RemainingHeight);
    }

    [Fact]
    public void A_page_size_change_after_an_early_read_makes_saving_fail()
    {
        // Review F-09: a read before any page was replayed with the old page size, so the save passed.
        var document = new PdfDocument();
        document.Footer.Height = 20;
        Assert.Equal(A4Height - 20, document.Body.RemainingHeight, Precision);
        document.PageSize = PdfPageSize.Letter;
        document.AddPage();
        document.Body.AddText("x");

        var error = Assert.Throws<InvalidOperationException>(() => PdfPageReader.SaveBytes(document));

        Assert.Contains("page layout settings", error.Message);
    }

    [Theory]
    [InlineData("kept table")]
    [InlineData("kept lines")]
    public void A_read_while_kept_items_wait_gives_the_room_the_next_item_really_gets(string waiting)
    {
        // Review F-21: the read ignored the waiting kept table's last row, which moves to page 2 with the kept heading
        // (817.89 pt read, 797.89 pt drawn), and an item as tall as the read then failed to save.
        static PdfDocument Build(string waiting)
        {
            var document = new PdfDocument();
            if (waiting == "kept table")
            {
                var table = document.Body.AddTable(200, keepWithNext: true);
                table.AddColumn();
                for (int r = 0; r < 41; r++) table.AddRow().GetCell(0).Text = "K" + r;
                document.Body.AddText("Heading", fontSize: 20, keepWithNext: true);
            }
            else
            {
                // 20 pt are left: the first kept line fits there, the second does not, and they stay together.
                document.Body.AddRectangle(100, A4Height - 20, PdfColor.Black);
                document.Body.AddText("first", keepWithNext: true);
                document.Body.AddText("second", keepWithNext: true);
            }
            return document;
        }

        var document = Build(waiting);
        double remaining = document.Body.RemainingHeight;
        document.Body.AddRectangle(100, remaining, PdfColor.Black);

        var pages = PdfPageReader.Save(document);

        double expected = waiting == "kept table" ? A4Height - 20 - 24 : A4Height - 2 * 14.4;
        Assert.Equal(expected, remaining, Precision);
        Assert.Equal(2, pages.Count);
        var last = pages[1].LayoutRectangles().Last();
        Assert.Equal(0, last.Y, Precision);
        Assert.Equal(remaining, last.Height, Precision);
    }

    [Fact]
    public void Reading_remaining_with_a_large_page_coordinate_table_stays_within_its_budget()
    {
        // Review F-22 (A-20): each read checks the page-coordinate tables, so it gets slower as they get more rows.
        // That's fine, as long as 4x the rows means about 4x the time and no worse.
        AssertGrowsLinearly(rows =>
        {
            var document = new PdfDocument();
            var table = document.AddPage().AddTable(300, 800, 200);
            table.AddColumn();
            for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = "P" + r;
            for (int i = 0; i < 500; i++)
            {
                document.Body.AddText("line " + i);
                _ = document.Body.RemainingHeight;
            }
            PdfPageReader.SaveBytes(document);
        }, size: 250);
    }

    [Fact]
    public void Reading_remaining_along_a_chain_of_kept_tables_stays_fast()
    {
        // Review F-32: each read copied the plan back to the chain's first kept table, so 4,000 kept tables took 2.9 s.
        AssertGrowsLinearly(tables =>
        {
            var document = new PdfDocument();
            for (int i = 0; i < tables; i++)
            {
                var table = document.Body.AddTable(200, keepWithNext: true);
                table.AddColumn();
                table.AddRow().GetCell(0).Text = "a" + i;
                table.AddRow().GetCell(0).Text = "b" + i;
                _ = document.Body.RemainingHeight;
            }
            PdfPageReader.SaveBytes(document);
        }, size: 1_000);
    }

    [Fact]
    public void Reading_remaining_inside_kept_runs_stays_fast()
    {
        // Review F-27: each read inside a waiting kept run copied the whole plan of the page sequence, so reads grew
        // quadratically (40,000 lines took about 3.6 s).
        AssertGrowsLinearly(lines =>
        {
            var document = new PdfDocument();
            for (int i = 0; i < lines; i++)
            {
                document.Body.AddText("line " + i, keepWithNext: i % 2 == 0);
                _ = document.Body.RemainingHeight;
            }
            PdfPageReader.SaveBytes(document);
        }, size: 10_000);
    }

    [Fact]
    public void Reading_remaining_after_every_line_stays_fast()
    {
        AssertGrowsLinearly(lines =>
        {
            var document = new PdfDocument();
            for (int i = 0; i < lines; i++)
            {
                document.Body.AddText("line " + i);
                _ = document.Body.RemainingHeight;
            }
            PdfPageReader.SaveBytes(document);
        }, size: 5_000);
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

    // A fixed time limit kept failing on busy machines even though the code was fine (QF-26111206). So instead we check
    // how the time grows: do 4x the work and it should take roughly 4x as long. If it takes 16x, reads have gone
    // quadratic again. We warm up first and keep the fastest of three tries, so a slow machine or a busy moment
    // doesn't skew the result.
    private static void AssertGrowsLinearly(Action<int> run, int size)
    {
        run(size / 4);
        var small = Fastest(() => run(size));
        var large = Fastest(() => run(4 * size));
        var ratio = large.TotalMilliseconds / Math.Max(small.TotalMilliseconds, 1);

        Assert.True(
            ratio < MaxGrowthForFourTimesTheWork,
            $"{size:N0} took {small.TotalMilliseconds:F0} ms and {4 * size:N0} took {large.TotalMilliseconds:F0} ms: " +
            $"{ratio:F1} times as long, where linear work is about 4.");
    }

    private static TimeSpan Fastest(Action run)
    {
        var fastest = TimeSpan.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var watch = Stopwatch.StartNew();
            run();
            watch.Stop();
            if (watch.Elapsed < fastest) fastest = watch.Elapsed;
        }

        return fastest;
    }

    private static PdfTable Table(PdfTable table, int rows)
    {
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = "R" + r;
        return table;
    }

    #endregion

    #endregion
}

/// <summary>
/// Tests that compare elapsed times run apart from the parallel tests, so CPU load from other tests cannot change
/// their ratios.
/// </summary>
[CollectionDefinition(nameof(TimingTestsCollection), DisableParallelization = true)]
public class TimingTestsCollection
{
}
