using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Splits a table's rows into page parts (WR-027). Rows are never split; a row that would pass the bottom margin moves
/// to the next page, where the table resumes at the top margin below the repeated header rows. Header rows are never
/// the last rows on a page. Pages are counted from the table's own page (0).
/// </summary>
internal static class TablePaginator
{
    #region Fields and Constants

    // A row that ends within this distance below the bottom margin still fits, so rounding never moves a row.
    private const double Tolerance = 1e-6;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Splits <paramref name="table"/> into parts.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="layout">The table's row and cell layout.</param>
    /// <param name="pageHeight">The height of the table's page and its continuation pages, in points.</param>
    /// <param name="topMargin">The document's top margin, in points.</param>
    /// <param name="bottomMargin">The document's bottom margin, in points.</param>
    /// <param name="describe">Returns the table's description for error messages, for example "table 1 on page 2".</param>
    /// <returns>The parts in page order, the header rows in effect, and the y-coordinate where the last row ends.</returns>
    /// <exception cref="InvalidOperationException">A row, or the header rows with a data row, cannot fit on an empty page.</exception>
    public static (List<Split> Parts, int HeaderRowCount, double EndY) Paginate(
        PdfTable table,
        TableLayout layout,
        double pageHeight,
        double topMargin,
        double bottomMargin,
        Func<string> describe)
    {
        var heights = layout.RowHeights;
        int rowCount = heights.Length;

        // A count that covers every row leaves no data rows, so those rows are laid out as ordinary rows.
        int headerRows = table.HeaderRowCount < rowCount ? table.HeaderRowCount : 0;
        bool repeatHeader = table.RepeatHeaderRows && headerRows > 0;
        double headerHeight = heights.Take(headerRows).Sum();
        double usable = pageHeight - topMargin - bottomMargin;

        EnsureEveryRowFits(heights, headerRows, headerHeight, repeatHeader, usable, describe);

        var parts = new List<Split>();
        int page = 0;
        double y = table.Y;
        var current = new Split(page, 0, 0, y, false);

        int row = 0;
        while (row < rowCount)
        {
            // The header rows only ever move together with the first data row, so they never end a page.
            bool headerBlock = row == 0 && headerRows > 0;
            double need = headerBlock ? headerHeight + heights[headerRows] : heights[row];
            int count = headerBlock ? headerRows + 1 : 1;

            if (y - need < bottomMargin - Tolerance)
            {
                // Only the first part may stay empty: its page may have no room left for the first row.
                if (current.EndRow == current.FirstRow && page > 0)
                    throw RowTooTall(describe(), row, heights[row], usable);

                parts.Add(current);
                page++;
                y = pageHeight - topMargin;
                bool repeats = repeatHeader && row > 0;
                current = new Split(page, row, row, y, repeats);
                if (repeats) y -= headerHeight;
                continue;
            }

            row += count;
            y -= need;
            current = current with { EndRow = row };
        }

        parts.Add(current);
        return (parts, headerRows, y);
    }

    #endregion

    #region Private Methods

    private static void EnsureEveryRowFits(double[] heights, int headerRows, double headerHeight, bool repeatHeader, double usable, Func<string> describe)
    {
        for (int row = 0; row < heights.Length; row++)
        {
            if (heights[row] > usable + Tolerance)
                throw RowTooTall(describe(), row, heights[row], usable);
        }

        if (headerRows == 0) return;

        // The header rows travel with the first data row, and with every data row when they repeat.
        int checkedEnd = repeatHeader ? heights.Length : headerRows + 1;
        for (int row = headerRows; row < checkedEnd; row++)
        {
            if (headerHeight + heights[row] > usable + Tolerance)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The {0} header row(s) of {1} together with row index {2} need {3:0.###} pt, which is more than the {4:0.###} pt between the top and bottom margins. Use fewer or smaller header rows, smaller rows, or smaller margins.",
                    headerRows,
                    describe(),
                    row,
                    headerHeight + heights[row],
                    Math.Max(0, usable)));
            }
        }
    }

    private static InvalidOperationException RowTooTall(string table, int row, double height, double usable) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "Row index {0} of {1} is {2:0.###} pt tall, which is more than the {3:0.###} pt between the top and bottom margins, so it cannot fit on any page. Use a smaller row height, an Exactly row so images scale down, or smaller margins.",
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
    /// <param name="RepeatsHeader">Whether the header rows are drawn again above the rows.</param>
    internal sealed record Split(int PageOffset, int FirstRow, int EndRow, double Top, bool RepeatsHeader);

    #endregion
}
