namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Lays out and draws a table. Each row follows its height rule (WR-024): an <c>Exactly</c> row keeps its height and
/// scales images down to fit, an <c>AtLeast</c> row grows to fit its content. A cell's content is drawn top to bottom,
/// aligned by <c>HorizontalAlignment</c>, with the whole block placed by <c>VerticalAlignment</c>.
/// </summary>
internal static class PdfTableRenderer
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Computes column widths, row heights, and the size of every content item, without drawing anything.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="pageNumber">The 1-based page number, used in error messages.</param>
    /// <param name="tableNumber">The 1-based position of the table among the page's tables, used in error messages.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="InvalidOperationException">A row leaves an image no room.</exception>
    public static TableLayout Layout(PdfTable table, int pageNumber, int tableNumber)
    {
        var columnWidths = table.GetEffectiveColumnWidths();
        var rowHeights = new double[table.RowCount];
        var cells = new CellLayout[table.RowCount, table.ColumnCount];

        for (int r = 0; r < table.RowCount; r++)
        {
            var row = table.Rows[r];
            var rule = row.HeightMode ?? table.RowHeightMode;
            double height = row.Height ?? table.RowHeight;
            double rowHeight = height;

            for (int c = 0; c < table.ColumnCount; c++)
            {
                var cell = table.GetCell(r, c);
                var layout = LayoutCell(cell, columnWidths[c], rule, height, () => Describe(pageNumber, tableNumber, r, c, height));
                cells[r, c] = layout;

                if (rule == PdfRowHeightMode.AtLeast && layout.Items.Count > 0)
                    rowHeight = Math.Max(rowHeight, layout.BlockHeight + 2 * cell.Padding);
            }

            rowHeights[r] = rowHeight;
        }

        return new TableLayout(columnWidths, rowHeights, cells);
    }

    /// <summary>
    /// Returns the content stream operators that draw <paramref name="table"/>.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="pageNumber">The 1-based page number, used in error messages.</param>
    /// <param name="tableNumber">The 1-based position of the table among the page's tables, used in error messages.</param>
    /// <param name="imageName">Returns the page resource name of an image, for example "/Im1".</param>
    /// <returns>The operators.</returns>
    /// <exception cref="InvalidOperationException">A row leaves an image no room.</exception>
    public static string Render(PdfTable table, int pageNumber, int tableNumber, Func<PdfImageData, string> imageName)
    {
        var layout = Layout(table, pageNumber, tableNumber);
        return RenderAt(table, layout, table.X, table.Y, imageName);
    }

    /// <summary>
    /// Returns the content stream operators that draw every row of <paramref name="table"/> with its top-left corner at
    /// (<paramref name="x"/>, <paramref name="top"/>), without pagination (used for tables in headers and footers).
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="x">The x-coordinate of the table's left edge, in points.</param>
    /// <param name="top">The y-coordinate of the table's top edge, in points.</param>
    /// <param name="imageName">Returns the page resource name of an image, for example "/Im1".</param>
    /// <returns>The operators.</returns>
    public static string RenderAt(PdfTable table, TableLayout layout, double x, double top, Func<PdfImageData, string> imageName)
    {
        var sb = new StringBuilder();
        DrawRows(sb, table, layout, Enumerable.Range(0, table.RowCount), x, top, imageName);
        return sb.ToString();
    }

    /// <summary>
    /// Returns the content stream operators that draw one page part of a table laid out across pages (WR-027): the
    /// repeated header rows when the part repeats them, then the part's rows, from the part's top edge down.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="plan">The table's page plan.</param>
    /// <param name="part">The part to draw; one of <paramref name="plan"/>'s parts.</param>
    /// <param name="imageName">Returns the page resource name of an image, for example "/Im1".</param>
    /// <returns>The operators; empty when the part has no rows.</returns>
    public static string RenderPart(PdfTable table, TablePlan plan, TablePart part, Func<PdfImageData, string> imageName)
    {
        var rows = Enumerable.Range(part.FirstRow, part.EndRow - part.FirstRow);
        if (part.RepeatsHeader) rows = Enumerable.Range(0, plan.HeaderRowCount).Concat(rows);

        var sb = new StringBuilder();
        DrawRows(sb, table, plan.Layout, rows, part.X, part.Top, imageName);
        return sb.ToString();
    }

    #endregion

    #region Private Methods

    private static double AlignedX(PdfCell cell, double currentX, double cellWidth, double contentWidth) => cell.HorizontalAlignment switch
    {
        PdfCellHorizontalAlignment.Right => currentX + cellWidth - contentWidth - cell.Padding,
        PdfCellHorizontalAlignment.Center => currentX + (cellWidth - contentWidth) / 2,
        _ => currentX + cell.Padding
    };

    private static string Describe(int pageNumber, int tableNumber, int row, int column, double height) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "table {0} on page {1}, row index {2}, column index {3} (an Exactly row of {4} pt)",
            tableNumber,
            pageNumber,
            row,
            column,
            height);

    private static void DrawContent(
        StringBuilder sb,
        PdfCell cell,
        CellLayout layout,
        double currentX,
        double currentY,
        double cellWidth,
        double rowHeight,
        Func<PdfImageData, string> imageName)
    {
        if (layout.Items.Count == 0) return;

        // A single line of text in the middle of the row is drawn exactly as before images existed, so documents
        // without images keep their bytes (AC-14).
        if (layout.Items.Count == 1 && layout.Items[0].Item is PdfCellText single
            && single.FontSize == PdfCellText.DefaultFontSize && cell.VerticalAlignment == PdfCellVerticalAlignment.Middle)
        {
            DrawSingleMiddleLine(sb, cell, single.Text, currentX, currentY, cellWidth, rowHeight);
            return;
        }

        double padding = cell.Padding;
        double top = cell.VerticalAlignment switch
        {
            PdfCellVerticalAlignment.Top => currentY - padding,
            PdfCellVerticalAlignment.Bottom => currentY - rowHeight + padding + layout.BlockHeight,
            _ => currentY - (rowHeight - layout.BlockHeight) / 2
        };

        foreach (var item in layout.Items)
        {
            if (item.Item is PdfCellText text)
            {
                // Each line box is centered on its baseline the same way the single-line case is (center - size / 3).
                double baseline = top - item.Height / 2 - text.FontSize / 3;
                double textX = AlignedX(cell, currentX, cellWidth, GetTextWidth(text.Text, text.FontSize));
                sb.AppendLine("BT"); // Begin text
                sb.AppendLine("0 0 0 rg"); // Set text color to black
                sb.AppendLine($"/F1 {PdfNumber.Format(text.FontSize)} Tf"); // Font
                sb.AppendLine($"1 0 0 1 {PdfNumber.Format(textX)} {PdfNumber.Format(baseline)} Tm"); // Set text matrix for absolute position
                sb.AppendLine($"({PdfLiteralString.Escape(text.Text)}) Tj"); // Show text
                sb.AppendLine("ET"); // End text
            }
            else if (item.Item is PdfCellImage image)
            {
                double imageX = AlignedX(cell, currentX, cellWidth, item.Width);
                var element = new PdfImageElement(image.Image.Data, imageX, top - item.Height, item.Width, item.Height);
                sb.AppendLine(element.ToContent(imageName(image.Image.Data)));
            }

            top -= item.Height;
        }
    }

    // Draws the given rows one below the other from top. Every cell is drawn with its own background and closed border,
    // so each page part of a table looks complete.
    private static void DrawRows(StringBuilder sb, PdfTable table, TableLayout layout, IEnumerable<int> rows, double left, double top, Func<PdfImageData, string> imageName)
    {
        double currentY = top;

        foreach (int r in rows)
        {
            double rowHeight = layout.RowHeights[r];
            double currentX = left;
            for (int c = 0; c < table.ColumnCount; c++)
            {
                var cell = table.GetCell(r, c);
                double cellWidth = layout.ColumnWidths[c];

                sb.AppendLine("q"); // Save graphics state

                // Draw background
                var bg = cell.BackgroundColor;
                sb.AppendLine(FormattableString.Invariant($"{bg.R:F3} {bg.G:F3} {bg.B:F3} rg")); // Set fill color
                sb.AppendLine(FormattableString.Invariant($"{currentX} {currentY - rowHeight} {cellWidth} {rowHeight} re f")); // Draw and fill rectangle

                // Draw borders
                var border = cell.BorderLeft; // Using left as a proxy for all for now
                sb.AppendLine(FormattableString.Invariant($"{border.Color.R:F3} {border.Color.G:F3} {border.Color.B:F3} RG")); // Set stroke color
                sb.AppendLine(FormattableString.Invariant($"{border.Width} w")); // Border width
                sb.AppendLine(FormattableString.Invariant($"{currentX} {currentY - rowHeight} {cellWidth} {rowHeight} re s")); // Stroke rectangle

                DrawContent(sb, cell, layout.Cells[r, c], currentX, currentY, cellWidth, rowHeight, imageName);

                sb.AppendLine("Q"); // Restore graphics state

                currentX += cellWidth;
            }
            currentY -= rowHeight;
        }
    }

    // The original single-line text placement, kept verbatim apart from escaping and invariant numbers.
    private static void DrawSingleMiddleLine(StringBuilder sb, PdfCell cell, string text, double currentX, double currentY, double cellWidth, double rowHeight)
    {
        sb.AppendLine("BT"); // Begin text
        sb.AppendLine("0 0 0 rg"); // Set text color to black
        sb.AppendLine($"/F1 12 Tf"); // Font

        // Calculate text position based on horizontal alignment
        double textWidth = GetTextWidth(text, 12);
        double textX = AlignedX(cell, currentX, cellWidth, textWidth);

        double textY = currentY - (rowHeight / 2) - 4; // Simple middle alignment
        sb.AppendLine(FormattableString.Invariant($"1 0 0 1 {textX} {textY} Tm")); // Set text matrix for absolute position
        sb.AppendLine($"({PdfLiteralString.Escape(text)}) Tj"); // Show text
        sb.AppendLine("ET"); // End text
    }

    // A simple approximation for text width. For accurate results, you would need font metrics.
    private static double GetTextWidth(string text, double fontSize)
    {
        return text.Length * fontSize * 0.5;
    }

    private static CellLayout LayoutCell(PdfCell cell, double columnWidth, PdfRowHeightMode rule, double height, Func<string> describe)
    {
        double paddedWidth = columnWidth - 2 * cell.Padding;
        var items = new List<LaidOutItem>();
        double textHeight = 0;
        double imageHeight = 0;

        foreach (var content in cell.Content)
        {
            switch (content)
            {
                case PdfCellText text when text.Text.Length > 0:
                    double lineHeight = cell.LineHeightFactor * text.FontSize;
                    items.Add(new LaidOutItem(text, GetTextWidth(text.Text, text.FontSize), lineHeight));
                    textHeight += lineHeight;
                    break;
                case PdfCellImage image:
                    if (paddedWidth <= 0)
                        throw new InvalidOperationException($"The image in {describe()} has no width left after the cell padding. Widen the column or reduce the padding.");

                    // Scaled down to the padded width, keeping the aspect ratio; never enlarged.
                    double naturalWidth = PdfUnits.FromPixels(image.Image.PixelWidth);
                    double naturalHeight = PdfUnits.FromPixels(image.Image.PixelHeight);
                    double scale = Math.Min(1, paddedWidth / naturalWidth);
                    items.Add(new LaidOutItem(image, naturalWidth * scale, naturalHeight * scale));
                    imageHeight += naturalHeight * scale;
                    break;
            }
        }

        if (rule == PdfRowHeightMode.Exactly && imageHeight > 0)
        {
            double available = height - 2 * cell.Padding - textHeight;
            if (available <= 0)
            {
                throw new InvalidOperationException(
                    $"The image in {describe()} has no height left after padding and text. Use PdfRowHeightMode.AtLeast for the row or table, or a larger row height.");
            }

            if (imageHeight > available)
            {
                // Images share the remaining height by one common factor (FA-05).
                double factor = available / imageHeight;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Item is PdfCellImage)
                        items[i] = items[i] with { Width = items[i].Width * factor, Height = items[i].Height * factor };
                }
            }
        }

        return new CellLayout(items, items.Sum(item => item.Height));
    }

    #endregion

    #endregion
}
