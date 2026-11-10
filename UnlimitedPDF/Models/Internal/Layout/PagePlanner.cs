namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// One page the planner starts from: a page the caller added with <c>AddPage</c>, or the page the library adds for
/// flow content placed before the first caller page. Its continuation pages follow it.
/// </summary>
/// <param name="Builder">The caller page's builder, or null for the library's first flow page.</param>
/// <param name="Size">The page size; the pages that continue it have the same size.</param>
/// <param name="Tables">The page-coordinate tables on the page, in the order they were added.</param>
/// <param name="FlowStart">The index of the first flow item added after the page, which starts on it.</param>
/// <param name="IsImported">Whether the root is an imported page: it takes no tables or flow, so flow added after it
/// starts on a new page after it (WR-036).</param>
internal sealed record PlanRoot(PdfPage? Builder, PdfPageSize Size, IReadOnlyList<PdfTable> Tables, int FlowStart, bool IsImported = false);

/// <summary>
/// Lays out every saved page in reading order (WR-027, WR-030, WR-033): each root page, then the pages that continue
/// it, which carry its page-coordinate tables and its body flow. The planner works incrementally, so
/// <c>Body.RemainingHeight</c> can ask where the next flow item lands while content is still being added, and the same code
/// lays out the whole document at save. Every layout error is thrown here, before any page object or byte exists.
/// </summary>
internal sealed class PagePlanner
{
    #region Fields and Constants

    // Kept items wait here until the item they keep with arrives (WR-033).
    private List<FlowItem> _pending = new();
    // For each kept table of the current kept run: what it keeps with before it, which moves with it when it moves whole.
    private Dictionary<FlowTable, Lead> _leads = new();
    private List<PlannedPage> _pages = new();
    private Dictionary<PdfTable, TablePlan> _plans = new();
    // On a copy made for a read: the plans it replaced in the shared dictionary, restored when the read ends.
    private Dictionary<PdfTable, TablePlan?>? _planUndo;
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

    #region Properties, Indexers

    /// <summary>
    /// Gets the page limit HTML layout stops at, or null for none.
    /// </summary>
    public int? MaxPages { get; init; }

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
    /// bottom. Kept items still waiting for their next item count as placed the way the run would end there: with a
    /// waiting kept table's last row moved to them when it must be, and kept together (WR-033).
    /// </summary>
    /// <param name="emptyDocumentGeometry">The geometry of page 1 when no root page exists yet.</param>
    /// <returns>The page number and remaining height, in points.</returns>
    public (int PageNumber, double Remaining) Probe(PageGeometry emptyDocumentGeometry)
    {
        if (_root is null) return (1, Math.Max(0, emptyDocumentGeometry.BodyHeight));

        if (_pending.Count == 0 && _pendingTable is null) return Where(this);

        // A waiting run is ended on a copy, so this plan keeps waiting for the item that really ends it.
        var copy = Copy();
        try
        {
            copy.FinishPending();
            return Where(copy);
        }
        finally
        {
            copy.UndoPlans();
        }

        static (int, double) Where(PagePlanner planner)
        {
            var cursor = planner._cursor;
            return (planner._slots[0].Number + cursor.Slot, Math.Max(0, cursor.Y - planner.Geometry(cursor.Slot).BodyBottom));
        }
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

    /// <summary>
    /// Returns the height of an item that never splits: a line, image, rectangle, block, or a table inside a block.
    /// Planning and drawing both use it, so they always agree.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The height in points.</returns>
    internal static double MeasureSimple(FlowItem item) => item switch
    {
        FlowText text => LayoutDrawing.LineHeight(text.FontSize),
        FlowImage image => ImageBox.Compute(image.Image, image.Width, image.Height, image.Options).BoxHeight,
        FlowRectangle rectangle => rectangle.Height,
        FlowTable table => PdfTableRenderer.Layout(table.Table, 0, 0).RowHeights.Sum(),
        FlowBlock block => block.Children.Select((child, i) => (i == 0 ? 0 : child.Spacing) + MeasureSimple(child)).Sum(),
        FlowHtml html => html.MeasuredHeight(),
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

        // An imported page has no room for flow: what follows it starts on the next page (WR-036).
        _cursor = Fresh(_root.IsImported ? 1 : 0);
        _nextItem = _root.FlowStart;
    }

    // Ends the waiting kept run where nothing follows it on this page sequence (a page break, the next caller page, or
    // the document's end): a kept table's last row still stays with the kept items after it, and those items stay
    // together (WR-033).
    private void FinishPending()
    {
        if (_pendingTable is not null)
        {
            KeepTableWithNext(_pendingTable, RunNeed(null, atTop: false), RunDescription(RunStart(_pendingTable), null));
            _pendingTable = null;
        }

        if (_pending.Count > 1) KeepRunWith(null);
        foreach (var item in _pending) PlaceBox(item);

        // New lists rather than cleared ones, since a copy made for a read shares them with the plan.
        _pending = new List<FlowItem>();
        _leads = new Dictionary<FlowTable, Lead>();
    }

    // A copy of the plan for the current root that can end the waiting run without changing this one, at a cost that
    // does not grow with the document. Ending the run changes only the pages from where it starts, so only those are
    // copied; earlier pages and the finished pages are shared. The waiting items and leads are shared and only read.
    // The table plans are shared too: the copy records each plan it replaces, and UndoPlans puts them back.
    private PagePlanner Copy()
    {
        int from = RunStartSlot();
        return new PagePlanner(_geometry, _checkBody)
        {
            MaxPages = MaxPages,
            _cursor = _cursor,
            _leads = _leads,
            _nextItem = _nextItem,
            _nextNumber = _nextNumber,
            _pages = _pages,
            _pending = _pending,
            _pendingTable = _pendingTable,
            _planUndo = new Dictionary<PdfTable, TablePlan?>(),
            _plans = _plans,
            _root = _root,
            _rootIndex = _rootIndex,
            _slots = _slots.Select((page, slot) => slot < from ? page : page.Copy()).ToList()
        };
    }

    // The first page that ending the waiting run can change: the cursor's page, or the page of the last part of the
    // waiting kept table. An earlier kept table in its run is reached only through a table that moves whole, so the
    // walk back stops at the first table whose last part is more than its head.
    private int RunStartSlot()
    {
        int from = _cursor.Slot;
        for (var table = _pendingTable; table is not null;)
        {
            if (!_plans.TryGetValue(table.Table, out var plan) || plan.Parts.Count == 0) break;
            var last = plan.Parts[plan.Parts.Count - 1];
            from = Math.Min(from, last.PageIndex - _pages.Count);
            bool movesWhole = last.FirstRow == 0 && last.EndRow - 1 <= plan.HeaderRowCount;
            if (!movesWhole || !_leads.TryGetValue(table, out var lead)) break;
            table = lead.Predecessor;
        }
        return Math.Max(0, from);
    }

    // Replaces a table's plan; on a copy made for a read, remembers the plan it replaced.
    private void SetPlan(PdfTable table, TablePlan plan)
    {
        if (_planUndo is not null && !_planUndo.ContainsKey(table))
            _planUndo[table] = _plans.TryGetValue(table, out var old) ? old : null;
        _plans[table] = plan;
    }

    // Puts back the plans a copy made for a read replaced in the shared dictionary.
    private void UndoPlans()
    {
        foreach (var undo in _planUndo!)
        {
            if (undo.Value is null) _plans.Remove(undo.Key);
            else _plans[undo.Key] = undo.Value;
        }
        _planUndo = null;
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

    // Keeps the last data row of a kept table with what follows it (WR-033): when the given height (the kept run after
    // the table and the head of the item that ends it) does not fit below the table, that row moves, with the repeated
    // header rows above it, to the first following page whose free room holds it and the run. When the row is the
    // table's head, the whole table moves, and what it keeps with before it moves too.
    private void KeepTableWithNext(FlowTable kept, double need, string run)
    {
        if (!_plans.TryGetValue(kept.Table, out var plan) || plan.Parts.Count == 0) return;

        var parts = plan.Parts.ToList();
        var last = parts[parts.Count - 1];
        int slot = last.PageIndex - _pages.Count;
        if (last.Bottom - need >= Geometry(slot).BodyBottom - TablePaginator.Tolerance) return;

        var heights = plan.Layout.RowHeights;
        int headerRows = plan.HeaderRowCount;
        int moveFrom = last.EndRow - 1;
        if (moveFrom < last.FirstRow) return;
        // Header rows never stay behind without a data row, so the whole first part moves with them.
        if (last.FirstRow == 0 && moveFrom <= headerRows) moveFrom = 0;

        if (moveFrom == 0)
        {
            _leads.TryGetValue(kept, out var lead);
            double tableHeight = heights.Take(last.EndRow).Sum();

            // The table and the kept items before it already start at the top of an empty body, so no page holds them
            // together with the run (WR-033), as for any run that does not fit an empty body.
            if (lead?.Predecessor is null && StartsEmptyBody(slot, kept, lead))
                throw Oversize(run, GroupHeight(kept, lead, tableHeight, need, atTop: true), Geometry(slot).BodyHeight);

            if (lead is not null && (lead.Items.Count > 0 || lead.Predecessor is not null))
            {
                MoveWithLead(kept, lead, slot, tableHeight, need, run);
                return;
            }
        }

        double moved = heights.Skip(moveFrom).Take(last.EndRow - moveFrom).Sum();
        bool repeats = kept.Table.RepeatHeaderRows && headerRows > 0 && moveFrom > 0;
        double headerHeight = repeats ? heights.Take(headerRows).Sum() : 0;

        // The moving row is all of the table's part on a page whose body is otherwise empty, so no page holds it with
        // the run (WR-033); moving it would leave that page blank.
        if (moveFrom > 0 && moveFrom == last.FirstRow && StartsEmptyBody(slot, kept, null))
            throw Oversize(run, headerHeight + moved + need, Geometry(slot).BodyHeight);

        // The moved rows go to the first following page whose free room holds them together with the run.
        int targetSlot = slot + 1;
        double top;
        while (true)
        {
            var target = Fresh(targetSlot);
            double gap = target.AtTop || moveFrom > 0 ? 0 : kept.Spacing;
            top = target.Y - gap;
            double bottom = Geometry(targetSlot).BodyBottom - TablePaginator.Tolerance;
            if (top - headerHeight - moved - need >= bottom) break;
            if (target.EmptyBody)
            {
                double body = Geometry(targetSlot).BodyHeight;
                if (top - headerHeight - moved < bottom) throw Oversize(kept.Description, headerHeight + moved, body);
                throw Oversize(run, headerHeight + moved + need, body);
            }
            targetSlot++;
        }

        Slot(targetSlot);
        var shortened = last with { EndRow = moveFrom, Bottom = last.Bottom + moved };
        if (!shortened.HasRows) shortened = shortened with { RepeatsHeader = false, Bottom = shortened.Top };
        var movedPart = new TablePart(_pages.Count + targetSlot, moveFrom, last.EndRow, top, top - headerHeight - moved, Geometry(targetSlot).Left + kept.X, repeats);

        parts[parts.Count - 1] = shortened;
        parts.Add(movedPart);
        SetPlan(kept.Table, new TablePlan(plan.Layout, parts, headerRows));

        var placements = _slots[slot].Flow;
        int index = placements.FindIndex(p => p.Item == kept && p.Part == last);
        if (shortened.HasRows) placements[index] = placements[index] with { Part = shortened };
        else placements.RemoveAt(index);
        _slots[targetSlot].Flow.Add(new FlowPlacement(kept, Geometry(targetSlot).Left, Geometry(targetSlot).Right, top, movedPart));
        _cursor = new Cursor(targetSlot, movedPart.Bottom, false, false);
    }

    // Moves a kept table that moves whole together with what it keeps with before it (WR-033): the kept items placed
    // just before it and, when a kept table comes before those, that table's last row. They all stand on the table's
    // page, so they are taken off it and placed again where they fit together with the run after the table.
    private void MoveWithLead(FlowTable kept, Lead lead, int slot, double tableHeight, double need, string run)
    {
        foreach (var page in _slots.Skip(slot)) page.Flow.RemoveAll(p => p.Item == kept || lead.Items.Contains(p.Item));

        double Group(bool atTop) => GroupHeight(kept, lead, tableHeight, need, atTop);

        if (lead.Predecessor is FlowTable predecessor)
        {
            // The earlier kept table's last row moves to the group, as it would for any kept run after it.
            KeepTableWithNext(predecessor, Group(atTop: false), run);
            var end = _plans[predecessor.Table].Parts.Last();
            _cursor = new Cursor(end.PageIndex - _pages.Count, end.Bottom, false, false);
        }
        else
        {
            var cursor = Fresh(slot + 1);
            while (cursor.Y - Group(cursor.AtTop) < Geometry(cursor.Slot).BodyBottom - TablePaginator.Tolerance)
            {
                if (cursor.EmptyBody) throw Oversize(run, Group(atTop: true), Geometry(cursor.Slot).BodyHeight);
                cursor = Fresh(cursor.Slot + 1);
            }
            _cursor = cursor;
        }

        foreach (var item in lead.Items) PlaceBox(item);
        PlaceTable(kept);
    }

    // The height a kept table that moves whole needs with the kept items before it and the run after it.
    private static double GroupHeight(FlowTable kept, Lead? lead, double tableHeight, double need, bool atTop)
    {
        var items = lead?.Items ?? new List<FlowItem>();
        double total = 0;
        for (int i = 0; i < items.Count; i++)
            total += (i == 0 && atTop ? 0 : items[i].Spacing) + MeasureSimple(items[i]);
        return total + (items.Count == 0 && atTop ? 0 : kept.Spacing) + tableHeight + need;
    }

    // Whether a kept table and the kept items before it are all that stands on a page whose body is otherwise empty.
    private bool StartsEmptyBody(int slot, FlowTable kept, Lead? lead) =>
        !Carried(slot) && _slots[slot].Flow.All(p => p.Item == kept || (lead?.Items.Contains(p.Item) ?? false));

    // The first item of the kept run that a kept table belongs to.
    private FlowItem RunStart(FlowTable table) => _leads.TryGetValue(table, out var lead) ? lead.First : table;

    // Moves the cursor to a fresh page when the kept run and the item's head (if any) do not fit together here.
    private void KeepRunWith(FlowItem? next)
    {
        if (_pending.Count == 0) return;

        var cursor = _cursor;
        while (cursor.Y - RunNeed(next, cursor.AtTop) < Geometry(cursor.Slot).BodyBottom - TablePaginator.Tolerance)
        {
            if (cursor.EmptyBody) throw Oversize(RunDescription(_pending[0], next), RunNeed(next, cursor.AtTop), Geometry(cursor.Slot).BodyHeight);
            cursor = Fresh(cursor.Slot + 1);
        }

        _cursor = cursor;
    }

    // Names a kept run from its first item to the item that ends it, or to its last kept item when nothing follows it.
    private string RunDescription(FlowItem first, FlowItem? next) => string.Format(
        CultureInfo.InvariantCulture,
        "the kept-together run of flow items {0} to {1}",
        first.Index + 1,
        (next ?? (_pending.Count > 0 ? _pending[_pending.Count - 1] : first)).Index + 1);

    // The height the waiting kept run needs together with the head of the item that ends it (if any).
    private double RunNeed(FlowItem? next, bool atTop)
    {
        double total = 0;
        for (int i = 0; i < _pending.Count; i++)
            total += (i == 0 && atTop ? 0 : _pending[i].Spacing) + MeasureSimple(_pending[i]);
        // A table without rows draws nothing, so it adds no spacing and no head (WR-033).
        bool takesRoom = next is not null && next is not FlowTable { Table.RowCount: 0 };
        return takesRoom ? total + next!.Spacing + HeadHeight(next) : total;
    }

    private void Place(FlowItem item)
    {
        // HTML is laid out at the body width of the page where it starts (WR-038), found again at every planning pass so
        // changed margins or page settings take effect (F-21).
        if (item is FlowHtml html) html.LayoutWidth = Geometry(_cursor.Slot).ContentWidth;

        if (item is FlowBreak)
        {
            FinishPending();
            _cursor = Fresh(_cursor.Slot + 1);
            Slot(_cursor.Slot);
            return;
        }

        // A kept table without rows draws nothing and takes no room, so the kept run goes on past it (WR-033).
        if (item is FlowTable { KeepWithNext: true } empty && empty.Table.RowCount == 0)
        {
            PlaceTable(empty);
            return;
        }

        // Kept items wait for the item that ends their run; a kept table waits with them (WR-033).
        // HTML taller than the body cannot be kept with what follows; it is placed and split as usual.
        if (item.KeepWithNext && item is not FlowTable && !(item is FlowHtml tall && MeasureSimple(tall) > Geometry(_cursor.Slot).BodyHeight))
        {
            _pending.Add(item);
            return;
        }

        FlowTable? predecessor = _pendingTable;
        if (predecessor is not null)
        {
            KeepTableWithNext(predecessor, RunNeed(item, atTop: false), RunDescription(RunStart(predecessor), item));
            _pendingTable = null;
        }

        KeepRunWith(item);
        var lead = _pending.ToList();
        foreach (var kept in _pending) PlaceBox(kept);
        _pending.Clear();

        if (item is FlowTable table)
        {
            PlaceTable(table);
            if (table.KeepWithNext && table.Table.RowCount > 0)
            {
                _pendingTable = table;
                var first = predecessor is not null ? RunStart(predecessor) : lead.Count > 0 ? lead[0] : table;
                _leads[table] = new Lead(lead, predecessor, first);
            }
        }
        else
        {
            PlaceBox(item);
        }

        if (!item.KeepWithNext) _leads.Clear();
    }

    private void PlaceBox(FlowItem item)
    {
        if (item is FlowHtml html)
        {
            PlaceHtml(html);
            return;
        }
        double height = MeasureSimple(item);
        _cursor = FitBox(_cursor, item.Spacing, height, item.Description, out double top);
        var page = Slot(_cursor.Slot);
        page.Flow.Add(new FlowPlacement(item, page.Geometry.Left, page.Geometry.Right, top, null));
    }

    // Lays HTML out into the free room of the following pages, starting below the previous item (WR-038). Layout
    // warnings are recorded on the HTML; in strict mode the first one fails before anything is written.
    private void PlaceHtml(FlowHtml item)
    {
        var start = _cursor;
        double startY = start.Y - (start.AtTop ? 0 : item.Spacing);
        HtmlPageRoom Room(int offset)
        {
            int slot = start.Slot + offset;
            // A few bytes of HTML can ask for any number of pages (a huge height or padding); with a page limit set, the
            // layout stops at the first page past it instead of planning them all (WR-033, A06).
            if (MaxPages is int max && _nextNumber + slot > max)
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The content needs more than {0} pages, but Body.MaxPages is {0}: {1} does not fit. Shorten the content or raise the limit.",
                    max,
                    item.Description));
            if (offset == 0) return new HtmlPageRoom(startY, Geometry(slot).BodyBottom, start.EmptyBody);
            var fresh = Fresh(slot);
            return new HtmlPageRoom(fresh.Y, Geometry(slot).BodyBottom, fresh.EmptyBody);
        }

        var warnings = new List<PdfHtmlWarning>();
        double width = item.LayoutWidth ?? Geometry(start.Slot).ContentWidth;
        var fragments = new HtmlLayoutEngine(item.Html.Model, width, Room, warnings).Layout();
        var numbered = warnings
            .Select(w => new PdfHtmlWarning(w.Kind, w.Source, w.Message, w.Line, w.PageNumber is int offset ? _nextNumber + start.Slot + offset : null))
            .ToList();
        if (item.Html.FailOnWarning && numbered.Count > 0) throw new PdfHtmlException(numbered[0]);
        item.Html.SetLayoutWarnings(numbered);

        bool placed = false;
        foreach (var fragment in fragments)
        {
            if (fragment.Commands.Count == 0) continue;
            int slot = start.Slot + fragment.PageOffset;
            var page = Slot(slot);
            page.Flow.Add(new FlowPlacement(item, page.Geometry.Left, page.Geometry.Right, fragment.Top, null, fragment));
            _cursor = new Cursor(slot, fragment.Bottom, false, false);
            placed = true;
        }
        if (!placed) Slot(start.Slot);
    }

    private void PlaceTable(FlowTable item)
    {
        var table = item.Table;
        if (table.RowCount == 0)
        {
            // A table without rows draws nothing and takes no room.
            SetPlan(table, new TablePlan(new TableLayout(Array.Empty<double>(), Array.Empty<double>(), new CellLayout[0, 0]), Array.Empty<TablePart>(), 0));
            return;
        }

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

        SetPlan(table, new TablePlan(layout, parts, headerRows));
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
                () => string.Format(CultureInfo.InvariantCulture, "table {0} on {1}", number, page));

            var parts = splits
                .Select(split => new TablePart(_pages.Count + split.PageOffset, split.FirstRow, split.EndRow, split.Top, split.Bottom, table.X, split.RepeatsHeader))
                .ToList();
            SetPlan(table, new TablePlan(layout, parts, headerRows));

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
            bool imported = _root.IsImported && _slots.Count == 0;
            if (_checkBody && !imported && (geometry.BodyHeight <= 0 || geometry.ContentWidth <= 0)) throw NoBody(number, geometry);
            _slots.Add(new PlannedPage(_rootIndex, _slots.Count, number, geometry) { IsImported = imported });
        }
        return _slots[index];
    }

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// Where the next flow item goes: the page among the root's pages, the y-coordinate below the previous item,
    /// whether the item is the first in the page's room (its spacing is then dropped), and whether that room is the
    /// whole, empty body (an item that does not fit there fits nowhere).
    /// </summary>
    private readonly record struct Cursor(int Slot, double Y, bool AtTop, bool EmptyBody);

    /// <summary>
    /// What a kept table keeps with before it in its kept run: the kept items placed just before it, and the kept
    /// table before those, whose last row stays with them. <paramref name="First"/> is the first item of the whole kept
    /// run, which names the run in errors.
    /// </summary>
    private sealed record Lead(List<FlowItem> Items, FlowTable? Predecessor, FlowItem First);

    #endregion
}
