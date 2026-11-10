using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// The free room on one page for laid-out HTML: its top and bottom page coordinates and whether the body is otherwise
/// empty there.
/// </summary>
/// <param name="Top">The top.</param>
/// <param name="Bottom">The bottom.</param>
/// <param name="EmptyBody">Whether nothing else stands in the body.</param>
internal readonly record struct HtmlPageRoom(double Top, double Bottom, bool EmptyBody);

/// <summary>
/// Lays out HTML into pages (WR-038): CSS block layout with collapsing vertical margins, padding, borders, backgrounds,
/// widths, and automatic horizontal margins; inline layout with white-space processing, wrapping at spaces, breaking a
/// word only when it alone is wider than its line, alignment including justify, indentation, line height, vertical
/// alignment, letter spacing, text transforms, decorations, inline backgrounds, images, and inline-blocks; and list
/// markers. Pagination happens during layout: a line, an image, or an atomic inline never splits and moves to the next
/// page when it does not fit; forced breaks and <c>break-inside: avoid</c> are honored; block decorations are sliced
/// at page breaks. Tables are laid out by the table part of this class.
/// </summary>
internal sealed partial class HtmlLayoutEngine
{
    #region Fields and Constants

    // Room for floating-point rounding when comparing positions, matching the body flow.
    private const double Tolerance = 0.0001;

    private readonly LayoutCache _cache;
    private readonly Func<int, HtmlPageRoom> _edges;
    private readonly Dictionary<int, HtmlPageFragment> _fragments = new();
    private readonly bool _measuring;
    private readonly int? _lastPage;
    private readonly HtmlModel _model;
    private readonly List<OpenBox> _open = new();
    private readonly List<PendingMarker> _pendingMarkers = new();
    private readonly HashSet<CachedLayout> _reported = new();
    private readonly Dictionary<int, HtmlPageRoom> _rooms = new();
    private readonly List<PdfHtmlWarning> _warnings;
    private readonly double _width;
    private bool _atTop;
    private bool _forceBreak;
    private int _page;
    private double _pendingMargin;
    private bool _report;
    private double _y;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="model">The loaded HTML.</param>
    /// <param name="width">The width HTML is laid out at: the body width of the page it starts on.</param>
    /// <param name="edges">The free room on each page, counted from the page the HTML starts on.</param>
    /// <param name="warnings">Receives layout warnings.</param>
    /// <param name="lastPage">The last page offset layout may move to, or null for no limit; moving past it throws
    /// <see cref="HtmlPageLimitException"/>.</param>
    public HtmlLayoutEngine(HtmlModel model, double width, Func<int, HtmlPageRoom> edges, List<PdfHtmlWarning> warnings, int? lastPage = null)
        : this(model, width, edges, warnings, measuring: false, new LayoutCache())
    {
        _lastPage = lastPage;
    }

    private HtmlLayoutEngine(HtmlModel model, double width, Func<int, HtmlPageRoom> edges, List<PdfHtmlWarning> warnings, bool measuring, LayoutCache cache)
    {
        _model = model;
        _width = width;
        _edges = edges;
        _warnings = warnings;
        _measuring = measuring;
        _cache = cache;
        var room = Room(0);
        _y = room.Top;

        // HTML that starts below other content is not at the top of its page (F-08).
        _atTop = room.EmptyBody;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the height laid-out content needs on one page of unlimited height.
    /// </summary>
    /// <param name="model">The loaded HTML.</param>
    /// <param name="box">The box.</param>
    /// <param name="width">The width.</param>
    /// <returns>The height in points.</returns>
    public static double MeasureHeight(HtmlModel model, HtmlBox box, double width) =>
        new HtmlLayoutEngine(model, width, Unlimited, new List<PdfHtmlWarning>(), measuring: true, new LayoutCache()).Measure(box, width);

    /// <summary>
    /// Lays out the whole HTML.
    /// </summary>
    /// <returns>The page fragments in page order.</returns>
    public IReadOnlyList<HtmlPageFragment> Layout()
    {
        var builder = new BoxTreeBuilder(_model);
        var root = builder.Build();
        if (builder.FlattenedInlineBlock is { } flattened)
        {
            Warn(PdfHtmlWarningKind.UnsupportedValue, flattened.Name, string.Format(CultureInfo.InvariantCulture,
                "Inline-blocks nested more than {0} deep are laid out inline.", BoxTreeBuilder.MaxInlineBlockNesting));
        }
        LayoutBox(root, 0, _width);
        Flush(0);
        Current.Bottom = Math.Min(Current.Bottom, _y);
        return _fragments.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
    }

    #endregion

    #region Private Methods

    private static double Collapse(double a, double b) =>
        a >= 0 && b >= 0 ? Math.Max(a, b) : a < 0 && b < 0 ? Math.Min(a, b) : a + b;

    // One page of unlimited height, for measuring, table cells, and inline-blocks.
    private static HtmlPageRoom Unlimited(int page) => new(0, -1e12, true);

    // Places vertical space (padding, borders, set heights) of any size: what does not fit continues on the next pages
    // (F-08). Anything placed means the page is no longer empty.
    private void Advance(double amount)
    {
        while (amount > Tolerance)
        {
            double room = _y - Room(_page).Bottom;
            if (_measuring || amount <= room + Tolerance)
            {
                _y -= amount;
                break;
            }
            amount -= Math.Max(0, room);
            _y = Room(_page).Bottom;
            Current.Bottom = Math.Min(Current.Bottom, _y);
            NextPage();
        }
        _atTop = false;
        Current.Bottom = Math.Min(Current.Bottom, _y);
    }

    // The height laid out since a position: on its page, plus the bodies of the pages between, plus this page so far.
    private double ContentSince(int page, double y)
    {
        if (page == _page) return y - _y;
        double total = y - Room(page).Bottom;
        for (int p = page + 1; p < _page; p++) total += Room(p).Top - Room(p).Bottom;
        return total + Room(_page).Top - _y;
    }

    // The body height of a fresh page, for "fits on a page" checks (F-11).
    private double FreshBodyHeight()
    {
        var room = Room(_page + 1);
        return room.Top - room.Bottom;
    }

    // Lays a box out once per width on a page of unlimited height (F-05), with its warnings kept for when it is placed.
    private CachedLayout LayoutUnbounded(HtmlBox box, double width)
    {
        if (_cache.Layouts.TryGetValue((box, width), out var known)) return known;
        var warnings = new List<PdfHtmlWarning>();
        var engine = new HtmlLayoutEngine(_model, width, Unlimited, warnings, measuring: true, _cache) { _report = true };
        engine.LayoutBox(box, 0, width);
        engine.Flush(0);
        var commands = engine._fragments.TryGetValue(0, out var fragment) ? fragment.Commands : new List<HtmlCommand>();
        var result = new CachedLayout(commands, -engine._y, warnings);
        _cache.Layouts[(box, width)] = result;
        return result;
    }

    private double Measure(HtmlBox box, double width) => LayoutUnbounded(box, width).Height;

    // Reports a laid-out part's warnings once, on the page where it is placed (F-14, F-15).
    private void ReportOnce(CachedLayout layout)
    {
        if (!_reported.Add(layout)) return;
        foreach (var w in layout.Warnings)
        {
            if (w.Kind == PdfHtmlWarningKind.LinkDropped && !_reportedLinks.Add(w.Source)) continue;
            _warnings.Add(new PdfHtmlWarning(w.Kind, w.Source, w.Message, w.Line, _page));
        }
    }

    private HtmlPageFragment Current => FragmentAt(_page);

    // Places the pending (collapsed) margin and makes room for content of the given height below it, moving to the
    // next page when it does not fit. A margin before an unforced page break is dropped.
    private void Flush(double height)
    {
        if (_forceBreak)
        {
            _forceBreak = false;
            if (!_atTop) NextPage();
        }
        if (!_atTop && _y - _pendingMargin - height < Room(_page).Bottom - Tolerance)
        {
            NextPage();
            _pendingMargin = 0;
        }
        _y -= _pendingMargin;
        _pendingMargin = 0;
        foreach (var box in _open)
        {
            if (box.StartY is null)
            {
                box.StartY = _y;
                box.StartPage = _page;
                if (!box.InsertAt.ContainsKey(_page)) box.InsertAt[_page] = Current.Commands.Count;
            }
        }
    }

    private HtmlPageFragment FragmentAt(int page)
    {
        if (!_fragments.TryGetValue(page, out var fragment))
        {
            fragment = new HtmlPageFragment(page, Room(page).Top);
            _fragments[page] = fragment;
        }
        return fragment;
    }

    private void Emit(HtmlCommand command)
    {
        var fragment = Current;
        fragment.Commands.Add(command);
    }

    private void LayoutBox(HtmlBox box, double left, double width)
    {
        switch (box)
        {
            case BlockBox block:
                LayoutBlock(block, left, width);
                break;
            case TableBox table:
                LayoutTable(table, left, width);
                break;
        }
    }

    private void LayoutBlock(BlockBox box, double left, double width)
    {
        var s = box.Style;
        var (ml, mr, bl, br, pl, pr, cw) = Horizontal(s, width);
        double mt = Resolve(s.Margins[0], width), mb = Resolve(s.Margins[2], width);
        double bt = s.Borders[0].UsedWidth, bb = s.Borders[2].UsedWidth;
        double pt = Resolve(s.Paddings[0], width), pb = Resolve(s.Paddings[2], width);

        if (s.BreakBefore == CssBreak.Page && !_measuring) _forceBreak = true;
        if (s.AvoidBreakInside && !_atTop && !_measuring)
        {
            // Judged against a fresh page's body, not the room left here (F-11).
            double needed = Measure(box, width);
            if (needed <= FreshBodyHeight() && _y - _pendingMargin - needed < Room(_page).Bottom - Tolerance) _forceBreak = true;
        }

        _pendingMargin = Collapse(_pendingMargin, mt);
        double borderLeft = left + ml;
        var open = new OpenBox(box, borderLeft, borderLeft + bl + pl + cw + pr + br);
        _open.Add(open);
        if (bt + pt > 0) Flush(bt + pt);

        bool marked = false;
        void Mark()
        {
            if (marked || box.Element is not { } element) return;
            marked = true;
            int level = element.Name.Length == 2 && element.Name[0] == 'h' && element.Name[1] is >= '1' and <= '6' ? element.Name[1] - '0' : 0;
            string? id = element.Attribute("id");
            if (id is not null || level > 0)
                FragmentAt(open.StartPage ?? _page).Commands.Add(new MarkCommand(id, level, level > 0 ? Collapsed(element.TextContent()) : null, borderLeft, open.StartY ?? _y));
        }
        if (bt + pt > 0) { Mark(); Advance(bt + pt); }

        double contentTop = _y;
        int contentPage = _page;
        double contentLeft = borderLeft + bl + pl;
        if (box.Marker is { Inside: false } marker) _pendingMarkers.Add(new PendingMarker(marker, s, contentLeft));

        if (box.Inlines.Count > 0)
        {
            LayoutInline(box, contentLeft, cw, Mark);
        }
        else
        {
            // An empty block lets margins collapse through it (F-20).
            foreach (var child in box.Children)
            {
                LayoutBox(child, contentLeft, cw);
                Mark();
            }
        }
        Mark();

        // A marker whose item has no line is drawn at the item's top, on the page where the item starts (F-13).
        _pendingMarkers.RemoveAll(p =>
        {
            if (p.Style != s) return false;
            DrawMarker(p, contentTop - p.Style.FontSize, FragmentAt(contentPage));
            return true;
        });

        // A zero height adds nothing, so margins still collapse through the block (F-41).
        double setHeight = s.Height is { } h ? Resolve(h, 0) : 0;
        if (pb + bb > 0 || setHeight > Tolerance) Flush(0);
        if (setHeight > Tolerance && open.StartY is double startY && open.StartPage is int startPage)
        {
            // A set height makes room across pages like any other content (F-08).
            double missing = bt + pt + setHeight - ContentSince(startPage, startY);
            if (missing > Tolerance) Advance(missing);
        }
        if (pb + bb > 0)
        {
            Flush(pb + bb);
            Advance(pb + bb);
        }
        if (_open.Remove(open) && HasDecoration(s)) Decorate(open);
        _pendingMargin = Collapse(_pendingMargin, mb);
        if (s.BreakAfter == CssBreak.Page && !_measuring) _forceBreak = true;
    }

    // Width, margins, borders, and padding across a containing width (fit to width, D-016).
    private static (double Ml, double Mr, double Bl, double Br, double Pl, double Pr, double Cw) Horizontal(ComputedStyle s, double width)
    {
        double bl = s.Borders[3].UsedWidth, br = s.Borders[1].UsedWidth;
        double pl = Resolve(s.Paddings[3], width), pr = Resolve(s.Paddings[1], width);
        bool autoLeft = s.Margins[3] is null, autoRight = s.Margins[1] is null;
        double ml = autoLeft ? 0 : Resolve(s.Margins[3], width), mr = autoRight ? 0 : Resolve(s.Margins[1], width);
        double? specified = s.Width is { } w ? Resolve(w, width) : null;

        double cw = specified ?? width - ml - mr - bl - br - pl - pr;
        if (s.MaxWidth is { } max) cw = Math.Min(cw, Resolve(max, width));
        if (s.MinWidth is { } min) cw = Math.Max(cw, Resolve(min, width));

        // Narrow to the room available (D-016), then share what is left between automatic margins.
        double room = width - ml - mr - bl - br - pl - pr;
        if (cw > room + Tolerance) cw = Math.Max(0, room);
        double free = width - ml - mr - bl - br - pl - pr - cw;
        if (free > Tolerance && (specified is not null || s.MaxWidth is not null))
        {
            if (autoLeft && autoRight) { ml += free / 2; mr += free / 2; }
            else if (autoLeft) ml += free;
            else if (autoRight) mr += free;
        }
        return (ml, mr, bl, br, pl, pr, Math.Max(0, cw));
    }

    private static bool HasDecoration(ComputedStyle s) => s.BackgroundColor is not null || s.Borders.Any(b => b.UsedWidth > 0);

    // Draws a block's background and borders on every page it spans, before its content there.
    private void Decorate(OpenBox box)
    {
        var s = box.Box.Style;
        int first = box.StartPage ?? _page;
        double startY = box.StartY ?? _y;
        for (int page = first; page <= _page; page++)
        {
            double top = page == first ? startY : Room(page).Top;
            double bottom = page == _page ? _y : Math.Min(FragmentAt(page).Bottom, Room(page).Bottom);
            if (top - bottom <= Tolerance && !(page == first && page == _page)) continue;
            var commands = new List<HtmlCommand>();
            if (s.BackgroundColor is { } background) commands.Add(new FillCommand(box.Left, bottom, box.Right - box.Left, top - bottom, background));
            AddBorders(commands, s.Borders, box.Left, box.Right, top, bottom, drawTop: page == first, drawBottom: page == _page);
            var fragment = FragmentAt(page);
            int index = box.InsertAt.TryGetValue(page, out int at) ? at : 0;
            fragment.Commands.InsertRange(Math.Min(index, fragment.Commands.Count), commands);
            foreach (var other in _open)
            {
                // An outer box that starts at the same spot stays underneath: only later positions move.
                if (other.InsertAt.TryGetValue(page, out int otherIndex) && otherIndex > index) other.InsertAt[page] = otherIndex + commands.Count;
            }
        }
    }

    private static void AddBorders(List<HtmlCommand> commands, CssBorderSide[] borders, double left, double right, double top, double bottom, bool drawTop, bool drawBottom)
    {
        void Side(CssBorderSide side, double x1, double y1, double x2, double y2, bool horizontal, double inward)
        {
            if (side.UsedWidth <= 0) return;
            double w = side.Width;
            if (side.Style == HtmlBorderStyle.Double && w >= 3 * 0.75)
            {
                double third = w / 3;
                double o1 = third / 2, o2 = w - third / 2;
                commands.Add(horizontal
                    ? new LineCommand(x1, y1 + inward * o1, x2, y2 + inward * o1, third, side.Color, HtmlBorderStyle.Solid)
                    : new LineCommand(x1 + inward * o1, y1, x2 + inward * o1, y2, third, side.Color, HtmlBorderStyle.Solid));
                commands.Add(horizontal
                    ? new LineCommand(x1, y1 + inward * o2, x2, y2 + inward * o2, third, side.Color, HtmlBorderStyle.Solid)
                    : new LineCommand(x1 + inward * o2, y1, x2 + inward * o2, y2, third, side.Color, HtmlBorderStyle.Solid));
                return;
            }
            var style = side.Style == HtmlBorderStyle.Double ? HtmlBorderStyle.Solid : side.Style;
            double half = w / 2 * inward;
            commands.Add(horizontal
                ? new LineCommand(x1, y1 + half, x2, y2 + half, w, side.Color, style)
                : new LineCommand(x1 + half, y1, x2 + half, y2, w, side.Color, style));
        }

        if (drawTop) Side(borders[0], left, top, right, top, horizontal: true, inward: -1);
        if (drawBottom) Side(borders[2], left, bottom, right, bottom, horizontal: true, inward: 1);
        Side(borders[3], left, top, left, bottom, horizontal: false, inward: 1);
        Side(borders[1], right, top, right, bottom, horizontal: false, inward: -1);
    }

    private static string Collapsed(string text) => string.Join(" ", text.Split(new[] { ' ', '\t', '\n', '\r', '\f', ' ' }, StringSplitOptions.RemoveEmptyEntries));

    private void DrawMarker(PendingMarker pending, double baseline, HtmlPageFragment fragment)
    {
        void Emit(HtmlCommand command) => fragment.Commands.Add(command);
        var s = pending.Style;
        double size = s.FontSize;
        var font = StandardFonts.Select(s.FontFamily, s.FontWeight, s.Italic);
        double gap = size * 0.5;
        switch (pending.Marker.Type)
        {
            case CssListStyleType.Disc:
            case CssListStyleType.Circle:
                double r = size * 0.18;
                Emit(new CircleCommand(pending.ContentLeft - gap - r, baseline + size * 0.3, r, s.Color, pending.Marker.Type == CssListStyleType.Disc));
                break;
            case CssListStyleType.Square:
                double side = size * 0.36;
                Emit(new FillCommand(pending.ContentLeft - gap - side, baseline + size * 0.3 - side / 2, side, side, s.Color));
                break;
            default:
                if (pending.Marker.Text is not { } text) return;
                var codes = WinAnsiEncoding.Encode(text);
                double width = StandardFontMetrics.Measure(font, text, size);
                Emit(new TextCommand(font, size, s.Color, pending.ContentLeft - size * 0.3 - width, baseline, codes, 0, 0));
                break;
        }
    }

    // Moves to the next page: open clips close here and reopen there; decorations of open blocks go after the clips
    // and before repeated table headers (F-16). A page below carried rows is not empty (F-08).
    private void NextPage()
    {
        foreach (var _ in _clips) Current.Commands.Add(new ClipEndCommand());
        _page++;
        CheckPageLimit(_page);
        var room = Room(_page);
        _y = room.Top;
        _atTop = room.EmptyBody;
        ReopenClips();
        var fragment = Current;
        foreach (var box in _open)
        {
            if (!box.InsertAt.ContainsKey(_page)) box.InsertAt[_page] = fragment.Commands.Count;
        }
        DrawRepeatedHeaders();
    }

    // Fails when layout moves past the last page it may use. Looking at a later page's room does not count; only
    // moving there does (F-42).
    private void CheckPageLimit(int page)
    {
        if (_lastPage is int last && page > last) throw new HtmlPageLimitException();
    }

    private static double Resolve(CssLength? length, double percentBase) =>
        length is { } l ? l.ToPoints(ComputedStyle.DefaultFontSize, ComputedStyle.DefaultFontSize, percentBase) : 0;

    private HtmlPageRoom Room(int page)
    {
        if (!_rooms.TryGetValue(page, out var room))
        {
            room = _edges(page);
            _rooms[page] = room;
        }
        return room;
    }

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// Thrown when HTML layout moves past the page limit it was given.
    /// </summary>
    internal sealed class HtmlPageLimitException : Exception
    {
    }

    // A block whose background or borders are drawn once its extent is known.
    private sealed class OpenBox
    {
        public OpenBox(HtmlBox box, double left, double right)
        {
            Box = box;
            Left = left;
            Right = right;
        }

        public HtmlBox Box { get; }

        public Dictionary<int, int> InsertAt { get; } = new();

        public double Left { get; }

        public double Right { get; }

        public int? StartPage { get; set; }

        public double? StartY { get; set; }
    }

    private sealed record PendingMarker(ListMarker Marker, ComputedStyle Style, double ContentLeft);

    // A box laid out on one page of unlimited height: its commands (top at 0, left at 0), height, and warnings.
    private sealed record CachedLayout(List<HtmlCommand> Commands, double Height, List<PdfHtmlWarning> Warnings);

    // Results shared by every engine of one layout: intrinsic widths per box and layouts per box and width (F-05).
    private sealed class LayoutCache
    {
        public Dictionary<(HtmlBox Box, bool Preferred, bool BreakWords), (double Min, double Max)> Intrinsic { get; } = new();

        public Dictionary<(HtmlBox Box, double Width), CachedLayout> Layouts { get; } = new();
    }

    #endregion
}
