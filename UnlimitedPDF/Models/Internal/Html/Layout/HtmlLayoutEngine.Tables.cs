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
    // The least room a cell must keep on a fresh page, inside its borders and padding, to continue across pages: one
    // line of default text.
    private const double MinimumSplitRoom = ComputedStyle.DefaultFontSize * ComputedStyle.NormalLineHeight;

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

    // Column widths (border-box widths of the cells in that column) for the room available. When the columns'
    // narrowest widths are wider than fitRoom (the room before the table would be clipped), the columns held open by
    // the longest words may narrow down to their widest character; those words then break between characters, as line
    // layout breaks them, while shorter words and non-breaking runs elsewhere stay whole (WR-038).
    private double[] ColumnWidths(TableBox table, List<GridCell> cells, int columns, double available, bool fixedWidth, double fitRoom, out double minimumTotal)
    {
        var (min, max, percent) = ColumnRanges(table, cells, columns, breakWords: false);
        if (min.Sum() > fitRoom + Tolerance)
        {
            var (hard, _, _) = ColumnRanges(table, cells, columns, breakWords: true);
            // When even broken words cannot fit, the narrowest whole-word widths stay and the table is clipped.
            if (hard.Sum() <= fitRoom + Tolerance) min = LongWordColumns(min, hard, fitRoom);
        }
        for (int c = 0; c < columns; c++) max[c] = Math.Max(max[c], min[c]);
        minimumTotal = min.Sum();
        return Distribute(min, max, percent, available, fixedWidth);
    }

    // Finds the highest level at which the narrowest widths, capped at that level but never below what breaking words
    // allows (hard), fit in room. Columns wider than that level are the ones held open by long words: their narrowest
    // width becomes the broken-word width, and the room is then shared as usual, in proportion to their content, so
    // several such columns break evenly, as browsers share a table that is too wide (F-51).
    private static double[] LongWordColumns(double[] min, double[] hard, double room)
    {
        double Total(double level)
        {
            double sum = 0;
            for (int c = 0; c < min.Length; c++) sum += Math.Max(hard[c], Math.Min(min[c], level));
            return sum;
        }
        double low = 0, high = min.Length == 0 ? 0 : min.Max();
        for (int i = 0; i < 60 && high - low > 0.001; i++)
        {
            double mid = (low + high) / 2;
            if (Total(mid) <= room) low = mid; else high = mid;
        }
        return min.Select((m, c) => m > low + Tolerance ? hard[c] : m).ToArray();
    }

    // Each column's narrowest and widest widths and its percentage, from the columns' and cells' widths and content.
    private (double[] Min, double[] Max, double?[] Percent) ColumnRanges(TableBox table, List<GridCell> cells, int columns, bool breakWords)
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
            var (cellMin, cellMax) = IntrinsicWidths(cell.Cell.Content, preferredWidth: true, breakWords: breakWords);
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
        return (min, max, percent);
    }

    // Shares the room available among the columns between their narrowest and widest widths.
    private static double[] Distribute(double[] min, double[] max, double?[] percent, double available, bool fixedWidth)
    {
        int columns = min.Length;
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
        // A table narrower than its words grows past its set width (CSS 2.1, 17.5.2); long words break only when the
        // table would otherwise be wider than its container.
        var widths = ColumnWidths(table, cells, columns, target - extras, specified is not null, outerRoom - extras, out _);
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
        string clipMessage = string.Format(CultureInfo.InvariantCulture, "A table needs {0:0.##} pt but the body has {1:0.##} pt; it is clipped at the body edge.", tableLeft + tableWidth, _width);

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

        // Each cell is laid out once (F-05); a spanning cell adds what it still needs to its last row.
        var byRow = cells.ToLookup(g => g.Row);
        var layout = new TableLayoutState(table, byRow, new Dictionary<GridCell, CellContentLayout>(), new double[table.Rows.Count], columnLeft, CellRight, collapse, spacingV);
        var heights = layout.Heights;
        for (int r = 0; r < table.Rows.Count; r++)
        {
            if (table.Rows[r].Style.Height is { Unit: not CssUnit.Percent } rh) heights[r] = Resolve(rh, 0);
        }
        foreach (var g in cells.OrderBy(g => g.RowSpan))
        {
            var (il, ir, it, ib) = CellInsets(g.Cell, collapse);
            double innerWidth = Math.Max(0, CellRight(g) - columnLeft[g.Column] - il - ir);
            var content = CellContent(g.Cell);
            var laidOut = LayoutUnbounded(content, innerWidth);
            double needed = laidOut.Height + it + ib;
            layout.Contents[g] = new CellContentLayout(content, innerWidth, needed, laidOut);
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

        // Header rows repeat only when they leave room for the body (F-09): a header taller than half a page body would
        // leave too little room on each page, so it is drawn once with a warning instead.
        double freshBody = FreshBodyHeight();
        bool repeatHeader = headerRows > 0 && headerRows < table.Rows.Count && headerHeight <= freshBody / 2;

        // Where the table starts. A forced break that is still pending decides by itself.
        _pendingMargin = Collapse(_pendingMargin, mt);
        double captionHeight = table.Captions.Sum(c => Measure(c, tableWidth));
        double firstRow = table.Rows.Count > headerRows ? heights[headerRows] + spacingV : 0;
        double startNeed = captionHeight + bt + pt + spacingV + headerHeight + firstRow;
        double repeated = repeatHeader ? headerHeight : 0;
        double lead = startNeed - firstRow, firstRowTop = _y - _pendingMargin - lead;
        var room = Room(_page);
        bool mayMove = !_measuring && !_forceBreak && !_atTop;
        bool move = false;

        // The table starts on the next page when its header and first body row fit together there but not here (F-11).
        if (mayMove && startNeed <= freshBody) move = firstRowTop - firstRow < room.Bottom - Tolerance;

        // A first row taller than a page is split where the table starts. When one of its cells cannot continue and
        // would be cut here, the table starts on the next page with it, if that shows the cell whole and cuts no cell
        // that the row alone would show there (F-64, F-67, F-70); otherwise the row loop below may still move the row
        // alone. This holds while the caption and header rows fit here, so that the row would start on this page, and
        // while the page holds something above the table: a page with nothing drawn on it is not left for this (F-71).
        else if (mayMove && table.Rows.Count > headerRows && firstRow > freshBody - repeated + Tolerance && firstRowTop >= room.Bottom - Tolerance && !NothingDrawnHere())
            move = BetterOnNextPage(layout, headerRows, firstRowTop, repeated, lead);

        // The margin before the table is dropped on the next page, as before any break the layout chooses: kept, it
        // could leave the first row without room there (F-73).
        if (move)
        {
            NextPage();
            _pendingMargin = 0;
        }

        // break-inside: avoid moves a table that fits a fresh page whole to the next page, as for blocks (F-38).
        if (s.AvoidBreakInside && !_measuring && !_atTop)
        {
            double whole = captionHeight + bt + pt + spacingV + heights.Sum(h => h + spacingV) + pb + bb;
            if (whole <= freshBody && _y - _pendingMargin - whole < room.Bottom - Tolerance) _forceBreak = true;
        }

        // Whether the table starts at the top of a page: nothing but its own caption and frame stand above its rows,
        // and nothing is drawn on the page above the table (F-71).
        bool topBefore = NothingDrawnHere();
        int pageBefore = _page;
        foreach (var caption in table.Captions) LayoutBlock(caption, tableLeft, tableWidth);

        var open = new OpenBox(table, tableLeft, tableLeft + tableWidth, _page);
        _open.Add(open);
        Flush(bt + pt + spacingV);
        bool startsAtTop = topBefore || _page != pageBefore;
        if (clipped && !_measuring)
        {
            // The clip opens before the table's own background and border (F-16), and the warning names this page (F-15).
            BeginClip();
            open.InsertAt[_page] = Current.Commands.Count;
            Warn(PdfHtmlWarningKind.ContentClipped, table.Element?.Name ?? "table", clipMessage);
        }
        if (headerRows > 0 && headerRows < table.Rows.Count && !repeatHeader && !_measuring)
            Warn(PdfHtmlWarningKind.ContentClipped, "thead", "The table's header rows take more than half the page body; they are not repeated on the following pages.");
        Advance(bt + pt + spacingV);

        // Marks of list items whose content starts with this table go on its first row (F-13).
        if (_pendingMarkers.Count > 0)
        {
            foreach (var marker in _pendingMarkers) DrawMarker(marker, _y - marker.Style.FontSize, Current);
            _pendingMarkers.Clear();
        }

        // Header rows prepared once for the pages the table continues on, without marks (F-18).
        var headerCommands = new List<HtmlCommand>();
        double headerY = 0;
        for (int r = 0; r < headerRows; r++)
        {
            DrawRow(layout, r, headerY, headerCommands, report: false);
            headerY -= heights[r] + spacingV;
        }
        headerCommands.RemoveAll(c => c is MarkCommand);
        var repeat = new OpenTable(headerCommands, headerHeight);

        var degraded = new HashSet<GridCell>();
        for (int r = 0; r < table.Rows.Count; r++)
        {
            if (r == headerRows && repeatHeader) _tables.Add(repeat);

            // Rows joined by a row span move together.
            int end = r + 1;
            for (int i = r; i < end; i++)
                foreach (var g in byRow[i]) if (!degraded.Contains(g)) end = Math.Max(end, g.Row + g.RowSpan);
            double GroupHeight() => Enumerable.Range(r, end - r).Sum(i => heights[i] + spacingV);
            double rowHeader = r >= headerRows && repeatHeader ? headerHeight : 0;
            double fullRoom = FreshBodyHeight() - rowHeader;

            // A row group taller than a page body is placed row by row, its spanning cells kept to their first row
            // (F-10), so each row can move or split on its own.
            if (end - r > 1 && GroupHeight() > fullRoom + Tolerance && !_measuring)
            {
                for (int i = r; i < end; i++)
                {
                    foreach (var g in byRow[i].Where(g => g.RowSpan > 1))
                    {
                        degraded.Add(g);
                        heights[g.Row] = Math.Max(heights[g.Row], layout.Contents[g].Needed);
                    }
                }
                layout.Degraded = degraded;
                end = r + 1;
            }

            double groupHeight = GroupHeight();
            room = Room(_page);
            bool fits = _y - groupHeight >= room.Bottom - Tolerance;
            if (!fits && !_measuring && !_atTop && groupHeight <= fullRoom + Tolerance)
            {
                NextPage();
                fits = true;
            }

            if (fits || _measuring || end - r > 1)
            {
                for (int i = r; i < end; i++)
                {
                    if (i > r && !_measuring && _y - heights[i] < Room(_page).Bottom - Tolerance) NextPage();
                    DrawRow(layout, i, _y, Current.Commands, report: true);
                    _y -= heights[i] + spacingV;
                    _atTop = false;
                    Current.Bottom = Math.Min(Current.Bottom, _y);
                }
                r = end - 1;
                continue;
            }

            // A split row starts on the next page when not even one line fits here (F-11). So does a row with a cell
            // that cannot continue and would be cut here, when a fresh page shows that cell whole (F-64). A row stays
            // when the table starts at the top of this page and nothing stands above the row that the next page would
            // not repeat: moving it would leave a page with nothing but the table's start (F-67).
            bool aloneAtTop = startsAtTop && (r == 0 || (r == headerRows && repeatHeader));
            if (_y - Room(_page).Bottom < table.Style.LineHeight * 2 || (!aloneAtTop && BetterOnNextPage(layout, r, _y, rowHeader, rowHeader))) NextPage();
            SplitRow(layout, r, rowHeader);
        }
        _tables.Remove(repeat);

        Flush(pb + bb);
        Advance(pb + bb);
        if (_open.Remove(open) && (s.BackgroundColor is not null || (!collapse && s.Borders.Any(b => b.UsedWidth > 0)))) Decorate(open);
        if (collapse && s.Borders.Any(b => b.UsedWidth > 0)) DrawCollapsedTableBorder(open, clipped && !_measuring);
        if (clipped && !_measuring) EndClip();
        _pendingMargin = Collapse(_pendingMargin, mb);
    }

    // The outer border of a collapsed table, centered on its outline, sliced at page breaks. On earlier pages, whose
    // clip has already closed, a clipped table's border gets its own clip (F-16).
    private void DrawCollapsedTableBorder(OpenBox box, bool clipped)
    {
        var s = box.Box.Style;
        int first = box.StartPage ?? _page;
        for (int page = first; page <= _page; page++)
        {
            double top = page == first ? box.StartY ?? _y : Room(page).Top;
            double bottom = page == _page ? _y : Math.Min(FragmentAt(page).Bottom, Room(page).Bottom);
            var fragment = FragmentAt(page);
            bool ownClip = clipped && page < _page;
            if (ownClip) fragment.Commands.Add(new ClipBeginCommand(0, Room(page).Bottom - 1, _width, Room(page).Top - Room(page).Bottom + 2));
            var b = s.Borders;
            void Edge(CssBorderSide side, double x1, double y1, double x2, double y2)
            {
                if (side.UsedWidth > 0) fragment.Commands.Add(new LineCommand(x1, y1, x2, y2, side.Width, side.Color, side.Style == HtmlBorderStyle.Double ? HtmlBorderStyle.Solid : side.Style));
            }
            if (page == first) Edge(b[0], box.Left, top, box.Right, top);
            if (page == _page) Edge(b[2], box.Left, bottom, box.Right, bottom);
            Edge(b[3], box.Left, top, box.Left, bottom);
            Edge(b[1], box.Right, top, box.Right, bottom);
            if (ownClip) fragment.Commands.Add(new ClipEndCommand());
        }
    }

    // The number of rows a cell spans here: one when its row group was too tall to keep together (F-10).
    private static int Span(TableLayoutState layout, GridCell g) => layout.Degraded?.Contains(g) == true ? 1 : g.RowSpan;

    // Draws the cells that start in a row with their top at rowTop. The cell layouts were made once; their warnings
    // are reported the first time the cell is placed, with the page it is placed on (F-14, F-15).
    private void DrawRow(TableLayoutState layout, int row, double rowTop, List<HtmlCommand> output, bool report)
    {
        foreach (var g in layout.ByRow[row])
        {
            int span = Span(layout, g);
            double height = Enumerable.Range(g.Row, span).Sum(r => layout.Heights[r]) + layout.SpacingV * (span - 1);
            double cellLeft = layout.ColumnLeft[g.Column], right = layout.CellRight(g);
            CellDecoration(output, layout.Table.Rows[row], g.Cell, cellLeft, right, rowTop, rowTop - height, drawTop: true, drawBottom: true, layout.Collapse);

            var (il, _, it, _) = CellInsets(g.Cell, layout.Collapse);
            var content = layout.Contents[g];
            double offset = g.Cell.Style.VerticalAlign switch
            {
                CssVerticalAlign.Middle => (height - content.Needed) / 2,
                CssVerticalAlign.Bottom => height - content.Needed,
                _ => 0
            };
            foreach (var command in content.Layout.Commands) output.Add(Translate(command, cellLeft + il, rowTop - it - Math.Max(0, offset)));
            if (report) ReportOnce(content.Layout);
        }
    }

    // Whether a cell's borders and padding leave it less than a line of a page's room, so that it can never continue
    // on such a page (F-59, F-61).
    private static bool WithoutRoom(double room, double insetTop, double insetBottom) => room - insetTop - insetBottom < MinimumSplitRoom;

    // Whether content with this lowest mark draws below a bottom edge when its top is at contentTop (F-65).
    private static bool DrawsBelow(double? lowestMark, double contentTop, double bottom) => lowestMark is double lowest && contentTop + lowest < bottom - Tolerance;

    // Whether a row about to be split with its top at rowTop is better started on the next page: one of its cells that
    // cannot continue (see SplitRow) would have marks cut at the bottom of this page, and the next page shows them
    // whole below what stands above the row there (F-64). A cell that can continue from this page must still be able
    // to from the next one, which differs when pages have bodies of their own (F-66). When more than the repeated
    // header stands above the row there, because the table's start moves with it, no cell may be cut that the row
    // alone would show there (F-70).
    private bool BetterOnNextPage(TableLayoutState layout, int row, double rowTop, double repeatedHeader, double above)
    {
        double next = FreshBodyHeight(), after = FreshBodyHeight(2);
        bool better = false;
        foreach (var g in layout.ByRow[row])
        {
            var (_, _, it, ib) = CellInsets(g.Cell, layout.Collapse);
            if (LowestMark(layout.Contents[g].Layout.Commands) is not double lowest) continue;
            if (!WithoutRoom(next - repeatedHeader, it, ib))
            {
                if (WithoutRoom(after - repeatedHeader, it, ib)) return false;
                continue;
            }
            double depth = it - lowest;
            if (above + depth > next + Tolerance)
            {
                if (repeatedHeader + depth <= next + Tolerance) return false;
                continue;
            }
            if (DrawsBelow(lowest, rowTop - it, Room(_page).Bottom)) better = true;
        }
        return better;
    }

    // Lays out a row taller than the body: each cell continues on the next pages on its own, below repeated headers.
    // A cell's borders and padding are kept on every page it continues on. A cell they leave less than a line of room
    // on a fresh page can never continue, whatever it holds: its content is drawn once, on the row's first page, and
    // clipped at the body bottom when it draws past it (F-59, F-61, F-65). The other cells split as usual, and the row
    // keeps its height (F-62).
    private void SplitRow(TableLayoutState layout, int row, double headerHeight)
    {
        int startPage = _page;
        double rowTop = _y;
        double fresh = FreshBodyHeight() - headerHeight;
        double pageTop = Room(startPage).Top, pageBottom = Room(startPage).Bottom;

        // Per cell: its fragments by page offset, counted from the row's page (F-63), and the last of them.
        var parts = new List<(GridCell Cell, Dictionary<int, HtmlPageFragment> Fragments, int Last, double Left, double Bottom)>();
        int lastOffset = 0;
        foreach (var g in layout.ByRow[row])
        {
            var (il, _, it, ib) = CellInsets(g.Cell, layout.Collapse);
            var content = layout.Contents[g];
            double left = layout.ColumnLeft[g.Column] + il;
            if (WithoutRoom(fresh, it, ib))
            {
                var once = new Dictionary<int, HtmlPageFragment>();
                double contentTop = rowTop - it;
                if (content.Layout.Commands.Count > 0)
                {
                    var fragment = new HtmlPageFragment(0, contentTop);
                    bool cut = DrawsBelow(LowestMark(content.Layout.Commands), contentTop, pageBottom);
                    if (cut)
                    {
                        Warn(PdfHtmlWarningKind.ContentClipped, g.Cell.Element?.Name ?? "td",
                            "A cell's borders and padding leave no room for its content on a page; the content is not continued and is clipped at the bottom of the page.");

                        // The body of this page, in the cell's own coordinates.
                        fragment.Commands.Add(new ClipBeginCommand(-left, pageBottom - 1, _width, pageTop - pageBottom + 2));
                    }
                    foreach (var command in content.Layout.Commands) fragment.Commands.Add(Translate(command, 0, contentTop));
                    if (cut) fragment.Commands.Add(new ClipEndCommand());
                    once[0] = fragment;
                    ReportOnce(content.Layout);
                }
                parts.Add((g, once, once.Count - 1, left, Math.Max(rowTop - content.Needed, pageBottom)));
                continue;
            }

            HtmlPageRoom Edges(int offset) => offset == 0
                ? new HtmlPageRoom(rowTop - it, pageBottom + ib, false)
                : new HtmlPageRoom(Room(startPage + offset).Top - headerHeight - it, Room(startPage + offset).Bottom + ib, true);
            var cellWarnings = new List<PdfHtmlWarning>();
            // The cell's pages count from the row's page, so it gets what is left of the page limit (F-48).
            var engine = new HtmlLayoutEngine(_model, content.Width, Edges, cellWarnings, measuring: false, _cache, _lastPage - startPage);
            engine.LayoutBox(content.Box, 0, content.Width);
            engine.Flush(0);
            var fragments = engine._fragments;
            int last = fragments.Count == 0 ? -1 : fragments.Keys.Max();
            lastOffset = Math.Max(lastOffset, last);
            parts.Add((g, fragments, last, left, engine._y - ib));

            // The cell's own page offsets count from the row's first page (F-15).
            foreach (var w in cellWarnings)
                _warnings.Add(new PdfHtmlWarning(w.Kind, w.Source, w.Message, w.Line, startPage + (w.PageNumber ?? 0)));
        }

        // Where the row ends on its last page: below the lowest cell that reaches it.
        double endBottom = parts.Where(p => p.Last == lastOffset).Select(p => p.Bottom).DefaultIfEmpty(Room(startPage + lastOffset).Top - headerHeight).Min();

        // Each part's bottom. A set row or cell height taller than the content continues below it, onto further pages
        // when needed, so the row keeps its height (F-39).
        double PartTop(int offset) => offset == 0 ? rowTop : Room(startPage + offset).Top - headerHeight;
        var bottoms = Enumerable.Range(0, lastOffset + 1).Select(o => o == lastOffset ? endBottom : Room(startPage + o).Bottom).ToList();
        double remaining = layout.Heights[row] - bottoms.Select((bottom, o) => PartTop(o) - bottom).Sum();
        while (remaining > Tolerance)
        {
            int o = bottoms.Count - 1;
            double floor = Room(startPage + o).Bottom;
            double extend = Math.Min(remaining, bottoms[o] - floor);
            bottoms[o] -= extend;
            remaining -= extend;
            if (remaining <= Tolerance || (_lastPage is int last && startPage + o + 1 > last)) break;
            if (PartTop(o + 1) - Room(startPage + o + 1).Bottom < MinimumPageRoom) break;
            bottoms.Add(PartTop(o + 1));
        }
        lastOffset = bottoms.Count - 1;

        for (int offset = 0; offset <= lastOffset; offset++)
        {
            if (offset > 0) NextPage();
            double top = offset == 0 ? rowTop : _y;
            double bottom = bottoms[offset];
            var commands = new List<HtmlCommand>();
            foreach (var part in parts)
                CellDecoration(commands, layout.Table.Rows[row], part.Cell.Cell, layout.ColumnLeft[part.Cell.Column], layout.CellRight(part.Cell), top, bottom, offset == 0, offset == lastOffset, layout.Collapse);
            foreach (var part in parts)
            {
                if (!part.Fragments.TryGetValue(offset, out var fragment)) continue;
                foreach (var command in fragment.Commands) commands.Add(Translate(command, part.Left, 0));
            }
            Current.Commands.AddRange(commands);
            _y = bottom;
            Current.Bottom = Math.Min(Current.Bottom, _y);
        }
        _y -= layout.SpacingV;
        _atTop = false;
    }

    private (double Min, double Max) TableIntrinsicWidths(TableBox table, bool breakWords)
    {
        var (cells, columns) = BuildGrid(table);
        var s = table.Style;
        double spacing = s.BorderCollapse ? 0 : s.BorderSpacingHorizontal * (columns + 1);
        var (min, max, percent) = ColumnRanges(table, cells, columns, breakWords);
        var widths = Distribute(min, max, percent, 1e9, fixedWidth: false);
        double minimum = min.Sum();
        return (minimum + spacing, widths.Sum() + spacing);
    }

    // On a new page: open clips continue there first, so decorations recorded after them are clipped too.
    private void ReopenClips()
    {
        var room = Room(_page);
        foreach (var clip in _clips) Current.Commands.Add(new ClipBeginCommand(clip.X, room.Bottom - 1, clip.Width, room.Top - room.Bottom + 2));
    }

    // On a new page: the header rows of the tables that continue here are drawn at the top.
    private void DrawRepeatedHeaders()
    {
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

    // A cell's content block, its width, the height it needs with insets, and its one layout.
    private sealed record CellContentLayout(BlockBox Box, double Width, double Needed, CachedLayout Layout);

    // A table whose header rows repeat on the pages it continues on.
    private sealed record OpenTable(List<HtmlCommand> Header, double HeaderHeight);

    // What drawing a table's rows needs.
    private sealed class TableLayoutState
    {
        public TableLayoutState(TableBox table, ILookup<int, GridCell> byRow, Dictionary<GridCell, CellContentLayout> contents, double[] heights,
            double[] columnLeft, Func<GridCell, double> cellRight, bool collapse, double spacingV)
        {
            Table = table;
            ByRow = byRow;
            Contents = contents;
            Heights = heights;
            ColumnLeft = columnLeft;
            CellRight = cellRight;
            Collapse = collapse;
            SpacingV = spacingV;
        }

        public ILookup<int, GridCell> ByRow { get; }

        public Func<GridCell, double> CellRight { get; }

        public bool Collapse { get; }

        public double[] ColumnLeft { get; }

        public Dictionary<GridCell, CellContentLayout> Contents { get; }

        public HashSet<GridCell>? Degraded { get; set; }

        public double[] Heights { get; }

        public double SpacingV { get; }

        public TableBox Table { get; }
    }

    #endregion
}
