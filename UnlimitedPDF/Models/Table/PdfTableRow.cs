namespace UnlimitedPDF.Models.Table;

/// <summary>
/// A table row: one cell per column, plus an optional height and height rule that override the table's defaults.
/// </summary>
public class PdfTableRow
{
    #region Fields and Constants

    // One cell per column of the parent table.
    private readonly List<PdfTableCell> _cells;
    private double? _height;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a row at <paramref name="index"/> with <paramref name="initialCols"/> empty cells.
    /// </summary>
    public PdfTableRow(int index, int initialCols)
    {
        Index = index;
        _cells = new List<PdfTableCell>(initialCols);
        for (int i = 0; i < initialCols; i++) _cells.Add(new PdfTableCell());
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the cell in column <paramref name="colIndex"/>.
    /// </summary>
    public PdfTableCell GetCell(int colIndex)
    {
        if (colIndex < 0 || colIndex >= _cells.Count) throw new ArgumentOutOfRangeException(nameof(colIndex));
        return _cells[colIndex];
    }

    /// <summary>
    /// Places <paramref name="cell"/>, with its content, in column <paramref name="colIndex"/>, adding cells first when needed.
    /// </summary>
    public void SetCell(int colIndex, PdfTableCell cell)
    {
        if (colIndex < 0) throw new ArgumentOutOfRangeException(nameof(colIndex));
        EnsureColumns(colIndex + 1);
        _cells[colIndex] = cell ?? new PdfTableCell();
    }

    #endregion

    #region Internal Methods

    // Helper to clone row cells (used when converting to fixed table)
    internal PdfTableCell CloneCellAt(int col)
    {
        var src = GetCell(col);
        return CloneCell(src);
    }

    internal void EnsureColumns(int cols)
    {
        while (_cells.Count < cols) _cells.Add(new PdfTableCell());
    }

    internal void InsertColumnAt(int colIndex)
    {
        if (colIndex < 0 || colIndex > _cells.Count) throw new ArgumentOutOfRangeException(nameof(colIndex));
        _cells.Insert(colIndex, new PdfTableCell());
    }

    internal void RemoveColumnAt(int colIndex)
    {
        if (colIndex < 0 || colIndex >= _cells.Count) throw new ArgumentOutOfRangeException(nameof(colIndex));
        _cells.RemoveAt(colIndex);
    }

    #endregion

    #region Private Methods

    private static PdfTableCell CloneCell(PdfTableCell src)
    {
        var dst = new PdfTableCell
        {
            HAlign = src.HAlign,
            VAlign = src.VAlign,
            Background = src.Background,
            Padding = src.Padding,
            RowSpan = src.RowSpan,
            ColSpan = src.ColSpan,
            LineHeightFactor = src.LineHeightFactor
        };

        // Content items are immutable, so the clone can share them.
        dst.SetContent(src.Content);

        // clone borders (shallow)
        dst.TopBorder.Width = src.TopBorder.Width;
        dst.TopBorder.Color = src.TopBorder.Color;
        dst.TopBorder.Style = src.TopBorder.Style;

        dst.BottomBorder.Width = src.BottomBorder.Width;
        dst.BottomBorder.Color = src.BottomBorder.Color;
        dst.BottomBorder.Style = src.BottomBorder.Style;

        dst.LeftBorder.Width = src.LeftBorder.Width;
        dst.LeftBorder.Color = src.LeftBorder.Color;
        dst.LeftBorder.Style = src.LeftBorder.Style;

        dst.RightBorder.Width = src.RightBorder.Width;
        dst.RightBorder.Color = src.RightBorder.Color;
        dst.RightBorder.Style = src.RightBorder.Style;

        return dst;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the row's cells, one per column.
    /// </summary>
    public IReadOnlyList<PdfTableCell> Cells => _cells;

    /// <summary>
    /// Gets or sets this row's height in points, or null to use the table's <see cref="PdfTable.RowHeight"/>. With
    /// <see cref="PdfRowHeightRule.AtLeast"/> it is a minimum.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or not finite.</exception>
    public double? Height
    {
        get => _height;
        set
        {
            if (value is double height && (double.IsNaN(height) || double.IsInfinity(height) || height <= 0))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The row height must be a finite number greater than zero.");
            _height = value;
        }
    }

    /// <summary>
    /// Gets or sets this row's height rule, or null to use the table's <see cref="PdfTable.RowHeightRule"/>.
    /// </summary>
    public PdfRowHeightRule? HeightRule { get; set; }

    /// <summary>
    /// Gets the row's position in its table.
    /// </summary>
    public int Index { get; internal set; }

    #endregion
}
