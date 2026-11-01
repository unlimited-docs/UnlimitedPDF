namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// A table's computed layout.
/// </summary>
/// <param name="ColumnWidths">The width of each column in points.</param>
/// <param name="RowHeights">The height of each row in points, after growth.</param>
/// <param name="Cells">The layout of each cell, by row and column.</param>
internal sealed record TableLayout(double[] ColumnWidths, double[] RowHeights, CellLayout[,] Cells);
