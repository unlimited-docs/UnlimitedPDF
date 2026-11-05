using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// One page the planner starts from: a page the caller added with <c>AddPage</c>, or the page the library adds for
/// flow content placed before the first caller page. Its continuation pages follow it.
/// </summary>
/// <param name="Builder">The caller page's builder, or null for the library's first flow page.</param>
/// <param name="Size">The page size; the pages that continue it have the same size.</param>
/// <param name="Tables">The page-coordinate tables on the page, in the order they were added.</param>
/// <param name="FlowStart">The index of the first flow item added after the page, which starts on it.</param>
internal sealed record PlanRoot(PdfPageBuilder? Builder, PdfPageSize Size, IReadOnlyList<PdfTable> Tables, int FlowStart);

/// <summary>
/// Lays out every saved page in reading order (WR-027, WR-030, WR-033): each root page, then the pages that continue
/// it, which carry its page-coordinate tables and its body flow. The planner works incrementally, so
/// <c>Body.Remaining</c> can ask where the next flow item lands while content is still being added, and the same code
/// lays out the whole document at save. Every layout error is thrown here, before any page object or byte exists.
/// </summary>
internal sealed class PagePlanner
{
    #region Fields and Constants

    // Kept items wait here until the item they keep with arrives (WR-033).
    private readonly List<FlowItem> _pending = new();
    private readonly List<PlannedPage> _pages = new();
    private readonly Dictionary<PdfTable, TablePlan> _plans = new();
    private readonly bool _checkBody;
    private readonly Func<int, PdfPageSize, PageGeometry> _geometry;
    private Cursor _cursor;
    private int _nextItem;
    private int _nextNumber = 1;
    private FlowTable? _pendingTable;
    private PlanRoot? _root;
    private int _rootIndex = -1;
    private List<PlannedPage> _slots = new();

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates a planner.
    /// </summary>
    /// <param name="geometry">Returns the geometry of the page with the given 1-based number and size.</param>
    /// <param name="checkBody">Whether every page must leave room for a body (only when the layout API is used, so
    /// documents that do not use it keep their behavior).</param>
    public PagePlanner(Func<int, PdfPageSize, PageGeometry> geometry, bool checkBody)
    {
        _geometry = geometry;
        _checkBody = checkBody;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Plans every root page up to <paramref name="rootIndex"/> and every flow item before <paramref name="flowCount"/>.
    /// </summary>
    /// <param name="roots">The root pages in reading order; later calls may only append to them.</param>
    /// <param name="flow">The flow items; later calls may only append to them.</param>
    /// <param name="flowCount">The number of flow items to plan.</param>
    /// <param name="rootIndex">The index of the last root page to enter.</param>
    public void Advance(IReadOnlyList<PlanRoot> roots, IReadOnlyList<FlowItem> flow, int flowCount, int rootIndex)
    {
        if (roots.Count == 0) return;
        if (_root is null) EnterRoot(roots, 0);

        while (true)
        {
            int segmentEnd = _rootIndex + 1 < roots.Count ? roots[_rootIndex + 1].FlowStart : int.MaxValue;
            int end = Math.Min(flowCount, segmentEnd);
            while (_nextItem < end) Place(flow[_nextItem++]);

            if (_rootIndex >= rootIndex || _rootIndex + 1 >= roots.Count) return;
            FinishRoot();
            EnterRoot(roots, _rootIndex + 1);
        }
    }

    /// <summary>
    /// Plans everything that is left and returns the saved pages in reading order.
    /// </summary>
    /// <param name="roots">The root pages in reading order.</param>
    /// <param name="flow">The flow items.</param>
    /// <returns>The pages and every table's plan.</returns>
    public (IReadOnlyList<PlannedPage> Pages, IReadOnlyDictionary<PdfTable, TablePlan> Plans) Finish(IReadOnlyList<PlanRoot> roots, IReadOnlyList<FlowItem> flow)
    {
        Advance(roots, flow, flow.Count, roots.Count - 1);
        if (_root is not null) FinishRoot();
        return (_pages, _plans);
    }

    /// <summary>
    /// Returns where the next flow item would land: its page number and the height from the cursor to that page's body
    /// bottom. Kept items still waiting for their next item count as placed where they are (WR-033).
    /// </summary>
    /// <param name="emptyDocumentGeometry">The geometry of page 1 when no root page exists yet.</param>
    /// <returns>The page number and remaining height, in points.</returns>
    public (int PageNumber, double Remaining) Probe(PageGeometry emptyDocumentGeometry)
    {
        if (_root is null) return (1, Math.Max(0, emptyDocumentGeometry.BodyHeight));

        var cursor = _cursor;
        foreach (var item in _pending)
            cursor = FitBox(cursor, item.Spacing, MeasureSimple(item), item.Description, out _);

        return (_slots[0].Number + cursor.Slot, Math.Max(0, cursor.Y - Geometry(cursor.Slot).BodyBottom));
    }

    /// <summary>
    /// Returns the layout fingerprint of a flow item: the measured heights its placement depends on.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The fingerprint.</returns>
    public static string Signature(FlowItem item) => item switch
    {
        FlowTable table => string.Join(",", PdfTableRenderer.Layout(table.Table, 0, 0).RowHeights.Select(PdfNumber.Format)),
        FlowBlock block => "[" + string.Join(";", block.Children.Select(Signature)) + "]",
        _ => PdfNumber.Format(MeasureSimple(item))
    };

    #endregion

    #region Private Methods

    private static InvalidOperationException NoBody(int number, PageGeometry geometry)
    {
        double topGap = geometry.HeaderTop - geometry.HeaderHeight - geometry.BodyTop;
        double bottomGap = geometry.BodyBottom - geometry.FooterTop;
        if (geometry.ContentWidth <= 0)
        {
            return new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "Page {0} has no body width: the left margin {1:0.###} and right margin {2:0.###} leave {3:0.###} pt of the {4:0.###} pt page width. Use smaller side margins.",
                number,
                geometry.Left,
                geometry.Width - geometry.Right,
                geometry.ContentWidth,
                geometry.Width));
        }

        return new InvalidOperationException(string.Format(
            CultureInfo.InvariantCulture,
            "Page {0} has no body height: the top margin {1:0.###}, header {2:0.###}, gaps {3:0.###} and {4:0.###}, footer {5:0.###}, and bottom margin {6:0.###} leave {7:0.###} pt of the {8:0.###} pt page height. Use smaller margins, bands, or gaps.",
            number,
            geometry.Height - geometry.HeaderTop,
            geometry.HeaderHeight,
            topGap,
            bottomGap,
            geometry.FooterHeight,
            geometry.FooterTop - geometry.FooterHeight,
            geometry.BodyHeight,
            geometry.Height));
    }

    private static InvalidOperationException Oversize(string description, double height, double body) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "{0} is {1:0.###} pt tall, which is more than the {2:0.###} pt body height of an empty page, so it cannot fit on any page. Make it smaller, split it, or use smaller margins, bands, or gaps.",
            char.ToUpperInvariant(description[0]) + description.Substring(1),
            height,
            Math.Max(0, body)));

    // Heights of items that never split. A table here is one inside a block.
    private static double MeasureSimple(FlowItem item) => item switch
    {
        FlowText text => LayoutDrawing.LineHeight(text.FontSize),
        FlowImage image => ImageBox.Compute(image.Image, image.Width, image.Height, image.Options).BoxHeight,
        FlowRectangle rectangle => rectangle.Height,
        FlowTable table => PdfTableRenderer.Layout(table.Table, 0, 0).RowHeights.Sum(),
        FlowBlock block => block.Children.Select((child, i) => (i == 0 ? 0 : child.Spacing) + MeasureSimple(child)).Sum(),
        _ => 0
    };

    private bool Carried(int slot) => slot < _slots.Count && _slots[slot].CarriedBottom is not null;

    private void EnterRoot(IReadOnlyList<PlanRoot> roots, int index)
    {
        _rootIndex = index;
        _root = roots[index];
        _slots = new List<PlannedPage>();
        Slot(0);

        PlanPageTables();

        _cursor = Fresh(0);
        _nextItem = _root.FlowStart;
    }

    // Places the waiting kept items without the keep rule: nothing follows them on this page sequence.
    private void FinishPending()
    {
        foreach (var item in _pending) PlaceBox(item);
        _pending.Clear();
        _pendingTable = null;
    }

    private void FinishRoot()
    {
        FinishPending();
        _pages.AddRange(_slots);
        _nextNumber += _slots.Count;
        _root = null;
    }

    // The cursor at the top of a page's free room: the body top, or below the rows carried onto it (WR-033).
    private Cursor Fresh(int slot)
    {
        bool carried = Carried(slot);
        double top = carried ? Math.Min(Geometry(slot).BodyTop, _slots[slot].CarriedBottom!.Value) : Geometry(slot).BodyTop;
        return new Cursor(slot, top, !carried, !carried);
    }

    // Moves the cursor until an item of the given height fits, dropping the spacing at a page's top.
    private Cursor FitBox(Cursor cursor, double spacing, double height, string description, out double top)
    {
        while (true)
        {
            double gap = cursor.AtTop ? 0 : spacing;
            if (cursor.Y - gap - height >= Geometry(cursor.Slot).BodyBottom - TablePaginator.Tolerance)
            {
                top = cursor.Y - gap;
                return new Cursor(cursor.Slot, top - height, false, false);
            }

            if (cursor.EmptyBody) throw Oversize(description, height, Geometry(cursor.Slot).BodyHeight);
            cursor = Fresh(cursor.Slot + 1);
        }
    }

    private PageGeometry Geometry(int slot) =>
        slot < _slots.Count ? _slots[slot].Geometry : _geometry(_nextNumber + slot, _root!.Size);

    // The least height an item needs on the page where it starts: a table's header rows and first data row.
    private double HeadHeight(FlowItem item) => item is FlowTable table
        ? TablePaginator.HeadHeight(table.Table, PdfTableRenderer.Layout(table.Table, 0, 0))
        : MeasureSimple(item);

    // Keeps the last data row of a kept table with the item that follows it (WR-033): when the next item's head does
    // not fit below the table, that row moves to the next page, with the repeated header rows above it.
    private void KeepTableWithNext(FlowTable kept, FlowItem next)
    {
        var plan = _plans[kept.Table];
        var parts = plan.Parts.ToList();
        var last = parts[parts.Count - 1];
        int slot = last.PageIndex - _pages.Count;
        double need = next.Spacing + HeadHeight(next);
        if (last.Bottom - need >= Geometry(slot).BodyBottom - TablePaginator.Tolerance) return;

        var heights = plan.Layout.RowHeights;
        int headerRows = plan.HeaderRowCount;
        int moveFrom = last.EndRow - 1;
        if (moveFrom < last.FirstRow) return;
        // Header rows never stay behind without a data row, so the whole first part moves with them.
        if (last.FirstRow == 0 && moveFrom <= headerRows) moveFrom = 0;

        double moved = heights.Skip(moveFrom).Take(last.EndRow - moveFrom).Sum();
        var shortened = last with { EndRow = moveFrom, Bottom = last.Bottom + moved };
        if (!shortened.HasRows) shortened = shortened with { RepeatsHeader = false, Bottom = shortened.Top };

        var target = Fresh(slot + 1);
        Slot(slot + 1);
        bool repeats = kept.Table.RepeatHeaderRows && headerRows > 0 && moveFrom > 0;
        double headerHeight = repeats ? heights.Take(headerRows).Sum() : 0;
        double gap = target.AtTop || moveFrom > 0 ? 0 : kept.Spacing;
        double top = target.Y - gap;
        var movedPart = new TablePart(_pages.Count + slot + 1, moveFrom, last.EndRow, top, top - headerHeight - moved, Geometry(slot + 1).Left + kept.X, repeats);

        parts[parts.Count - 1] = shortened;
        parts.Add(movedPart);
        _plans[kept.Table] = new TablePlan(plan.Layout, parts, headerRows);

        var placements = _slots[slot].Flow;
        int index = placements.FindIndex(p => p.Item == kept && p.Part == last);
        if (shortened.HasRows) placements[index] = placements[index] with { Part = shortened };
        else placements.RemoveAt(index);
        _slots[slot + 1].Flow.Add(new FlowPlacement(kept, Geometry(slot + 1).Left, Geometry(slot + 1).Right, top, movedPart));
        _cursor = new Cursor(slot + 1, movedPart.Bottom, false, false);
    }

    // Moves the cursor to a fresh page when the kept run and the item's head do not fit together here.
    private void KeepRunWith(FlowItem next)
    {
        if (_pending.Count == 0) return;

        double Need(Cursor cursor)
        {
            double total = 0;
            for (int i = 0; i < _pending.Count; i++)
                total += (i == 0 && cursor.AtTop ? 0 : _pending[i].Spacing) + MeasureSimple(_pending[i]);
            return total + next.Spacing + HeadHeight(next);
        }

        var cursor = _cursor;
        while (cursor.Y - Need(cursor) < Geometry(cursor.Slot).BodyBottom - TablePaginator.Tolerance)
        {
            if (cursor.EmptyBody)
            {
                string description = string.Format(
                    CultureInfo.InvariantCulture,
                    "the kept-together run of flow items {0} to {1}",
                    _pending[0].Index + 1,
                    next.Index + 1);
                throw Oversize(description, Need(cursor), Geometry(cursor.Slot).BodyHeight);
            }
            cursor = Fresh(cursor.Slot + 1);
        }

        _cursor = cursor;
    }

    private void Place(FlowItem item)
    {
        if (item is FlowBreak)
        {
            FinishPending();
            _cursor = Fresh(_cursor.Slot + 1);
            Slot(_cursor.Slot);
            return;
        }

        if (_pendingTable is not null)
        {
            KeepTableWithNext(_pendingTable, item);
            _pendingTable = null;
        }

        if (item.KeepWithNext && item is not FlowTable)
        {
            _pending.Add(item);
            return;
        }

        KeepRunWith(item);
        foreach (var kept in _pending) PlaceBox(kept);
        _pending.Clear();

        if (item is FlowTable table)
        {
            PlaceTable(table);
            if (table.KeepWithNext) _pendingTable = table;
        }
        else
        {
            PlaceBox(item);
        }
    }

    private void PlaceBox(FlowItem item)
    {
        double height = MeasureSimple(item);
        _cursor = FitBox(_cursor, item.Spacing, height, item.Description, out double top);
        var page = Slot(_cursor.Slot);
        page.Flow.Add(new FlowPlacement(item, page.Geometry.Left, page.Geometry.Right, top, null));
    }

    private void PlaceTable(FlowTable item)
    {
        var table = item.Table;
        if (table.RowCount == 0) return;

        var start = _cursor;
        var layout = PdfTableRenderer.Layout(table, _nextNumber + start.Slot, item.Index + 1);
        double startY = start.Y - (start.AtTop ? 0 : item.Spacing);

        (double, double, bool) Edges(int offset)
        {
            int slot = start.Slot + offset;
            if (offset == 0) return (startY, Geometry(slot).BodyBottom, start.EmptyBody);
            var fresh = Fresh(slot);
            return (fresh.Y, Geometry(slot).BodyBottom, fresh.EmptyBody);
        }

        var (splits, headerRows) = TablePaginator.Paginate(
            table,
            layout,
            startY,
            Edges,
            Geometry(start.Slot + 1).BodyHeight,
            () => item.Description);

        var parts = new List<TablePart>();
        foreach (var split in splits)
        {
            int slot = start.Slot + split.PageOffset;
            var page = Slot(slot);
            var part = new TablePart(_pages.Count + slot, split.FirstRow, split.EndRow, split.Top, split.Bottom, page.Geometry.Left + item.X, split.RepeatsHeader);
            parts.Add(part);
            if (part.HasRows) page.Flow.Add(new FlowPlacement(item, page.Geometry.Left, page.Geometry.Right, split.Top, part));
        }

        _plans[table] = new TablePlan(layout, parts, headerRows);
        var last = parts[parts.Count - 1];
        _cursor = new Cursor(start.Slot + splits[splits.Count - 1].PageOffset, last.Bottom, false, false);
    }

    // Lays out the root page's page-coordinate tables first: they break at the body bottom, resume at the body top of
    // the following pages, and share those pages (WR-027).
    private void PlanPageTables()
    {
        var root = _root!;
        int tableNumber = 0;
        foreach (var table in root.Tables)
        {
            int number = ++tableNumber;
            string page = root.Builder?.Description ?? "page 1";
            var layout = PdfTableRenderer.Layout(table, root.Builder?.PageNumber ?? 1, number);
            var (splits, headerRows) = TablePaginator.Paginate(
                table,
                layout,
                table.Y,
                offset => (Geometry(offset).BodyTop, Geometry(offset).BodyBottom, true),
                Geometry(1).BodyHeight,
                () => string.Format(CultureInfo.InvariantCulture, "table {0} on {1}", number, page));

            var parts = splits
                .Select(split => new TablePart(_pages.Count + split.PageOffset, split.FirstRow, split.EndRow, split.Top, split.Bottom, table.X, split.RepeatsHeader))
                .ToList();
            _plans[table] = new TablePlan(layout, parts, headerRows);

            for (int i = 1; i < parts.Count; i++)
                Slot(splits[i].PageOffset).Carry(table, parts[i]);
        }
    }

    // Returns the planned page at the given index among the root page and its continuation pages, adding pages up to it.
    private PlannedPage Slot(int index)
    {
        while (_slots.Count <= index)
        {
            int number = _nextNumber + _slots.Count;
            var geometry = _geometry(number, _root!.Size);
            if (_checkBody && (geometry.BodyHeight <= 0 || geometry.ContentWidth <= 0)) throw NoBody(number, geometry);
            _slots.Add(new PlannedPage(_rootIndex, _slots.Count, number, _root.Size, geometry));
        }
        return _slots[index];
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the index of the root page the planner is in, or -1 before the first.
    /// </summary>
    public int RootIndex => _rootIndex;

    #endregion

    #region Nested Types

    /// <summary>
    /// Where the next flow item goes: the page among the root's pages, the y-coordinate below the previous item,
    /// whether the item is the first in the page's room (its spacing is then dropped), and whether that room is the
    /// whole, empty body (an item that does not fit there fits nowhere).
    /// </summary>
    private readonly record struct Cursor(int Slot, double Y, bool AtTop, bool EmptyBody);

    #endregion
}
