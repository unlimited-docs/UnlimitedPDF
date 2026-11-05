namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// How a table is laid out across pages: its row and cell layout, the part drawn on each page, the number of header
/// rows in effect, and the y-coordinate where its last row ends.
/// </summary>
internal sealed class TablePlan
{
    #region Constructors and Finalizers

    public TablePlan(TableLayout layout, IReadOnlyList<TablePart> parts, int headerRowCount, double endY)
    {
        Layout = layout;
        Parts = parts;
        HeaderRowCount = headerRowCount;
        EndY = endY;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether <paramref name="other"/> puts the same rows on the same pages at the same positions and ends at
    /// the same y-coordinate. Changes that leave the layout the same, such as a cell color, compare equal (WR-029).
    /// </summary>
    /// <param name="other">The plan to compare with.</param>
    /// <returns>True when both plans place the table the same way.</returns>
    public bool PlacesTableLike(TablePlan other)
    {
        return EndY.Equals(other.EndY) && Parts.SequenceEqual(other.Parts);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the y-coordinate, in points, where the table's last row ends on the last part's page.
    /// </summary>
    public double EndY { get; }

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
