using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Table pagination (WR-027), page margins (WR-028), and the table end query (WR-029): rows continue on library-added
/// continuation pages, header rows repeat and never end a page, tables on one page share continuation pages, and
/// saving fails before any output when a layout is impossible or a queried end went stale.
/// </summary>
public class TablePaginationTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_table_that_fits_its_page_adds_no_page()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        NumberedTable(page, 700, 35);

        var pages = PdfPageReader.Save(document);

        Assert.Single(pages);
        Assert.True(pages[0].Shows("R34"));
    }

    [Fact]
    public void A_table_on_a_continuation_page_continues_on_the_same_chain()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var first = NumberedTable(page, 700, 60);
        var end = page.GetTableEnd(first);

        // 25 rows are on the only continuation page, ending at 841.89 - 500; 80 more rows need two further pages.
        var second = end.Page.AddTable(50, end.Y - 20, 300);
        second.AddColumn();
        for (int r = 0; r < 80; r++) second.AddRow().GetCell(0).Text = "S" + r;
        document.AddPage().AddText("LAST", 50, 400);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(5, pages.Count);
        Assert.True(pages[1].Shows("R59") && pages[1].Shows("S0"));
        Assert.True(pages[3].Shows("S79"));
        Assert.True(pages[4].Shows("LAST"));
        AssertEachRowOnOnePage(pages, "S", 80);
    }

    [Fact]
    public void A_table_without_rows_ends_at_its_own_top()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = page.AddTable(50, 640, 400);

        var end = page.GetTableEnd(table);

        Assert.Same(page, end.Page);
        Assert.Equal(640, end.Y);
        Assert.Empty(end.ContinuationPages);
    }

    [Fact]
    public void At_least_image_row_that_grows_past_the_bottom_moves_to_the_next_page()
    {
        // A 200 x 80 px image in a 100 pt column is 92 x 36.8 pt, so its AtLeast row grows to 44.8 pt.
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = page.AddTable(50, 60, 100);
        table.AddColumn();
        table.AddRow().GetCell(0).Text = "first";
        var row = table.AddRow();
        row.HeightRule = PdfRowHeightRule.AtLeast;
        row.GetCell(0).AddContent(PdfImage.FromBytes(new JpegBuilder { Width = 200, Height = 80 }.Build()));

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.True(pages[0].Shows("first"));
        Assert.DoesNotContain("Do", pages[0].Content);
        Assert.Contains("/Im1 Do", pages[1].Content);
    }

    [Fact]
    public void Changing_a_queried_table_without_changing_its_layout_still_saves()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = NumberedTable(page, 700, 60);
        page.GetTableEnd(table);

        table.GetCell(3, 0).Background = PdfColor.LightGray;

        Assert.Equal(2, PdfPageReader.Save(document).Count);
    }

    [Fact]
    public void Content_added_to_the_end_page_is_drawn_after_the_table()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("BELOW", 50, 100);
        var table = NumberedTable(page, 700, 60);

        var end = page.GetTableEnd(table);
        end.Page.AddText("TOTAL", 350, (int)(end.Y - 20));

        // 35 rows fit on page 1; the other 25 end at 841.89 - 25 x 20 on the continuation page.
        Assert.Single(end.ContinuationPages);
        Assert.Same(end.ContinuationPages[0], end.Page);
        Assert.Equal(A4Height - 500, end.Y, Precision);

        var pages = PdfPageReader.Save(document);
        Assert.True(pages[0].Shows("BELOW"));
        Assert.False(pages[1].Shows("BELOW"));
        Assert.True(pages[1].Shows("TOTAL"));
    }

    [Fact]
    public void Continuation_pages_come_before_later_pages_and_keep_the_starting_page_size()
    {
        var document = new PdfDocument(PdfPageSize.Letter);
        var page = document.AddPage();
        NumberedTable(page, 700, 60);
        document.PageSize = PdfPageSize.A4;
        document.AddPage().AddText("P2", 50, 700);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(3, pages.Count);
        Assert.Equal("[0 0 612 792]", pages[0].MediaBox);
        Assert.Equal("[0 0 612 792]", pages[1].MediaBox);
        Assert.True(pages[1].Shows("R59"));
        Assert.Equal("[0 0 595.28 841.89]", pages[2].MediaBox);
        Assert.True(pages[2].Shows("P2"));
    }

    [Fact]
    public void Continued_rows_resume_at_the_top_margin_with_the_same_x_and_width()
    {
        foreach (double topMargin in new[] { 0.0, 50.0 })
        {
            var document = new PdfDocument { TopMargin = topMargin };
            var page = document.AddPage();
            NumberedTable(page, 700, 60);

            var first = PdfPageReader.Save(document)[1].FilledRectangles()[0];

            Assert.Equal(50, first.X, Precision);
            Assert.Equal(300, first.Width, Precision);
            Assert.Equal(A4Height - topMargin - 20, first.Y, Precision);
        }
    }

    [Fact]
    public void Continuation_pages_draw_closed_cells_and_no_marker()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        NumberedTable(page, 700, 60);

        var continuation = PdfPageReader.Save(document)[1].Content;

        // 25 one-cell rows: one filled and one stroked rectangle and one text per cell, nothing else.
        Assert.Equal(25, CountOf(continuation, " re f"));
        Assert.Equal(25, CountOf(continuation, " re s"));
        Assert.Equal(25, CountOf(continuation, ") Tj"));
    }

    [Fact]
    public void Default_settings_change_nothing()
    {
        var document = new PdfDocument();
        var table = document.AddPage().AddTable(50, 700, 300);

        Assert.Equal(0, document.TopMargin);
        Assert.Equal(0, document.BottomMargin);
        Assert.Equal(0, table.HeaderRowCount);
        Assert.False(table.RepeatHeaderRows);
    }

    [Fact]
    public void Each_continuation_page_can_get_its_own_footer()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = NumberedTable(page, 700, 150);

        var end = page.GetTableEnd(table);
        for (int i = 0; i < end.ContinuationPages.Count; i++)
        {
            end.ContinuationPages[i].AddText("Footer " + (i + 1), 50, 10);
        }

        var pages = PdfPageReader.Save(document);
        Assert.Equal(end.ContinuationPages.Count + 1, pages.Count);
        for (int i = 1; i < pages.Count; i++)
        {
            Assert.True(pages[i].Shows("Footer " + i));
        }
        Assert.Same(end.ContinuationPages[end.ContinuationPages.Count - 1], end.Page);
    }

    [Fact]
    public void Every_row_is_drawn_once_in_order_across_pages()
    {
        // 35 rows fit from y = 700; each A4 continuation page holds 42 rows of 20 pt.
        var document = new PdfDocument();
        var page = document.AddPage();
        NumberedTable(page, 700, 120);

        var pages = PdfPageReader.Save(document);

        Assert.Equal(4, pages.Count);
        Assert.True(pages[0].Shows("R34") && !pages[0].Shows("R35"));
        Assert.True(pages[1].Shows("R35") && pages[1].Shows("R76"));
        Assert.True(pages[2].Shows("R77") && pages[2].Shows("R118"));
        Assert.True(pages[3].Shows("R119"));
        AssertEachRowOnOnePage(pages, "R", 120);
    }

    [Fact]
    public void Header_rows_are_never_left_alone_at_a_page_bottom()
    {
        // The header fits above the bottom (30 - 20 = 10) but the first data row does not, so both move.
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = HeaderTable(page, 30, 10);

        var pages = PdfPageReader.Save(document);

        Assert.Empty(pages[0].FilledRectangles());
        Assert.True(pages[1].Shows("H") && pages[1].Shows("R0"));
        Assert.Equal(A4Height - 20, pages[1].FilledRectangles()[0].Y, Precision);
        Assert.Equal(1, table.HeaderRowCount);
    }

    [Fact]
    public void Header_rows_repeat_on_every_continuation_page_only_when_switched_on()
    {
        foreach (bool repeat in new[] { true, false })
        {
            var document = new PdfDocument { TopMargin = 36 };
            var page = document.AddPage();
            var table = HeaderTable(page, 700, 150);
            table.RepeatHeaderRows = repeat;

            var pages = PdfPageReader.Save(document);

            Assert.True(pages.Count > 2);
            Assert.True(pages[0].Shows("H"));
            foreach (var continuation in pages.Skip(1))
            {
                Assert.Equal(repeat, continuation.Shows("H"));
                if (repeat) Assert.Equal(A4Height - 36 - 20, continuation.FilledRectangles()[0].Y, Precision);
            }
            AssertEachRowOnOnePage(pages, "R", 150);
        }
    }

    [Fact]
    public void Header_rows_that_cannot_fit_with_a_data_row_fail_the_save()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = HeaderTable(page, 800, 50);
        table.RepeatHeaderRows = true;
        table.Rows[0].Height = 830;

        var error = Assert.Throws<InvalidOperationException>(() => SaveToMemory(document));
        Assert.Contains("header row", error.Message);
    }

    [Fact]
    public void Header_row_count_covering_every_row_lays_them_out_as_ordinary_rows()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = NumberedTable(page, 700, 60);
        table.HeaderRowCount = 60;
        table.RepeatHeaderRows = true;

        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.False(pages[1].Shows("R0"));
        AssertEachRowOnOnePage(pages, "R", 60);
    }

    [Fact]
    public void Invalid_settings_are_rejected()
    {
        var document = new PdfDocument();
        foreach (double value in new[] { -1, double.NaN, double.PositiveInfinity })
        {
            Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => document.TopMargin = value).ParamName);
            Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => document.BottomMargin = value).ParamName);
        }

        var table = document.AddPage().AddTable(50, 700, 300);
        Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => table.HeaderRowCount = -1).ParamName);
        Assert.Equal(0, document.TopMargin);
        Assert.Equal(0, table.HeaderRowCount);
    }

    [Fact]
    public void Margins_that_leave_no_room_make_saving_a_table_fail()
    {
        var document = new PdfDocument { TopMargin = 500, BottomMargin = 400 };
        NumberedTable(document.AddPage(), 300, 1);

        Assert.Throws<InvalidOperationException>(() => SaveToMemory(document));
    }

    [Fact]
    public void Querying_a_table_from_another_page_or_null_is_rejected()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        var table = first.AddTable(50, 700, 300);

        Assert.Equal("table", Assert.Throws<ArgumentNullException>(() => first.GetTableEnd(null!)).ParamName);
        Assert.Equal("table", Assert.Throws<ArgumentException>(() => second.GetTableEnd(table)).ParamName);
    }

    [Fact]
    public void Readme_tables_across_pages_sample_runs_as_written()
    {
        // The code of README.md and nuget/README.md "Tables across pages", saved to memory instead of a file (PR-012).
        var pdfDocument = new PdfDocument { TopMargin = 40, BottomMargin = 50 };
        var page = pdfDocument.AddPage();

        var table = page.AddTable(40, 760, 515);
        table.SetColumnWidths(60, 255, 200);

        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        table.GetOrCreateCell(0, 0).Text = "No.";
        table.GetOrCreateCell(0, 1).Text = "Name";
        table.GetOrCreateCell(0, 2).Text = "Department";

        for (int i = 1; i <= 200; i++)
        {
            table.GetOrCreateCell(i, 0).Text = i.ToString();
            table.GetOrCreateCell(i, 1).Text = "Employee " + i;
            table.GetOrCreateCell(i, 2).Text = "Engineering";
        }

        var end = page.GetTableEnd(table);
        end.Page.AddText("200 employees", 40, (int)(end.Y - 20), 10);
        for (int i = 0; i < end.ContinuationPages.Count; i++)
        {
            end.ContinuationPages[i].AddText($"Page {i + 2}", 500, 20, 9);
        }

        var pages = PdfPageReader.Save(pdfDocument);
        Assert.Equal(end.ContinuationPages.Count + 1, pages.Count);
        Assert.True(pages.Count > 1);
        Assert.True(pages[pages.Count - 1].Shows("200 employees"));
        Assert.All(pages.Skip(1), p => Assert.True(p.Shows("Department")));
    }

    [Fact]
    public void Row_taller_than_the_page_fails_before_anything_is_written()
    {
        foreach (var rule in new[] { PdfRowHeightRule.Exactly, PdfRowHeightRule.AtLeast })
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var table = page.AddTable(50, 800, 160);
            table.AddColumn();
            table.AddRow().GetCell(0).Text = "before";
            var row = table.AddRow();
            row.HeightRule = rule;
            if (rule == PdfRowHeightRule.Exactly)
            {
                row.Height = 900;
            }
            else
            {
                // 200 x 1300 px is 150 x 975 pt, which fits the 152 pt padded width unscaled.
                row.GetCell(0).AddContent(PdfImage.FromBytes(new JpegBuilder { Width = 200, Height = 1300 }.Build()));
            }

            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "existing");
                var error = Assert.Throws<InvalidOperationException>(() => document.Save(path));
                Assert.Contains("Row index 1 of table 1 on page 1", error.Message);
                Assert.Equal("existing", File.ReadAllText(path));
                Assert.Throws<InvalidOperationException>(() => page.GetTableEnd(table));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Saving_again_gives_the_same_bytes_and_adds_pages_for_new_rows_only()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = NumberedTable(page, 700, 60);

        var first = PdfPageReader.SaveBytes(document);
        Assert.Equal(first, PdfPageReader.SaveBytes(document));

        for (int r = 60; r < 130; r++) table.AddRow().GetCell(0).Text = "R" + r;
        var pages = PdfPageReader.Save(document);

        Assert.Equal(4, pages.Count);
        AssertEachRowOnOnePage(pages, "R", 130);
    }

    [Fact]
    public async Task Save_async_writes_the_same_bytes_as_save()
    {
        var document = new PdfDocument { BottomMargin = 36 };
        var page = document.AddPage();
        NumberedTable(page, 700, 90);

        var sync = PdfPageReader.SaveBytes(document);
        using var stream = new MemoryStream();
        await document.SaveAsync(stream);

        Assert.Equal(sync, stream.ToArray());
    }

    [Fact]
    public void Side_by_side_tables_share_continuation_pages()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("NOTE", 50, 820);
        page.AddImage(PdfImage.FromBytes(new JpegBuilder { Width = 40, Height = 40 }.Build()), 500, 800);
        NumberedTable(page, 700, 80, x: 50, width: 200, prefix: "A");
        NumberedTable(page, 700, 150, x: 300, width: 200, prefix: "B");

        var pages = PdfPageReader.Save(document);

        // B needs 1 + ceil((150 - 35) / 42) = 4 pages; A's 45 continued rows use the first two continuation pages.
        Assert.Equal(4, pages.Count);
        Assert.True(pages[1].Shows("A35") && pages[1].Shows("B35"));
        Assert.True(pages[2].Shows("A79") && pages[2].Shows("B118"));
        Assert.True(pages[3].Shows("B149"));
        Assert.All(pages.Skip(1), p => Assert.False(p.Shows("NOTE")));
        Assert.All(pages.Skip(1), p => Assert.DoesNotContain("Do", p.Content));
        Assert.All(pages[1].FilledRectangles().Where(r => r.Width == 200), r => Assert.True(r.X == 50 || r.X == 300));
        AssertEachRowOnOnePage(pages, "A", 80);
        AssertEachRowOnOnePage(pages, "B", 150);
    }

    [Fact]
    public void Stale_table_end_fails_the_save_until_queried_again()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing");

            var document = new PdfDocument();
            var page = document.AddPage();
            var table = NumberedTable(page, 700, 60);
            page.GetTableEnd(table);

            for (int r = 60; r < 110; r++) table.AddRow().GetCell(0).Text = "R" + r;
            var error = Assert.Throws<InvalidOperationException>(() => document.Save(path));
            Assert.Contains("Table 1 on page 1 no longer lays out", error.Message);
            Assert.Equal("existing", File.ReadAllText(path));

            page.GetTableEnd(table);
            document.BottomMargin = 100;
            Assert.Throws<InvalidOperationException>(() => document.Save(path));
            Assert.Equal("existing", File.ReadAllText(path));

            page.GetTableEnd(table);
            document.Save(path);
            PdfStructureChecker.AssertValid(File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Table_whose_first_row_has_no_room_starts_on_the_next_page()
    {
        var document = new PdfDocument { BottomMargin = 36 };
        var page = document.AddPage();
        page.AddText("ABOVE", 50, 60);
        var table = NumberedTable(page, 30, 5);

        var end = page.GetTableEnd(table);
        var pages = PdfPageReader.Save(document);

        Assert.Equal(2, pages.Count);
        Assert.Empty(pages[0].FilledRectangles());
        Assert.True(pages[0].Shows("ABOVE"));
        Assert.Equal(A4Height - 20, pages[1].FilledRectangles()[0].Y, Precision);
        Assert.Single(end.ContinuationPages);
        Assert.Equal(A4Height - 100, end.Y, Precision);
    }

    [Fact]
    public void Security_page_list_stays_consistent_when_pages_are_added_between_saves()
    {
        // A08: /Count matches /Kids, and every object stays reachable and numbered in order after re-saving.
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = NumberedTable(page, 700, 60);
        PdfPageReader.SaveBytes(document);
        for (int r = 60; r < 200; r++) table.AddRow().GetCell(0).Text = "R" + r;
        document.AddPage().AddText("P2", 50, 700);

        var bytes = PdfPageReader.SaveBytes(document);
        var pages = PdfPageReader.Read(bytes);

        Assert.Contains($"/Count {pages.Count}", Encoding.Latin1.GetString(bytes));
        Assert.True(pages[pages.Count - 1].Shows("P2"));
        AssertEachRowOnOnePage(pages, "R", 200);
    }

    [Fact]
    public void Security_pages_never_outnumber_data_rows_and_output_ignores_the_culture()
    {
        // A06: no amplification (at most one page per data row) and the same bytes in every culture.
        byte[] Build()
        {
            var document = new PdfDocument { TopMargin = 36.5, BottomMargin = 36.5 };
            var table = HeaderTable(document.AddPage(), 700.25, 1000);
            table.RepeatHeaderRows = true;
            return PdfPageReader.SaveBytes(document);
        }

        byte[] invariant;
        using (new CultureScope(""))
        {
            invariant = Build();
        }

        using (new CultureScope("de-DE"))
        {
            Assert.Equal(invariant, Build());
        }

        Assert.InRange(PdfPageReader.Read(invariant).Count, 2, 1001);
    }

    [Fact]
    public void Security_repeated_header_text_is_escaped_on_every_page()
    {
        // A05: header text with PDF string delimiters cannot inject operators on continuation pages.
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = HeaderTable(page, 700, 120);
        table.GetCell(0, 0).Text = @"Name (a\b";
        table.RepeatHeaderRows = true;

        var pages = PdfPageReader.Save(document);

        Assert.True(pages.Count > 2);
        Assert.All(pages, p => Assert.Contains(@"(Name \(a\\b) Tj", p.Content));
    }

    [Fact]
    public void The_bottom_margin_decides_where_a_table_breaks()
    {
        foreach (var (margin, lastOnFirstPage) in new[] { (0.0, 34), (72.0, 30) })
        {
            var document = new PdfDocument { BottomMargin = margin };
            NumberedTable(document.AddPage(), 700, 60);

            var pages = PdfPageReader.Save(document);

            Assert.True(pages[0].Shows("R" + lastOnFirstPage));
            Assert.False(pages[0].Shows("R" + (lastOnFirstPage + 1)));
            Assert.All(pages[0].FilledRectangles(), r => Assert.True(r.Y >= margin - 1e-6));
        }
    }

    #endregion

    #region Private Methods

    private static void AssertEachRowOnOnePage(IReadOnlyList<PdfPageReader.Page> pages, string prefix, int rows)
    {
        for (int r = 0; r < rows; r++)
        {
            Assert.Equal(1, pages.Count(p => p.Shows(prefix + r)));
        }
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    // A one-column table whose first row is the header "H", followed by rows "R0" to "R{rows - 1}".
    private static PdfTable HeaderTable(PdfPageBuilder page, double y, int rows)
    {
        var table = page.AddTable(50, y, 300);
        table.AddColumn();
        table.AddRow().GetCell(0).Text = "H";
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = "R" + r;
        table.HeaderRowCount = 1;
        return table;
    }

    // A one-column table of 20 pt rows labelled prefix + index.
    private static PdfTable NumberedTable(PdfPageBuilder page, double y, int rows, double x = 50, double width = 300, string prefix = "R")
    {
        var table = page.AddTable(x, y, width);
        table.AddColumn();
        for (int r = 0; r < rows; r++) table.AddRow().GetCell(0).Text = prefix + r;
        return table;
    }

    private static void SaveToMemory(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
    }

    #endregion

    #endregion
}
