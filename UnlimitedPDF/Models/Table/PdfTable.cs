namespace UnlimitedPDF;

/// <summary>
/// A table drawn on a page: rows of cells in columns, positioned by its top-left corner.
/// </summary>
public class PdfTable
{
    #region Fields and Constants

    private readonly List<PdfColumn> _cols = new();
    private int _headerRowCount;
    private readonly List<PdfRow> _rows = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a column; every existing row gets an empty cell for it.
    /// </summary>
    /// <returns>The new column.</returns>
    public PdfColumn AddColumn()
    {
        var column = new PdfColumn(_cols.Count) { Width = null };
        _cols.Add(column);
        // ensure all rows extend
        foreach (var r in _rows) r.EnsureColumns(ColumnCount);
        return column;
    }

    /// <summary>
    /// Adds a row with one empty cell per column.
    /// </summary>
    /// <returns>The new row.</returns>
    public PdfRow AddRow()
    {
        var row = new PdfRow(_rows.Count, ColumnCount);
        _rows.Add(row);
        return row;
    }

    /// <summary>
    /// Adds rows and columns until the table has at least <paramref name="rows"/> rows and <paramref name="columns"/> columns.
    /// </summary>
    /// <param name="rows">The minimum number of rows.</param>
    /// <param name="columns">The minimum number of columns.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Adds rows and columns until the table has at least the given number of rows and columns.")]
    public void EnsureSize(int rows, int columns)
    {
        if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 0) throw new ArgumentOutOfRangeException(nameof(columns));
        while (_cols.Count < columns) AddColumn();
        while (_rows.Count < rows) AddRow();
    }

    /// <summary>
    /// Returns the cell at (<paramref name="row"/>, <paramref name="column"/>). It never adds rows or columns; use
    /// <see cref="GetOrCreateCell"/> to grow the table.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="column">The column index.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the table.</exception>
    [AddedVersion("0.0.3")]
    [Description("Returns the cell at the given row and column.")]
    public PdfCell GetCell(int row, int column)
    {
        if (row < 0 || row >= RowCount) throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0 || column >= ColumnCount) throw new ArgumentOutOfRangeException(nameof(column));
        return _rows[row].GetCell(column);
    }

    /// <summary>
    /// Returns the cell at (<paramref name="row"/>, <paramref name="column"/>), adding rows and columns first when needed.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="column">The column index.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Returns the cell at the given row and column, adding rows and columns first when needed.")]
    public PdfCell GetOrCreateCell(int row, int column)
    {
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        EnsureSize(row + 1, column + 1);
        return _rows[row].GetCell(column);
    }

    /// <summary>
    /// Inserts a column at <paramref name="index"/>; every row gets an empty cell for it.
    /// </summary>
    public PdfColumn InsertColumn(int index)
    {
        if (index < 0 || index > _cols.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var column = new PdfColumn(index) { Width = null };
        _cols.Insert(index, column);
        // fix indices
        for (int i = 0; i < _cols.Count; i++) _cols[i].Index = i;
        // ensure rows update their cell lists
        foreach (var r in _rows) r.InsertColumnAt(index);
        NormalizeSpans();
        return column;
    }

    /// <summary>
    /// Inserts a row with one empty cell per column at <paramref name="index"/>.
    /// </summary>
    public PdfRow InsertRow(int index)
    {
        if (index < 0 || index > _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var row = new PdfRow(index, ColumnCount);
        _rows.Insert(index, row);
        // fix indices
        for (int i = 0; i < _rows.Count; i++) _rows[i].Index = i;
        NormalizeSpans();
        return row;
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
    /// Places <paramref name="cell"/>, with its content, at (<paramref name="row"/>, <paramref name="column"/>), adding rows
    /// and columns first when needed. A null cell places a new empty cell.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="column">The column index.</param>
    /// <param name="cell">The cell to place.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Places a cell, with its content, at the given row and column.")]
    public void SetCell(int row, int column, PdfCell cell)
    {
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        EnsureSize(row + 1, column + 1);
        _rows[row].SetCell(column, cell ?? new PdfCell());
        NormalizeSpans();
    }

    /// <summary>
    /// Set absolute widths for columns. Length must be <= ColumnCount; if shorter it's padded.
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
    /// Sets the first text item of the cell at (<paramref name="row"/>, <paramref name="column"/>), adding rows and columns
    /// first when needed. Images and other text items in the cell are untouched.
    /// </summary>
    public void SetText(int row, int column, string text)
    {
        var cell = GetOrCreateCell(row, column);
        cell.Text = text ?? "";
    }

    /// <summary>
    /// Removes trailing rows and columns whose cells are all empty. A cell holding an image, or any non-empty text,
    /// is not empty.
    /// </summary>
    [AddedVersion("1.0.0")]
    [Description("Removes trailing rows and columns whose cells are all empty.")]
    public void TrimEmptyTrailingRowsAndColumns()
    {
        // trim trailing empty rows
        for (int r = RowCount - 1; r >= 0; r--)
        {
            bool any = false;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!_rows[r].GetCell(c).IsEmpty) { any = true; break; }
            }
            if (!any) _rows.RemoveAt(r);
            else break;
        }

        // trim trailing columns
        bool colTrim = true;
        while (colTrim && ColumnCount > 0)
        {
            int last = ColumnCount - 1;
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

    #region Internal Methods

    /// <summary>
    /// Return effective column widths: use fixed widths where set, distribute remaining equally.
    /// </summary>
    internal double[] GetEffectiveColumnWidths()
    {
        if (ColumnCount == 0) return Array.Empty<double>();
        double totalFixed = 0;
        int fixedCount = 0;
        foreach (var c in _cols)
        {
            if (c.Width.HasValue) { totalFixed += c.Width.Value; fixedCount++; }
        }
        int flexible = ColumnCount - fixedCount;
        double remaining = Math.Max(0, Width - totalFixed);
        double flexWidth = flexible > 0 ? remaining / flexible : 0;
        var result = new double[ColumnCount];
        for (int i = 0; i < ColumnCount; i++)
        {
            result[i] = _cols[i].Width ?? flexWidth;
        }
        return result;
    }

    /// <summary>
    /// Checks if neighbour at (nr,nc) belongs to same span owner as (ownerR,ownerC).
    /// </summary>
    internal bool IsSameSpanCell(int nr, int nc, int ownerR, int ownerC)
    {
        if (nr < 0 || nc < 0 || nr >= RowCount || nc >= ColumnCount) return false;
        for (int r = 0; r <= nr; r++)
        {
            for (int c = 0; c <= nc; c++)
            {
                var cell = _rows[r].GetCell(c);
                int rs = Math.Max(1, cell.RowSpan);
                int cs = Math.Max(1, cell.ColumnSpan);
                if (r + rs > nr && c + cs > nc && r <= nr && c <= nc)
                    return (r == ownerR && c == ownerC);
            }
        }
        return false;
    }

    /// <summary>
    /// Ensure RowSpan/ColumnSpan are in-bounds and >=1 for every cell.
    /// </summary>
    internal void NormalizeSpans()
    {
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = _rows[r].GetCell(c);
                if (cell.RowSpan < 1) cell.RowSpan = 1;
                if (cell.ColumnSpan < 1) cell.ColumnSpan = 1;
                if (r + cell.RowSpan > RowCount) cell.RowSpan = Math.Max(1, RowCount - r);
                if (c + cell.ColumnSpan > ColumnCount) cell.ColumnSpan = Math.Max(1, ColumnCount - c);
            }
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    [AddedVersion("1.0.0")]
    public int ColumnCount => _cols.Count;

    /// <summary>
    /// Gets the columns.
    /// </summary>
    public IReadOnlyList<PdfColumn> Columns => _cols;

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
    /// Gets or sets the default row height in points, used by rows that set no <see cref="PdfRow.Height"/>.
    /// The default is 20.
    /// </summary>
    public double RowHeight { get; set; } = 20;

    /// <summary>
    /// Gets or sets the default height mode, used by rows that set no <see cref="PdfRow.HeightMode"/>. The default
    /// is <see cref="PdfRowHeightMode.Exactly"/>, which keeps every row at its height as before.
    /// </summary>
    [AddedVersion("1.0.0")]
    public PdfRowHeightMode RowHeightMode { get; set; } = PdfRowHeightMode.Exactly;

    /// <summary>
    /// Gets the rows.
    /// </summary>
    public IReadOnlyList<PdfRow> Rows => _rows;

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

    #endregion
}
