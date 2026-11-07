namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Splits a table's rows into page parts (WR-027). Rows are never split; a row that would pass the body bottom of its
/// page moves to the next page, where the table resumes at that page's body top below the repeated header rows. Header
/// rows are never the last rows on a page. Pages are counted from the table's own page (0).
/// </summary>
internal static class TablePaginator
{
    #region Fields and Constants

    // A row that ends within this distance below the body bottom still fits, so rounding never moves a row.
    internal const double Tolerance = 1e-6;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Splits <paramref name="table"/> into parts.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="layout">The table's row and cell layout.</param>
    /// <param name="startY">The y-coordinate of the table's top edge on its own page, in points.</param>
    /// <param name="edges">Returns the top and bottom edges of the room on the page <c>n</c> pages after the table's own
    /// page, and whether that room is the page's whole, empty body. On the table's own page the top is the body top
    /// (or the table's start in the flow), which its rows must also fit below.</param>
    /// <param name="describe">Returns the table's description for error messages, for example "table 1 on page 2".</param>
    /// <returns>The parts in page order and the header rows in effect.</returns>
    /// <exception cref="InvalidOperationException">A row, or the header rows with a data row, cannot fit on an empty page.</exception>
    public static (List<Split> Parts, int HeaderRowCount) Paginate(
        PdfTable table,
        TableLayout layout,
        double startY,
        Func<int, (double Top, double Bottom, bool Empty)> edges,
        Func<string> describe)
    {
        var heights = layout.RowHeights;
        int rowCount = heights.Length;

        // A count that covers every row leaves no data rows, so those rows are laid out as ordinary rows.
        int headerRows = table.HeaderRowCount < rowCount ? table.HeaderRowCount : 0;
        bool repeatHeader = table.RepeatHeaderRows && headerRows > 0;
        double headerHeight = heights.Take(headerRows).Sum();

        var parts = new List<Split>();
        int page = 0;
        double y = startY;
        var (ownTop, bottom, _) = edges(0);
        var current = new Split(page, 0, 0, y, y, false);

        int row = 0;
        while (row < rowCount)
        {
            // The header rows only ever move together with the first data row, so they never end a page.
            bool headerBlock = row == 0 && headerRows > 0;
            double need = headerBlock ? headerHeight + heights[headerRows] : heights[row];
            int count = headerBlock ? headerRows + 1 : 1;

            if (y - need < bottom - Tolerance)
            {
                // Only the first part, or a part on a page whose room is partly taken, may stay empty.
                var (top, pageBottom, empty) = edges(page);
                // A row is checked against the empty body of the page it lands on, which page settings may size
                // differently from the table's own page.
                if (current.EndRow == current.FirstRow && page > 0 && empty)
                {
                    EnsureFits(heights, row, headerRows, headerHeight, headerBlock || current.RepeatsHeader, top - pageBottom, describe);
                    throw RowTooTall(describe(), row, heights[row], top - pageBottom);
                }

                // A part without data rows draws nothing, so header rows never stand alone on a page.
                if (current.EndRow == current.FirstRow) current = current with { RepeatsHeader = false, Bottom = current.Top };
                parts.Add(current);
                page++;
                var (nextTop, nextBottom, _) = edges(page);
                bottom = nextBottom;
                y = nextTop;
                bool repeats = repeatHeader && row > 0;
                current = new Split(page, row, row, y, y, repeats);
                if (repeats)
                {
                    y -= headerHeight;
                    current = current with { Bottom = y };
                }
                continue;
            }

            // A table placed above the body top fits rows there that no empty body can hold; those fail as on any
            // other page (WR-027), so documents without the layout API keep failing before output.
            if (page == 0) EnsureFits(heights, row, headerRows, headerHeight, headerBlock || repeatHeader, ownTop - bottom, describe);

            row += count;
            y -= need;
            current = current with { EndRow = row, Bottom = y };
        }

        parts.Add(current);
        return (parts, headerRows);
    }

    /// <summary>
    /// Returns the height of the table's header rows together with its first data row, or of its first row when it has
    /// no header rows: the least height its first part needs. Returns 0 for a table without rows.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="layout">The table's row and cell layout.</param>
    /// <returns>The height in points.</returns>
    public static double HeadHeight(PdfTable table, TableLayout layout)
    {
        var heights = layout.RowHeights;
        if (heights.Length == 0) return 0;
        int headerRows = table.HeaderRowCount < heights.Length ? table.HeaderRowCount : 0;
        return heights.Take(headerRows + 1).Sum();
    }

    #endregion

    #region Private Methods

    // Fails when the rows starting at `row` cannot fit an empty body of the given height: a row that alone is too tall
    // is named by its own message, and the header rows only when they push their data row over.
    private static void EnsureFits(double[] heights, int row, int headerRows, double headerHeight, bool withHeader, double usable, Func<string> describe)
    {
        bool headerBlock = row == 0 && headerRows > 0;
        int dataRow = headerBlock ? headerRows : row;
        for (int r = row; r <= dataRow; r++)
        {
            if (heights[r] > usable + Tolerance) throw RowTooTall(describe(), r, heights[r], usable);
        }

        if (withHeader && headerHeight + heights[dataRow] > usable + Tolerance)
            throw HeaderTooTall(describe(), headerRows, dataRow, headerHeight + heights[dataRow], usable);
    }

    private static InvalidOperationException HeaderTooTall(string table, int headerRows, int row, double height, double usable) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "The {0} header row(s) of {1} together with row index {2} need {3:0.###} pt, which is more than the {4:0.###} pt of body height between the margins, header, and footer. Use fewer or smaller header rows, smaller rows, or smaller margins.",
            headerRows,
            table,
            row,
            height,
            Math.Max(0, usable)));

    private static InvalidOperationException RowTooTall(string table, int row, double height, double usable) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "Row index {0} of {1} is {2:0.###} pt tall, which is more than the {3:0.###} pt of body height between the margins, header, and footer, so it cannot fit on any page. Use a smaller row height, an Exactly row so images scale down, or smaller margins.",
            row,
            table,
            height,
            Math.Max(0, usable)));

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// A part before its page is known: <paramref name="PageOffset"/> pages after the table's own page.
    /// </summary>
    /// <param name="PageOffset">The number of pages after the table's own page.</param>
    /// <param name="FirstRow">The index of the first row.</param>
    /// <param name="EndRow">The index after the last row.</param>
    /// <param name="Top">The y-coordinate of the part's top edge, in points.</param>
    /// <param name="Bottom">The y-coordinate where the part's last row ends, in points (the top when it draws nothing).</param>
    /// <param name="RepeatsHeader">Whether the header rows are drawn again above the rows.</param>
    internal sealed record Split(int PageOffset, int FirstRow, int EndRow, double Top, double Bottom, bool RepeatsHeader);

    #endregion
}
