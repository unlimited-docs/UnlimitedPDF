namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// How a table is laid out across pages: its row and cell layout, the part drawn on each page, and the number of
/// header rows in effect.
/// </summary>
internal sealed class TablePlan
{
    #region Constructors and Finalizers

    public TablePlan(TableLayout layout, IReadOnlyList<TablePart> parts, int headerRowCount)
    {
        Layout = layout;
        Parts = parts;
        HeaderRowCount = headerRowCount;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of leading header rows in effect (0 when no data rows follow them).
    /// </summary>
    public int HeaderRowCount { get; }

    /// <summary>
    /// Gets the column widths, row heights, and cell content layout.
    /// </summary>
    public TableLayout Layout { get; }

    /// <summary>
    /// Gets the table's parts in page order. The first part is on the table's own page and may have no rows.
    /// </summary>
    public IReadOnlyList<TablePart> Parts { get; }

    #endregion
}
