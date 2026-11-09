namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// Table layout (WR-038, AC-11).
/// </summary>
internal sealed partial class HtmlLayoutEngine
{
    #region Methods

    #region Private Methods

    // Called when layout moves to a new page.
    private void OnPageStarted()
    {
    }

    // Interim layout: equal columns, each row placed whole.
    private void LayoutTable(TableBox table, double left, double width)
    {
        int columns = Math.Max(1, table.Rows.Select(r => r.Cells.Sum(c => c.ColumnSpan)).DefaultIfEmpty(1).Max());
        double columnWidth = width / columns;
        foreach (var row in table.Rows)
        {
            var cells = new List<(double X, double W, List<HtmlCommand> Commands, double Height)>();
            double x = left;
            foreach (var cell in row.Cells)
            {
                double w = columnWidth * cell.ColumnSpan;
                var engine = Unbounded(_model, w);
                engine.LayoutBox(cell.Content, 0, w);
                engine.Flush(0);
                cells.Add((x, w, engine._fragments.TryGetValue(0, out var f) ? f.Commands : new List<HtmlCommand>(), -engine._y));
                x += w;
            }
            double height = cells.Select(c => c.Height).DefaultIfEmpty(0).Max();
            Flush(height);
            double top = _y;
            foreach (var cell in cells)
                foreach (var command in cell.Commands) Current.Commands.Add(Translate(command, cell.X, top));
            _y -= height;
            _atTop = false;
            Current.Bottom = Math.Min(Current.Bottom, _y);
        }
    }

    private (double Min, double Max) TableIntrinsicWidths(TableBox table)
    {
        double min = 0, max = 0;
        foreach (var row in table.Rows)
        {
            double rowMin = 0, rowMax = 0;
            foreach (var cell in row.Cells)
            {
                var (cellMin, cellMax) = IntrinsicWidths(cell.Content);
                rowMin += cellMin;
                rowMax += cellMax;
            }
            min = Math.Max(min, rowMin);
            max = Math.Max(max, rowMax);
        }
        return (min, max);
    }

    #endregion

    #endregion
}
