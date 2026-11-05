namespace UnlimitedPDF.Models.Table;

/// <summary>
/// A table drawn on a page: rows of cells in columns, positioned by its top-left corner.
/// </summary>
public class PdfTable
{
    #region Fields and Constants

    private readonly List<PdfTableColumn> _cols = new();
    private int _headerRowCount;
    private readonly List<PdfTableRow> _rows = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a column; every existing row gets an empty cell for it.
    /// </summary>
    /// <returns>The new column.</returns>
    public PdfTableColumn AddColumn()
    {
        var col = new PdfTableColumn(_cols.Count) { Width = null };
        _cols.Add(col);
        // ensure all rows extend
        foreach (var r in _rows) r.EnsureColumns(ColCount);
        return col;
    }

    /// <summary>
    /// Adds a row with one empty cell per column.
    /// </summary>
    /// <returns>The new row.</returns>
    public PdfTableRow AddRow()
    {
        var row = new PdfTableRow(_rows.Count, ColCount);
        _rows.Add(row);
        return row;
    }

    /// <summary>
    /// Adds rows and columns until the table has at least <paramref name="rows"/> rows and <paramref name="cols"/> columns.
    /// </summary>
    /// <param name="rows">The minimum number of rows.</param>
    /// <param name="cols">The minimum number of columns.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Adds rows and columns until the table has at least the given number of rows and columns.")]
    public void EnsureSize(int rows, int cols)
    {
        if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (cols < 0) throw new ArgumentOutOfRangeException(nameof(cols));
        while (_cols.Count < cols) AddColumn();
        while (_rows.Count < rows) AddRow();
    }

    /// <summary>
    /// Returns the cell at (<paramref name="row"/>, <paramref name="col"/>).
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the table.</exception>
    [AddedVersion("0.0.3")]
    [Description("Returns the cell at the given row and column.")]
    public PdfTableCell GetCell(int row, int col)
    {
        if (row < 0 || row >= RowCount) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= ColCount) throw new ArgumentOutOfRangeException(nameof(col));
        return _rows[row].GetCell(col);
    }

    /// <summary>
    /// Return effective column widths: use fixed widths where set, distribute remaining equally.
    /// </summary>
    public double[] GetEffectiveColumnWidths()
    {
        if (ColCount == 0) return Array.Empty<double>();
        double totalFixed = 0;
        int fixedCount = 0;
        foreach (var c in _cols)
        {
            if (c.Width.HasValue) { totalFixed += c.Width.Value; fixedCount++; }
        }
        int flexible = ColCount - fixedCount;
        double remaining = Math.Max(0, Width - totalFixed);
        double flexWidth = flexible > 0 ? remaining / flexible : 0;
        var result = new double[ColCount];
        for (int i = 0; i < ColCount; i++)
        {
            result[i] = _cols[i].Width ?? flexWidth;
        }
        return result;
    }

    /// <summary>
    /// Returns the cell at (<paramref name="row"/>, <paramref name="col"/>), adding rows and columns first when needed.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Returns the cell at the given row and column, adding rows and columns first when needed.")]
    public PdfTableCell GetOrCreateCell(int row, int col)
    {
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0) throw new ArgumentOutOfRangeException(nameof(col));
        EnsureSize(row + 1, col + 1);
        return _rows[row].GetCell(col);
    }

    /// <summary>
    /// Inserts a column at <paramref name="index"/>; every row gets an empty cell for it.
    /// </summary>
    public PdfTableColumn InsertColumn(int index)
    {
        if (index < 0 || index > _cols.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var col = new PdfTableColumn(index) { Width = null };
        _cols.Insert(index, col);
        // fix indices
        for (int i = 0; i < _cols.Count; i++) _cols[i].Index = i;
        // ensure rows update their cell lists
        foreach (var r in _rows) r.InsertColumnAt(index);
        NormalizeSpans();
        return col;
    }

    /// <summary>
    /// Inserts a row with one empty cell per column at <paramref name="index"/>.
    /// </summary>
    public PdfTableRow InsertRow(int index)
    {
        if (index < 0 || index > _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var row = new PdfTableRow(index, ColCount);
        _rows.Insert(index, row);
        // fix indices
        for (int i = 0; i < _rows.Count; i++) _rows[i].Index = i;
        NormalizeSpans();
        return row;
    }

    /// <summary>
    /// Checks if neighbour at (nr,nc) belongs to same span owner as (ownerR,ownerC).
    /// </summary>
    public bool IsSameSpanCell(int nr, int nc, int ownerR, int ownerC)
    {
        if (nr < 0 || nc < 0 || nr >= RowCount || nc >= ColCount) return false;
        for (int r = 0; r <= nr; r++)
        {
            for (int c = 0; c <= nc; c++)
            {
                var cell = _rows[r].GetCell(c);
                int rs = Math.Max(1, cell.RowSpan);
                int cs = Math.Max(1, cell.ColSpan);
                if (r + rs > nr && c + cs > nc && r <= nr && c <= nc)
                    return (r == ownerR && c == ownerC);
            }
        }
        return false;
    }

    /// <summary>
    /// Ensure RowSpan/ColSpan are in-bounds and >=1 for every cell.
    /// </summary>
    public void NormalizeSpans()
    {
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColCount; c++)
            {
                var cell = _rows[r].GetCell(c);
                if (cell.RowSpan < 1) cell.RowSpan = 1;
                if (cell.ColSpan < 1) cell.ColSpan = 1;
                if (r + cell.RowSpan > RowCount) cell.RowSpan = Math.Max(1, RowCount - r);
                if (c + cell.ColSpan > ColCount) cell.ColSpan = Math.Max(1, ColCount - c);
            }
        }
    }

    /// <summary>
    /// Removes the column at <paramref name="index"/> and its cells.
    /// </summary>
    public void RemoveColumn(int index)
    {
        if (index < 0 || index >= _cols.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _cols.RemoveAt(index);
        for (int i = 0; i < _cols.Count; i++) _cols[i].Index = i;
        foreach (var r in _rows) r.RemoveColumnAt(index);
        NormalizeSpans();
    }

    /// <summary>
    /// Removes the row at <paramref name="index"/>.
    /// </summary>
    public void RemoveRow(int index)
    {
        if (index < 0 || index >= _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _rows.RemoveAt(index);
        for (int i = 0; i < _rows.Count; i++) _rows[i].Index = i;
        NormalizeSpans();
    }

    /// <summary>
    /// Places <paramref name="cell"/>, with its content, at (<paramref name="row"/>, <paramref name="col"/>), adding rows
    /// and columns first when needed. A null cell places a new empty cell.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <param name="cell">The cell to place.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Places a cell, with its content, at the given row and column.")]
    public void SetCell(int row, int col, PdfTableCell cell)
    {
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0) throw new ArgumentOutOfRangeException(nameof(col));
        EnsureSize(row + 1, col + 1);
        _rows[row].SetCell(col, cell ?? new PdfTableCell());
        NormalizeSpans();
    }

    /// <summary>
    /// Set absolute widths for columns. Length must be <= ColCount; if shorter it's padded.
    /// </summary>
    public void SetColumnWidths(params double[] widths)
    {
        if (widths == null) throw new ArgumentNullException(nameof(widths));
        // Only ensure the column count, not the row count.
        while (_cols.Count < widths.Length)
        {
            AddColumn();
        }

        for (int i = 0; i < _cols.Count; i++)
        {
            _cols[i].Width = (i < widths.Length) ? widths[i] : (widths.LastOrDefault());
        }
    }

    /// <summary>
    /// Sets the first text item of the cell at (<paramref name="row"/>, <paramref name="col"/>), adding rows and columns
    /// first when needed. Images and other text items in the cell are untouched.
    /// </summary>
    public void SetText(int row, int col, string text)
    {
        var cell = GetOrCreateCell(row, col);
        cell.Text = text ?? "";
    }

    /// <summary>
    /// Removes trailing rows and columns whose cells are all empty. A cell holding an image, or any non-empty text,
    /// is not empty.
    /// </summary>
    [AddedVersion("0.0.3")]
    [Description("Removes trailing rows and columns whose cells are all empty.")]
    public void TrimEmptyTrailingRowsAndCols()
    {
        // trim trailing empty rows
        for (int r = RowCount - 1; r >= 0; r--)
        {
            bool any = false;
            for (int c = 0; c < ColCount; c++)
            {
                if (!_rows[r].GetCell(c).IsEmpty) { any = true; break; }
            }
            if (!any) _rows.RemoveAt(r);
            else break;
        }

        // trim trailing cols
        bool colTrim = true;
        while (colTrim && ColCount > 0)
        {
            int last = ColCount - 1;
            bool any = false;
            foreach (var row in _rows)
            {
                if (!row.GetCell(last).IsEmpty) { any = true; break; }
            }
            if (!any)
            {
                RemoveColumn(last);
            }
            else colTrim = false;
        }

        NormalizeSpans();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int ColCount => _cols.Count;

    /// <summary>
    /// Gets the columns.
    /// </summary>
    public IReadOnlyList<PdfTableColumn> Columns => _cols;

    /// <summary>
    /// Gets or sets how many leading rows are header rows. The default is 0 (no header rows). Header rows are never the
    /// last rows on a page: when they and the next row do not fit, they move to the next page together. Set
    /// <see cref="RepeatHeaderRows"/> to draw them again at the top of every continuation page.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int HeaderRowCount
    {
        get => _headerRowCount;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The header row count cannot be negative.");
            _headerRowCount = value;
        }
    }

    /// <summary>
    /// Gets or sets whether the <see cref="HeaderRowCount"/> header rows are drawn again at the top of every
    /// continuation page when the table continues on a new page. The default is false.
    /// </summary>
    public bool RepeatHeaderRows { get; set; }

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int RowCount => _rows.Count;

    /// <summary>
    /// Gets or sets the default row height in points, used by rows that set no <see cref="PdfTableRow.Height"/>.
    /// The default is 20.
    /// </summary>
    public double RowHeight { get; set; } = 20;

    /// <summary>
    /// Gets or sets the default height rule, used by rows that set no <see cref="PdfTableRow.HeightRule"/>. The default
    /// is <see cref="PdfRowHeightRule.Exactly"/>, which keeps every row at its height as before.
    /// </summary>
    public PdfRowHeightRule RowHeightRule { get; set; } = PdfRowHeightRule.Exactly;

    /// <summary>
    /// Gets the rows.
    /// </summary>
    public IReadOnlyList<PdfTableRow> Rows => _rows;

    /// <summary>
    /// Gets or sets the table width in points.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the x-coordinate of the table's left edge, in points.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the y-coordinate of the table's top edge, in points.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets how the table was laid out when its end was last queried, or null if it never was. Saving fails
    /// when the table no longer lays out the same way (WR-029).
    /// </summary>
    internal TablePlan? QueriedPlan { get; set; }

    #endregion
}
