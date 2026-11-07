namespace UnlimitedPDF;

/// <summary>
/// A table row: one cell per column, plus an optional height and height mode that override the table's defaults.
/// </summary>
public class PdfRow
{
    #region Fields and Constants

    // One cell per column of the parent table.
    private readonly List<PdfCell> _cells;
    private double? _height;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a row at <paramref name="index"/> with <paramref name="initialColumns"/> empty cells.
    /// </summary>
    public PdfRow(int index, int initialColumns)
    {
        Index = index;
        _cells = new List<PdfCell>(initialColumns);
        for (int i = 0; i < initialColumns; i++) _cells.Add(new PdfCell());
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the cell in column <paramref name="columnIndex"/>.
    /// </summary>
    /// <param name="columnIndex">The 0-based column index.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnIndex"/> is outside the row.</exception>
    [AddedVersion("0.0.3")]
    [Description("Returns the cell in the given column.")]
    public PdfCell GetCell(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _cells.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        return _cells[columnIndex];
    }

    /// <summary>
    /// Places <paramref name="cell"/>, with its content, in column <paramref name="columnIndex"/>, adding cells first when needed.
    /// </summary>
    /// <param name="columnIndex">The 0-based column index.</param>
    /// <param name="cell">The cell; null places an empty cell.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnIndex"/> is negative.</exception>
    [AddedVersion("0.0.3")]
    [Description("Places a cell, with its content, in the given column, adding cells first when needed.")]
    public void SetCell(int columnIndex, PdfCell cell)
    {
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        EnsureColumns(columnIndex + 1);
        _cells[columnIndex] = cell ?? new PdfCell();
    }

    #endregion

    #region Internal Methods

    // Helper to clone row cells (used when converting to fixed table)
    internal PdfCell CloneCellAt(int col)
    {
        var src = GetCell(col);
        return CloneCell(src);
    }

    internal void EnsureColumns(int cols)
    {
        while (_cells.Count < cols) _cells.Add(new PdfCell());
    }

    internal void InsertColumnAt(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex > _cells.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        _cells.Insert(columnIndex, new PdfCell());
    }

    internal void RemoveColumnAt(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _cells.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        _cells.RemoveAt(columnIndex);
    }

    #endregion

    #region Private Methods

    private static PdfCell CloneCell(PdfCell src)
    {
        var dst = new PdfCell
        {
            HorizontalAlignment = src.HorizontalAlignment,
            VerticalAlignment = src.VerticalAlignment,
            BackgroundColor = src.BackgroundColor,
            Padding = src.Padding,
            RowSpan = src.RowSpan,
            ColumnSpan = src.ColumnSpan,
            LineHeightFactor = src.LineHeightFactor
        };

        // Content items are immutable, so the clone can share them.
        dst.SetContent(src.Content);

        // clone borders (shallow)
        dst.BorderTop.Width = src.BorderTop.Width;
        dst.BorderTop.Color = src.BorderTop.Color;
        dst.BorderTop.Style = src.BorderTop.Style;

        dst.BorderBottom.Width = src.BorderBottom.Width;
        dst.BorderBottom.Color = src.BorderBottom.Color;
        dst.BorderBottom.Style = src.BorderBottom.Style;

        dst.BorderLeft.Width = src.BorderLeft.Width;
        dst.BorderLeft.Color = src.BorderLeft.Color;
        dst.BorderLeft.Style = src.BorderLeft.Style;

        dst.BorderRight.Width = src.BorderRight.Width;
        dst.BorderRight.Color = src.BorderRight.Color;
        dst.BorderRight.Style = src.BorderRight.Style;

        return dst;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the row's cells, one per column.
    /// </summary>
    public IReadOnlyList<PdfCell> Cells => _cells;

    /// <summary>
    /// Gets or sets this row's height in points, or null to use the table's <see cref="PdfTable.RowHeight"/>. With
    /// <see cref="PdfRowHeightMode.AtLeast"/> it is a minimum.
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
    /// Gets or sets this row's height mode, or null to use the table's <see cref="PdfTable.RowHeightMode"/>.
    /// </summary>
    [AddedVersion("1.0.0")]
    public PdfRowHeightMode? HeightMode { get; set; }

    /// <summary>
    /// Gets the row's position in its table.
    /// </summary>
    public int Index { get; internal set; }

    #endregion
}
