using UnlimitedPDF.Models.Internal.Html.Css;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// Table layout (WR-038, AC-11, AC-16, AC-17): a cell grid with column and row spans; automatic column widths from
/// each column's narrowest and widest content, fixed and percentage widths, and the separated and collapsed border
/// models; captions; nested tables. Rows move to the next page whole, <c>thead</c> rows repeat at the top of each
/// page the table continues on, and a row taller than the body continues on the next pages cell by cell. A table
/// whose narrowest columns are still wider than the room is clipped at the body edge with a warning.
/// </summary>
internal sealed partial class HtmlLayoutEngine
{
    #region Fields and Constants

    private readonly List<(double X, double Width)> _clips = new();
    private readonly List<OpenTable> _tables = new();

    #endregion

    #region Methods

    #region Private Methods

    // Builds the grid: where each cell starts, after cells spanning down from earlier rows.
    private static (List<GridCell> Cells, int Columns) BuildGrid(TableBox table)
    {
        var cells = new List<GridCell>();
        var taken = new Dictionary<(int Row, int Column), bool>();
        int columns = 0;
        for (int r = 0; r < table.Rows.Count; r++)
        {
            int c = 0;
            foreach (var cell in table.Rows[r].Cells)
            {
                while (taken.ContainsKey((r, c))) c++;
                int rowSpan = Math.Min(cell.RowSpan, table.Rows.Count - r);
                for (int dr = 0; dr < rowSpan; dr++)
                    for (int dc = 0; dc < cell.ColumnSpan; dc++) taken[(r + dr, c + dc)] = true;
                cells.Add(new GridCell(cell, r, c, rowSpan));
                c += cell.ColumnSpan;
                columns = Math.Max(columns, c);
            }
        }
        foreach (var key in taken.Keys) columns = Math.Max(columns, key.Column + 1);
        return (cells, Math.Max(columns, table.Columns.Count));
    }

    // Column widths (border-box widths of the cells in that column) for the room available.
    private double[] ColumnWidths(TableBox table, List<GridCell> cells, int columns, double available, bool fixedWidth, out double minimumTotal)
    {
        var min = new double[columns];
        var max = new double[columns];
        var percent = new double?[columns];
        for (int c = 0; c < table.Columns.Count && c < columns; c++)
        {
            if (table.Columns[c].Width is { } w)
            {
                if (w.Unit == CssUnit.Percent) percent[c] = w.Value;
                else { min[c] = Math.Max(min[c], Resolve(w, 0)); max[c] = Math.Max(max[c], Resolve(w, 0)); }
            }
        }

        // Single-column cells first, then spanning cells share what they need beyond their columns.
        foreach (var cell in cells.OrderBy(g => g.Cell.ColumnSpan))
        {
            var (cellMin, cellMax) = IntrinsicWidths(cell.Cell.Content, preferredWidth: true);
            if (cell.Cell.Style.Width is { Unit: CssUnit.Percent } pw && cell.Cell.ColumnSpan == 1) percent[cell.Column] = Math.Max(percent[cell.Column] ?? 0, pw.Value);
            if (cell.Cell.ColumnSpan == 1)
            {
                min[cell.Column] = Math.Max(min[cell.Column], cellMin);
                max[cell.Column] = Math.Max(max[cell.Column], cellMax);
                continue;
            }
            int end = Math.Min(columns, cell.Column + cell.Cell.ColumnSpan);
            double spanMin = 0, spanMax = 0;
            for (int c = cell.Column; c < end; c++) { spanMin += min[c]; spanMax += max[c]; }
            int count = end - cell.Column;
            for (int c = cell.Column; c < end; c++)
            {
                double share = spanMax > 0 ? max[c] / spanMax : 1.0 / count;
                if (cellMin > spanMin) min[c] += (cellMin - spanMin) * share;
                if (cellMax > spanMax) max[c] += (cellMax - spanMax) * share;
            }
        }
        for (int c = 0; c < columns; c++) max[c] = Math.Max(max[c], min[c]);

        minimumTotal = min.Sum();
        var widths = new double[columns];
        double room = Math.Max(0, available);

        // Percentage columns take their share of the room first, never below their content.
        double fixedRoom = room;
        for (int c = 0; c < columns; c++)
        {
            if (percent[c] is not double p) continue;
            widths[c] = Math.Max(min[c], room * p / 100);
            fixedRoom -= widths[c];
        }
        var auto = Enumerable.Range(0, columns).Where(c => percent[c] is null).ToList();
        double autoMin = auto.Sum(c => min[c]), autoMax = auto.Sum(c => max[c]);
        fixedRoom = Math.Max(0, fixedRoom);

        if (fixedRoom >= autoMax)
        {
            // A table with a set width spreads the extra room over its columns; an automatic one stops at its content.
            double extra = fixedWidth || percent.Any(p => p is not null) ? fixedRoom - autoMax : 0;
            foreach (int c in auto) widths[c] = max[c] + (autoMax > 0 ? extra * max[c] / autoMax : extra / Math.Max(1, auto.Count));
        }
        else if (fixedRoom >= autoMin)
        {
            double t = autoMax - autoMin > 0 ? (fixedRoom - autoMin) / (autoMax - autoMin) : 0;
            foreach (int c in auto) widths[c] = min[c] + (max[c] - min[c]) * t;
        }
        else
        {
            // Narrower than the content allows: every column at its narrowest (D-016); the table overflows.
            foreach (int c in auto) widths[c] = min[c];
        }
        return widths;
    }

    // Draws the cell background and borders of one cell part.
    private static void CellDecoration(List<HtmlCommand> commands, TableRowBox row, TableCellBox cell, double left, double right, double top, double bottom, bool drawTop, bool drawBottom, bool collapse)
    {
        var background = cell.Style.BackgroundColor ?? row.Style.BackgroundColor ?? row.GroupStyle?.BackgroundColor;
        if (background is { } bg && right > left && top > bottom) commands.Add(new FillCommand(left, bottom, right - left, top - bottom, bg));
        if (!collapse)
        {
            AddBorders(commands, cell.Style.Borders, left, right, top, bottom, drawTop, drawBottom);
            return;
        }
        // Collapsed borders sit centered on the grid lines.
        var b = cell.Style.Borders;
        void Edge(CssBorderSide side, double x1, double y1, double x2, double y2)
        {
            if (side.UsedWidth > 0) commands.Add(new LineCommand(x1, y1, x2, y2, side.Width, side.Color, side.Style == HtmlBorderStyle.Double ? HtmlBorderStyle.Solid : side.Style));
        }
        if (drawTop) Edge(b[0], left, top, right, top);
        if (drawBottom) Edge(b[2], left, bottom, right, bottom);
        Edge(b[3], left, top, left, bottom);
        Edge(b[1], right, top, right, bottom);
    }

    // The content block of a cell without the cell's own box: its inherited styles, so text keeps its alignment and font.
    private static BlockBox CellContent(TableCellBox cell)
    {
        var inner = new BlockBox(ComputedStyle.Compute(new Dictionary<string, string> { ["display"] = "block" }, cell.Style), cell.Element);
        inner.Children.AddRange(cell.Content.Children);
        inner.Inlines.AddRange(cell.Content.Inlines);
        return inner;
    }

    // Insets from a cell's border box to its content: borders (half of them when collapsed) and padding.
    private static (double Left, double Right, double Top, double Bottom) CellInsets(TableCellBox cell, bool collapse)
    {
        var s = cell.Style;
        double factor = collapse ? 0.5 : 1;
        return (s.Borders[3].UsedWidth * factor + Resolve(s.Paddings[3], 0),
                s.Borders[1].UsedWidth * factor + Resolve(s.Paddings[1], 0),
                s.Borders[0].UsedWidth * factor + Resolve(s.Paddings[0], 0),
                s.Borders[2].UsedWidth * factor + Resolve(s.Paddings[2], 0));
    }

    private void LayoutTable(TableBox table, double left, double width)
    {
        var s = table.Style;
        bool collapse = s.BorderCollapse;
        double spacingH = collapse ? 0 : s.BorderSpacingHorizontal, spacingV = collapse ? 0 : s.BorderSpacingVertical;
        double bl = collapse ? 0 : s.Borders[3].UsedWidth, br = collapse ? 0 : s.Borders[1].UsedWidth;
        double bt = collapse ? 0 : s.Borders[0].UsedWidth, bb = collapse ? 0 : s.Borders[2].UsedWidth;
        double pl = collapse ? 0 : Resolve(s.Paddings[3], width), pr = collapse ? 0 : Resolve(s.Paddings[1], width);
        double pt = collapse ? 0 : Resolve(s.Paddings[0], width), pb = collapse ? 0 : Resolve(s.Paddings[2], width);
        bool autoLeft = s.Margins[3] is null, autoRight = s.Margins[1] is null;
        double ml = autoLeft ? 0 : Resolve(s.Margins[3], width), mr = autoRight ? 0 : Resolve(s.Margins[1], width);
        double mt = Resolve(s.Margins[0], width), mb = Resolve(s.Margins[2], width);

        var (cells, columns) = BuildGrid(table);
        double extras = bl + br + pl + pr + spacingH * (columns + 1);
        double outerRoom = width - ml - mr;
        double? specified = s.Width is { } sw ? Resolve(sw, width) : null;
        double target = Math.Min(specified ?? outerRoom, outerRoom);
        var widths = ColumnWidths(table, cells, columns, target - extras, specified is not null, out double minimumTotal);
        double tableWidth = widths.Sum() + extras;

        // A table is at least as wide as its captions' narrowest content (CSS 2.1, 17.4).
        double captionMin = table.Captions.Select(c => IntrinsicWidths(c).Min).DefaultIfEmpty(0).Max();
        if (captionMin > tableWidth + Tolerance && widths.Length > 0)
        {
            double grow = Math.Min(captionMin, outerRoom) - tableWidth;
            double sum = widths.Sum();
            for (int c = 0; c < widths.Length; c++) widths[c] += sum > 0 ? grow * widths[c] / sum : grow / widths.Length;
            tableWidth += grow;
        }
        double free = outerRoom - tableWidth;
        if (free > Tolerance) { if (autoLeft && autoRight) ml += free / 2; else if (autoLeft) ml += free; }
        double tableLeft = left + ml;

        // The narrowest columns are still wider than the room: clipped at the body edge (D-016).
        bool clipped = tableLeft + tableWidth > _width + Tolerance;
        if (clipped && !_measuring)
            Warn(PdfHtmlWarningKind.ContentClipped, table.Element?.Name ?? "table",
                string.Format(CultureInfo.InvariantCulture, "A table needs {0:0.##} pt but the body has {1:0.##} pt; it is clipped at the body edge.", tableLeft + tableWidth, _width));

        // Column positions (left edges of the cell border boxes).
        var columnLeft = new double[columns + 1];
        double x = tableLeft + bl + pl + spacingH;
        for (int c = 0; c < columns; c++)
        {
            columnLeft[c] = x;
            x += widths[c] + spacingH;
        }
        columnLeft[columns] = x;
        double CellRight(GridCell g) => columnLeft[Math.Min(columns, g.Column + g.Cell.ColumnSpan)] - spacingH;

        // Row heights from each cell's content; a spanning cell adds what it still needs to its last row.
        var contents = new Dictionary<GridCell, (BlockBox Box, double Width, double Height)>();
        var heights = new double[table.Rows.Count];
        for (int r = 0; r < table.Rows.Count; r++)
        {
            if (table.Rows[r].Style.Height is { Unit: not CssUnit.Percent } rh) heights[r] = Resolve(rh, 0);
        }
        foreach (var g in cells.OrderBy(g => g.RowSpan))
        {
            var (il, ir, it, ib) = CellInsets(g.Cell, collapse);
            double innerWidth = Math.Max(0, CellRight(g) - columnLeft[g.Column] - il - ir);
            var content = CellContent(g.Cell);
            double contentHeight = MeasureHeight(_model, content, innerWidth);
            double needed = contentHeight + it + ib;
            contents[g] = (content, innerWidth, needed);
            if (g.Cell.Style.Height is { Unit: not CssUnit.Percent } ch) needed = Math.Max(needed, Resolve(ch, 0));
            if (g.RowSpan == 1) heights[g.Row] = Math.Max(heights[g.Row], needed);
            else
            {
                double spanned = Enumerable.Range(g.Row, g.RowSpan).Sum(r => heights[r]) + spacingV * (g.RowSpan - 1);
                if (needed > spanned) heights[g.Row + g.RowSpan - 1] += needed - spanned;
            }
        }

        int headerRows = Math.Min(table.HeaderRowCount, table.Rows.Count);
        double headerHeight = Enumerable.Range(0, headerRows).Sum(r => heights[r] + spacingV);

        // The table starts where its header and first body row fit together.
        _pendingMargin = Collapse(_pendingMargin, mt);
        double captionHeight = table.Captions.Sum(c => MeasureHeight(_model, c, tableWidth));
        double firstRow = table.Rows.Count > headerRows ? heights[headerRows] + spacingV : 0;
        double startNeed = captionHeight + bt + pt + spacingV + headerHeight + firstRow;
        var room = Room(_page);
        if (!_measuring && !_atTop && startNeed <= room.Top - room.Bottom && _y - _pendingMargin - startNeed < room.Bottom - Tolerance) _forceBreak = true;

        foreach (var caption in table.Captions) LayoutBlock(caption, tableLeft, tableWidth);

        var open = new OpenBox(table, tableLeft, tableLeft + tableWidth);
        _open.Add(open);
        Flush(bt + pt + spacingV);
        if (clipped && !_measuring) BeginClip();
        _y -= bt + pt + spacingV;
        _atTop = false;

        // Header rows prepared once, for the first page and for every page the table continues on.
        var headerCommands = new List<HtmlCommand>();
        double headerY = 0;
        for (int r = 0; r < headerRows; r++)
        {
            DrawRow(table, cells, r, contents, heights, columnLeft, CellRight, headerY, collapse, spacingV, headerCommands);
            headerY -= heights[r] + spacingV;
        }
        var repeat = new OpenTable(headerCommands, headerHeight);

        for (int r = 0; r < table.Rows.Count; r++)
        {
            if (r == headerRows && headerRows > 0) _tables.Add(repeat);

            // Rows joined by a row span move together.
            int end = r + 1;
            for (int i = r; i < end; i++)
                foreach (var g in cells.Where(g => g.Row == i)) end = Math.Max(end, g.Row + g.RowSpan);
            if (r < headerRows) end = r + 1;
            double groupHeight = Enumerable.Range(r, end - r).Sum(i => heights[i] + spacingV);

            room = Room(_page);
            double fullRoom = Room(_page + 1).Top - Room(_page + 1).Bottom - (r >= headerRows ? headerHeight : 0);
            bool fits = _y - groupHeight >= room.Bottom - Tolerance;
            if (!fits && !_measuring && !_atTop && groupHeight <= fullRoom + Tolerance)
            {
                NextPage();
                fits = true;
            }

            if (fits || _measuring || end - r > 1 || r < headerRows)
            {
                for (int i = r; i < end; i++)
                {
                    if (i > r && !_measuring && _y - heights[i] < Room(_page).Bottom - Tolerance) NextPage();
                    DrawRow(table, cells, i, contents, heights, columnLeft, CellRight, _y, collapse, spacingV, Current.Commands);
                    _y -= heights[i] + spacingV;
                    _atTop = false;
                    Current.Bottom = Math.Min(Current.Bottom, _y);
                }
                r = end - 1;
                continue;
            }

            SplitRow(table, cells, r, contents, heights, columnLeft, CellRight, collapse, spacingV, headerHeight);
        }
        if (headerRows > 0 && headerRows < table.Rows.Count) _tables.Remove(repeat);

        Flush(pb + bb);
        _y -= pb + bb;
        if (clipped && !_measuring) EndClip();
        if (_open.Remove(open) && (s.BackgroundColor is not null || (!collapse && s.Borders.Any(b => b.UsedWidth > 0)))) Decorate(open);
        if (collapse && s.Borders.Any(b => b.UsedWidth > 0)) DrawCollapsedTableBorder(open);
        _pendingMargin = Collapse(_pendingMargin, mb);
    }

    // The outer border of a collapsed table, centered on its outline, sliced at page breaks.
    private void DrawCollapsedTableBorder(OpenBox box)
    {
        var s = box.Box.Style;
        int first = box.StartPage ?? _page;
        for (int page = first; page <= _page; page++)
        {
            double top = page == first ? box.StartY ?? _y : Room(page).Top;
            double bottom = page == _page ? _y : Math.Min(FragmentAt(page).Bottom, Room(page).Bottom);
            var fragment = FragmentAt(page);
            var b = s.Borders;
            void Edge(CssBorderSide side, double x1, double y1, double x2, double y2)
            {
                if (side.UsedWidth > 0) fragment.Commands.Add(new LineCommand(x1, y1, x2, y2, side.Width, side.Color, side.Style == HtmlBorderStyle.Double ? HtmlBorderStyle.Solid : side.Style));
            }
            if (page == first) Edge(b[0], box.Left, top, box.Right, top);
            if (page == _page) Edge(b[2], box.Left, bottom, box.Right, bottom);
            Edge(b[3], box.Left, top, box.Left, bottom);
            Edge(b[1], box.Right, top, box.Right, bottom);
        }
    }

    // Draws the cells that start in a row with their top at rowTop.
    private void DrawRow(TableBox table, List<GridCell> cells, int row, Dictionary<GridCell, (BlockBox Box, double Width, double Height)> contents,
        double[] heights, double[] columnLeft, Func<GridCell, double> cellRight, double rowTop, bool collapse, double spacingV, List<HtmlCommand> output)
    {
        foreach (var g in cells.Where(g => g.Row == row))
        {
            double height = Enumerable.Range(g.Row, g.RowSpan).Sum(r => heights[r]) + spacingV * (g.RowSpan - 1);
            double cellLeft = columnLeft[g.Column], right = cellRight(g);
            CellDecoration(output, table.Rows[row], g.Cell, cellLeft, right, rowTop, rowTop - height, drawTop: true, drawBottom: true, collapse);

            var (il, _, it, ib) = CellInsets(g.Cell, collapse);
            var (content, innerWidth, needed) = contents[g];
            double offset = g.Cell.Style.VerticalAlign switch
            {
                CssVerticalAlign.Middle => (height - needed) / 2,
                CssVerticalAlign.Bottom => height - needed,
                _ => 0
            };
            var engine = Unbounded(_model, innerWidth);
            engine.LayoutBox(content, 0, innerWidth);
            engine.Flush(0);
            if (!engine._fragments.TryGetValue(0, out var fragment)) continue;
            foreach (var command in fragment.Commands) output.Add(Translate(command, cellLeft + il, rowTop - it - Math.Max(0, offset)));
            foreach (var warning in engine._warnings) _warnings.Add(warning);
        }
    }

    // Lays out a row taller than the body: each cell continues on the next pages on its own, below repeated headers.
    private void SplitRow(TableBox table, List<GridCell> cells, int row, Dictionary<GridCell, (BlockBox Box, double Width, double Height)> contents,
        double[] heights, double[] columnLeft, Func<GridCell, double> cellRight, bool collapse, double spacingV, double headerHeight)
    {
        int startPage = _page;
        double rowTop = _y;
        var parts = new List<(GridCell Cell, IReadOnlyList<HtmlPageFragment> Fragments, double Left, double Bottom)>();
        int lastOffset = 0;
        foreach (var g in cells.Where(g => g.Row == row))
        {
            var (il, _, it, ib) = CellInsets(g.Cell, collapse);
            var (content, innerWidth, _) = contents[g];
            HtmlPageRoom Edges(int offset) => offset == 0
                ? new HtmlPageRoom(rowTop - it, Room(startPage).Bottom + ib, false)
                : new HtmlPageRoom(Room(startPage + offset).Top - headerHeight - it, Room(startPage + offset).Bottom + ib, true);
            var engine = new HtmlLayoutEngine(_model, innerWidth, Edges, _warnings, measuring: false);
            engine.LayoutBox(content, 0, innerWidth);
            engine.Flush(0);
            var fragments = engine._fragments.OrderBy(p => p.Key).Select(p => p.Value).ToList();
            lastOffset = Math.Max(lastOffset, fragments.Count == 0 ? 0 : fragments[fragments.Count - 1].PageOffset);
            parts.Add((g, fragments, columnLeft[g.Column] + il, engine._y - ib));
        }

        // Where the row ends on its last page: below the lowest cell that reaches it.
        double endBottom = parts.Where(p => p.Fragments.Count > 0 && p.Fragments[p.Fragments.Count - 1].PageOffset == lastOffset).Select(p => p.Bottom).DefaultIfEmpty(Room(startPage + lastOffset).Top - headerHeight).Min();

        for (int offset = 0; offset <= lastOffset; offset++)
        {
            if (offset > 0) NextPage();
            double top = offset == 0 ? rowTop : _y;
            double bottom = offset == lastOffset ? endBottom : Room(startPage + offset).Bottom;
            var commands = new List<HtmlCommand>();
            foreach (var part in parts)
                CellDecoration(commands, table.Rows[row], part.Cell.Cell, columnLeft[part.Cell.Column], cellRight(part.Cell), top, bottom, offset == 0, offset == lastOffset, collapse);
            foreach (var part in parts)
                foreach (var fragment in part.Fragments.Where(f => f.PageOffset == offset))
                    foreach (var command in fragment.Commands) commands.Add(Translate(command, part.Left, 0));
            Current.Commands.AddRange(commands);
            _y = bottom;
            Current.Bottom = Math.Min(Current.Bottom, _y);
        }
        _y -= spacingV;
        _atTop = false;
    }

    private (double Min, double Max) TableIntrinsicWidths(TableBox table)
    {
        var (cells, columns) = BuildGrid(table);
        var s = table.Style;
        double spacing = s.BorderCollapse ? 0 : s.BorderSpacingHorizontal * (columns + 1);
        var widths = ColumnWidths(table, cells, columns, 1e9, fixedWidth: false, out double minimum);
        return (minimum + spacing, widths.Sum() + spacing);
    }

    // Called when layout moves to a new page: open clips continue there, and repeated table headers are drawn first.
    private void OnPageStarted()
    {
        var room = Room(_page);
        foreach (var clip in _clips) Current.Commands.Add(new ClipBeginCommand(clip.X, room.Bottom - 1, clip.Width, room.Top - room.Bottom + 2));
        foreach (var table in _tables)
        {
            foreach (var command in table.Header) Current.Commands.Add(Translate(command, 0, _y));
            _y -= table.HeaderHeight;
        }
        if (_tables.Count > 0) _atTop = false;
    }

    private void BeginClip()
    {
        var room = Room(_page);
        _clips.Add((0, _width));
        Current.Commands.Add(new ClipBeginCommand(0, room.Bottom - 1, _width, room.Top - room.Bottom + 2));
    }

    private void EndClip()
    {
        if (_clips.Count == 0) return;
        _clips.RemoveAt(_clips.Count - 1);
        Current.Commands.Add(new ClipEndCommand());
    }

    private void Warn(PdfHtmlWarningKind kind, string source, string message) =>
        _warnings.Add(new PdfHtmlWarning(kind, source, message, line: null, pageNumber: _page));

    #endregion

    #endregion

    #region Nested Types

    private sealed record GridCell(TableCellBox Cell, int Row, int Column, int RowSpan);

    // A table whose header rows repeat on the pages it continues on.
    private sealed record OpenTable(List<HtmlCommand> Header, double HeaderHeight);

    #endregion
}
