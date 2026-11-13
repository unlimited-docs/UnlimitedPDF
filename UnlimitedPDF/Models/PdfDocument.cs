namespace UnlimitedPDF;

public class PdfDocument
{
    #region Fields and Constants

    private readonly PdfHeader _fileHeader;
    private readonly Dictionary<string, int> _imageObjects = new(StringComparer.Ordinal);
    private readonly Dictionary<PdfFile, Dictionary<int, int>> _importedObjects = new();
    private readonly List<PageImport> _imports = new();
    private readonly PdfBody _objects;
    private readonly List<PdfObject> _outlineObjects = new();
    private readonly List<PageEntry> _pageEntries = new();
    private readonly PdfPages _pages;
    private readonly List<RemainingQuery> _queries = new();
    private readonly List<PdfPageSettings> _rules = new();
    private readonly Dictionary<StandardFont, int> _standardFontObjects = new();
    private double _bodyMarginBottom;
    private double _bodyMarginTop;
    private PageEntry? _flowRoot;
    private PdfPageSize? _flowRootSize;
    private int _layoutVersion;
    private PagePlanner? _livePlanner;
    private string? _livePlannerStamp;
    private double _marginBottom;
    private double _marginLeft;
    private double _marginRight;
    private double _marginTop;
    private int _signedItems;

    #endregion

    #region Constructors and Finalizers

    public PdfDocument(PdfPageSize pageSize = PdfPageSize.A4)
    {
        PageSize = pageSize;
        _fileHeader = new PdfHeader();
        _objects = new PdfBody();
        _pages = new PdfPages();
        Header = new PdfPageSection(LayoutChanged);
        Footer = new PdfPageSection(LayoutChanged);
        Body = new PdfBodyFlow(this);

        // Build the core PDF document structure
        var catalog = new PdfObject(objectNumber: 1, content: new PdfCatalog(pages: new PdfIndirectReference(objectNumber: 2)).ToString());
        var pagesObject = new PdfObject (objectNumber: 2, content: _pages.ToString());

        // Define a font resource
        var font = new PdfObject(objectNumber: 3, content: new PdfFont().ToString());

        // Add all objects to the document body
        _objects.AddObject(catalog);
        _objects.AddObject(pagesObject);
        _objects.AddObject(font);
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a new page to the PDF document and returns a page object for adding content. When the body flow is used,
    /// the page also acts as a page break: flow items added after it start on it.
    /// </summary>
    /// <returns>A <see cref="PdfPage"/> object to which content can be added.</returns>
    [AddedVersion("0.0.2")]
    [Description("Adds a new page to the PDF document and returns a page object for adding content.")]
    public PdfPage AddPage()
    {
        var entry = CreatePageEntry(PageSize, pageNumber: _pageEntries.Count + 1, continuationIndex: 0);
        entry.FlowStart = Body.Items.Count;

        // Update the /Kids array in the /Pages object to include the new page.
        _pages.Kids.Add(new PdfIndirectReference(objectNumber: entry.PageObject.ObjectNumber));
        _pageEntries.Add(entry);

        return entry.Builder!;
    }

    /// <summary>
    /// Adds pages of an existing PDF after the pages added so far: the whole file, or the pages from
    /// <paramref name="firstPage"/> to <paramref name="lastPage"/>. The pages keep their size, rotation, content, links,
    /// comments, and layers; filled form fields keep their look but are no longer editable. They take no new content
    /// and no header, footer, or page settings, but count in page numbers. When the body flow is used, they act as a
    /// page break: flow items added after them start on a new page. Each call adds one top-level bookmark holding the
    /// file's own bookmarks (WR-036).
    /// </summary>
    /// <param name="file">The loaded PDF to take pages from.</param>
    /// <param name="firstPage">The 1-based number of the first page to add, or null for the first page.</param>
    /// <param name="lastPage">The 1-based number of the last page to add, or null for the last page.</param>
    /// <param name="bookmarkTitle">The title of this call's top-level bookmark, or null to use the file's title, its
    /// file name, or "Document N".</param>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstPage"/> or <paramref name="lastPage"/> is
    /// outside the file's pages, or <paramref name="lastPage"/> comes before <paramref name="firstPage"/>.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds pages of an existing PDF after the pages added so far.")]
    public void AddPages(PdfFile file, int? firstPage = null, int? lastPage = null, string? bookmarkTitle = null)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        int first = firstPage ?? 1;
        int last = lastPage ?? file.PageCount;
        if (first < 1 || first > file.PageCount)
            throw new ArgumentOutOfRangeException(nameof(firstPage), firstPage, $"The first page must be from 1 to {file.PageCount}.");
        if (last < 1 || last > file.PageCount)
            throw new ArgumentOutOfRangeException(nameof(lastPage), lastPage, $"The last page must be from 1 to {file.PageCount}.");
        if (last < first)
            throw new ArgumentOutOfRangeException(nameof(lastPage), lastPage, "The last page must not come before the first page.");

        // Every new object is built first; the document changes only once nothing can fail.
        if (!_importedObjects.TryGetValue(file, out var shared)) shared = new Dictionary<int, int>();
        var imported = new PageImporter(file, shared).Import(first - 1, last - 1, _objects.Objects.Max(o => o.ObjectNumber) + 1);

        foreach (var obj in imported.Objects) _objects.AddObject(obj);
        foreach (var (source, copy) in imported.Shareable) shared[source] = copy;
        _importedObjects[file] = shared;

        var entries = new List<PageEntry>();
        foreach (int number in imported.PageNumbers)
        {
            var pageObject = imported.Objects.First(o => o.ObjectNumber == number);
            var entry = new PageEntry(null, null, pageObject, PageSize) { FlowStart = Body.Items.Count };
            _pages.Kids.Add(new PdfIndirectReference(objectNumber: number));
            _pageEntries.Add(entry);
            entries.Add(entry);
        }
        _imports.Add(new PageImport(file, first, last, bookmarkTitle, entries));
    }

    /// <summary>
    /// Adds page settings that override the header, footer, body margins, or side margins on the first, last, odd, or even
    /// pages (WR-032). Only the values they set apply; Last-page settings may change header and footer content only.
    /// </summary>
    /// <param name="target">The pages the settings apply to.</param>
    /// <returns>The page settings, to set their overrides.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="target"/> is not a defined value.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds page settings that override the page layout on the first, last, odd, or even pages.")]
    public PdfPageSettings AddPageSettings(PdfPageTarget target)
    {
        if (!Enum.IsDefined(typeof(PdfPageTarget), target))
            throw new ArgumentOutOfRangeException(nameof(target), target, "The target must be First, Last, Odd, or Even.");
        return AddRule(new PdfPageSettings(target, null, _rules.Count, LayoutChanged));
    }

    /// <summary>
    /// Adds page settings that override the header, footer, body margins, or side margins on one page, by its number among
    /// the saved pages (WR-032). A number past the last page never matches.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <returns>The page settings, to set their overrides.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageNumber"/> is less than 1.</exception>
    [AddedVersion("1.0.0")]
    [Description("Adds page settings that override the page layout on one page, by its number.")]
    public PdfPageSettings AddPageSettings(int pageNumber)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page numbers start at 1.");
        return AddRule(new PdfPageSettings(null, pageNumber, _rules.Count, LayoutChanged));
    }

    /// <summary>
    /// Writes a PDF document to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to which the document will be written.</param>
    [AddedVersion("0.0.3")]
    [Description("Writes a PDF document to the specified stream.")]
    public void Save(Stream stream)
    {
        PrepareDocumentForSave();
        WriteToStream(stream);
    }

    /// <summary>
    /// Writes a PDF document to the specified file path. The document is validated before the file is opened,
    /// so an invalid document leaves an existing file unchanged.
    /// </summary>
    /// <param name="path">The file path where the document will be saved.</param>
    [AddedVersion("0.0.3")]
    [Description("Writes a PDF document to the specified file path.")]
    public void Save(string path)
    {
        PrepareDocumentForSave();
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        WriteToStream(fs);
    }

    /// <summary>
    /// Asynchronously writes a PDF document to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to which the document will be written.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [AddedVersion("0.0.3")]
    [Description("Asynchronously writes a PDF document to the specified stream.")]
    public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        PrepareDocumentForSave();
        await WriteToStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes a PDF document to the specified file path. The document is validated before the
    /// file is opened, so an invalid document leaves an existing file unchanged.
    /// </summary>
    /// <param name="path">The file path where the document will be saved.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [AddedVersion("0.0.3")]
    [Description("Asynchronously writes a PDF document to the specified file path.")]
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        PrepareDocumentForSave();
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        await WriteToStreamAsync(fs, cancellationToken);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Appends an object whose syntax is already bytes, numbered after every existing object (WR-002).
    /// </summary>
    /// <param name="body">The value's bytes.</param>
    /// <param name="streamData">The stream data, or null when the object is not a stream.</param>
    /// <returns>The object, carrying its number.</returns>
    internal PdfRawObject AddRawObject(byte[] body, byte[]? streamData = null)
    {
        var raw = new PdfRawObject(_objects.Objects.Max(o => o.ObjectNumber) + 1, body, streamData);
        _objects.AddObject(raw);
        return raw;
    }

    /// <summary>
    /// Records the page size of the library's first flow page when the first flow item comes before any caller page.
    /// </summary>
    internal void FlowItemAdding()
    {
        if (_pageEntries.Count == 0 && _flowRootSize is null) _flowRootSize = PageSize;
    }

    /// <summary>
    /// Marks the layout settings as changed, so the next <c>Body.RemainingHeight</c> read plans the pages again.
    /// </summary>
    internal void LayoutChanged() => _layoutVersion++;

    /// <summary>
    /// Returns where the next flow item would land and records the read so the save can check it stays exact (WR-033).
    /// </summary>
    /// <returns>The height from the cursor to the body bottom of that page, in points.</returns>
    internal double QueryRemaining()
    {
        var roots = Roots();

        // The live plan is reused only while nothing it already planned can have changed: the layout settings, the
        // caller pages, and the page-coordinate tables on them (WR-033).
        string stamp = LivePlanStamp(roots);
        if (_livePlanner is null || _livePlannerStamp != stamp)
        {
            _livePlanner = new PagePlanner(Geometry, checkBody: true);
            _livePlannerStamp = stamp;
        }

        int page;
        double value;
        try
        {
            _livePlanner.Advance(roots, Body.Items, Body.Items.Count, roots.Count - 1);
            (page, value) = _livePlanner.Probe(Geometry(1, PageSize));
        }
        catch
        {
            // A layout error leaves the plan part-way; the next read plans again from the start.
            _livePlanner = null;
            throw;
        }

        // Each item's layout fingerprint is taken by the first read that measured it.
        for (; _signedItems < Body.Items.Count; _signedItems++)
            Body.Items[_signedItems].MeasuredSignature = PagePlanner.Signature(Body.Items[_signedItems]);

        _queries.Add(new RemainingQuery(Body.Items.Count, _pageEntries.Count, PageSize, page, value, SettingsSignature()));
        return value;
    }

    #endregion

    #region Private Methods

    private static void EnsureMargin(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The margin must be a finite number of points, zero or greater.");
    }

    private static string Describe(IEnumerable<FlowItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return string.Empty;
        string kinds = string.Join(", ", list.Select(i => i.Kind).Distinct());
        return list.Count == 1
            ? list[0].Description
            : string.Format(CultureInfo.InvariantCulture, "flow items {0} to {1} ({2})", list[0].Index + 1, list[list.Count - 1].Index + 1, kinds);
    }

    // Appends a continuation page of the root page's size, numbered after every existing object as AddPage does
    // (WR-002). Its place in /Kids, after the root page and its earlier continuation pages, is set at save.
    private void AddContinuationPage(PageEntry root)
    {
        int pageNumber = root.Builder?.PageNumber ?? _pageEntries.IndexOf(root) + 1;
        var entry = CreatePageEntry(root.PageSize, pageNumber, continuationIndex: root.Chain.Count + 1);
        root.Chain.Add(entry);
    }

    private PdfPageSettings AddRule(PdfPageSettings rule)
    {
        _rules.Add(rule);
        LayoutChanged();
        return rule;
    }

    // Checks that every Body.RemainingHeight read is still exact (WR-033), replaying the reads in order on a fresh plan.
    private void CheckQueries(IReadOnlyList<PlanRoot> roots, PagePlanner planner)
    {
        bool hasFlowRoot = _flowRoot is not null || NeedsFlowRoot();
        string settings = SettingsSignature();

        foreach (var query in _queries)
        {
            // A read taken before any page existed is checked against the page the next item really landed on.
            int rootIndex = query.CallerPages == 0 ? (roots.Count > 0 ? 0 : -1) : query.CallerPages - 1 + (hasFlowRoot ? 1 : 0);
            (int Page, double Value) now;
            if (rootIndex < 0)
            {
                now = (1, Math.Max(0, Geometry(1, query.PageSize).BodyHeight));
            }
            else
            {
                planner.Advance(roots, Body.Items, query.FlowCount, rootIndex);
                now = planner.Probe(Geometry(1, query.PageSize));
            }

            if (now.Page == query.PageNumber && Math.Abs(now.Value - query.Value) <= 1e-6) continue;

            string cause;
            if (query.Settings != settings)
            {
                cause = "the page layout settings (a margin, band height, body margin, or page settings) changed";
            }
            else
            {
                var changed = Body.Items.Take(query.FlowCount).FirstOrDefault(item => item.MeasuredSignature != PagePlanner.Signature(item));
                cause = changed is not null
                    ? changed.Description + " changed its layout"
                    : "content placed before it changed its layout";
            }

            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "Body.RemainingHeight was read as {0:0.###} pt on page {1} before flow item {2}, but {3} afterwards, so that value is no longer exact (it is now {4:0.###} pt on page {5}). Read Body.RemainingHeight after the content and settings it depends on are final.",
                query.Value,
                query.PageNumber,
                query.FlowCount + 1,
                cause,
                now.Value,
                now.Page));
        }
    }

    // Fails when the content needs more pages than Body.MaxPages allows (WR-033), naming what does not fit.
    private void CheckPageLimit(IReadOnlyList<PlannedPage> pages)
    {
        if (Body.MaxPages is not int max || pages.Count <= max) return;

        var excess = pages.Where(page => page.Number > max).ToList();
        var items = excess.SelectMany(page => page.Flow).Select(p => p.Item).Distinct().OrderBy(i => i.Index).ToList();
        double over = excess.Sum(page => page.Flow.Count == 0
            ? 0
            : page.Geometry.BodyTop - page.Flow.Min(p => p.Part?.Bottom ?? p.Top - PagePlanner.MeasureSimple(p.Item)));

        string what = items.Count > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} do not fit, {1:0.###} pt over", Describe(items), over)
            : string.Format(CultureInfo.InvariantCulture, "pages {0} to {1} come from pages added with AddPage or from tables placed at page coordinates", max + 1, pages.Count);

        throw new InvalidOperationException(string.Format(
            CultureInfo.InvariantCulture,
            "The content needs {0} pages, but Body.MaxPages is {1}: {2}. Shorten the content or raise the limit.",
            pages.Count,
            max,
            what));
    }

    private PageEntry CreatePageEntry(PdfPageSize size, int pageNumber, int continuationIndex)
    {
        // Determine object numbers for the new page and its content stream.
        int pageObjectNumber = _objects.Objects.Max(o => o.ObjectNumber) + 1;
        int contentStreamObjectNumber = pageObjectNumber + 1;

        // Create an empty content stream object. It will be populated with content just before the document is written.
        var contentStream = new PdfPageContentStream(contentStreamObjectNumber, string.Empty);

        // Create pdf page internal object
        var pageInternal = new PdfPageDictionary
        {
            Parent = new PdfIndirectReference(objectNumber: 2), // Parent is the /Pages object
            Contents = new PdfIndirectReference(objectNumber: contentStreamObjectNumber),
            MediaBox = size.ToMediaBoxString()
        };

        var pageObject = new PdfObject(objectNumber: pageObjectNumber, content: pageInternal.ToString());

        // Add the new page and content stream objects to the document body.
        _objects.AddObject(pageObject);
        _objects.AddObject(contentStream);

        // Keep the page so its dictionary can be rewritten at save (resources are only known then).
        var builder = new PdfPage(contentStream, pageNumber, continuationIndex);
        return new PageEntry(builder, pageInternal, pageObject, size);
    }

    // Every page with content maps /F1 to the document font (object 3), which all text outside HTML is drawn with, and
    // pages with HTML text also map their standard fonts (WR-038). A page must name each font its content uses: viewers
    // built on Apple's PDF engine (every browser on iPhone and iPad) skip text whose font isn't in the page's resources,
    // where desktop viewers quietly fall back to a default font (QF-26111305).
    private PdfDictionary CreateResources(IReadOnlyList<PdfImageData> images, IReadOnlyCollection<StandardFont> fonts)
    {
        var resources = new PdfDictionary();
        if (images.Count > 0)
        {
            var entries = images.Select((image, i) => $"/Im{i + 1} {new PdfIndirectReference(_imageObjects[image.ContentKey])}");
            resources.AddEntry("XObject", $"<< {string.Join(" ", entries)} >>");
        }

        var fontEntries = new[] { $"/F1 {new PdfIndirectReference(objectNumber: 3)}" }
            .Concat(fonts.Select(font => $"/{StandardFontObject.ResourceName(font)} {new PdfIndirectReference(_standardFontObjects[font])}"));
        resources.AddEntry("Font", $"<< {string.Join(" ", fontEntries)} >>");
        return resources;
    }

    // Draws a page's body flow content, then its header and footer bands (WR-031, WR-033).
    private static void DrawLayout(StringBuilder sb, PageDrawSink sink, PlannedPage page, BandContent? header, BandContent? footer, IReadOnlyDictionary<PdfTable, TablePlan> plans)
    {
        var imageName = sink.ImageName;
        foreach (var placement in page.Flow)
        {
            if (placement.Html is { } fragment)
                HtmlPainter.Draw(sb, sink, fragment, placement.Left, ((FlowHtml)placement.Item).HeadingBookmarks);
            else
                DrawFlow(sb, imageName, placement, plans);
        }

        var g = page.Geometry;
        if (header is not null) DrawSection(sb, imageName, header, g, g.HeaderTop, g.HeaderHeight);
        if (footer is not null) DrawSection(sb, imageName, footer, g, g.FooterTop, g.FooterHeight);
    }

    private static void DrawFlow(StringBuilder sb, Func<PdfImageData, string> imageName, FlowPlacement placement, IReadOnlyDictionary<PdfTable, TablePlan> plans)
    {
        if (placement.Part is TablePart part)
        {
            var table = ((FlowTable)placement.Item).Table;
            sb.Append(PdfTableRenderer.RenderPart(table, plans[table], part, imageName));
            return;
        }

        DrawItem(sb, imageName, placement.Item, placement.Left, placement.Right, placement.Top);
    }

    // Draws one flow or block item with its top edge at top, inside the area between left and right.
    private static void DrawItem(StringBuilder sb, Func<PdfImageData, string> imageName, FlowItem item, double left, double right, double top)
    {
        switch (item)
        {
            case FlowText text:
                sb.AppendLine(LayoutDrawing.Text(text.Text, text.FontSize, text.Alignment, left, right, text.X, top));
                break;
            case FlowImage image:
                var box = ImageBox.Compute(image.Image, image.Width, image.Height, image.Options);
                var element = new PdfImageElement(image.Image.Data, left + image.X + box.OffsetX, top - box.BoxHeight + box.OffsetY, box.DrawWidth, box.DrawHeight);
                sb.AppendLine(element.ToContent(imageName(image.Image.Data)));
                break;
            case FlowRectangle rectangle:
                sb.AppendLine(LayoutDrawing.Rectangle(left + rectangle.X, top - rectangle.Height, rectangle.Width, rectangle.Height, rectangle.Color));
                break;
            case FlowTable table:
                var layout = PdfTableRenderer.Layout(table.Table, 0, table.Index + 1);
                sb.Append(PdfTableRenderer.RenderAt(table.Table, layout, left + table.X, top, imageName));
                break;
            case FlowBlock block:
                double y = top;
                for (int i = 0; i < block.Children.Count; i++)
                {
                    var child = block.Children[i];
                    if (i > 0) y -= child.Spacing;
                    DrawItem(sb, imageName, child, left + block.X, right, y);
                    y -= PagePlanner.MeasureSimple(child);
                }
                break;
        }
    }

    // Collects a band's content for one page: the content added directly, then the callback's, with every band table
    // laid out. Runs before any page object exists, so a callback or layout error leaves the document unchanged (A10).
    private static BandContent? PrepareBand(PdfPageSection section, PdfPageInfo info)
    {
        if (!section.HasContent) return null;

        var items = section.Items.ToList();
        if (section.Callback is not null)
        {
            var content = new PdfSectionContent();
            section.Callback(content, info);
            items.AddRange(content.Items);
        }
        if (items.Count == 0) return null;

        int tableNumber = 0;
        var layouts = items
            .Select(item => item is SectionTable table ? PdfTableRenderer.Layout(table.Table, info.PageNumber, ++tableNumber) : null)
            .ToList();
        return new BandContent(info, items, layouts);
    }

    // Draws a band's prepared content clipped to the band (WR-031).
    private static void DrawSection(StringBuilder sb, Func<PdfImageData, string> imageName, BandContent band, PageGeometry g, double top, double height)
    {
        sb.AppendLine("q"); // Save graphics state
        sb.AppendLine($"{PdfNumber.Format(g.Left)} {PdfNumber.Format(top - height)} {PdfNumber.Format(g.ContentWidth)} {PdfNumber.Format(height)} re W n"); // Clip to the band

        for (int i = 0; i < band.Items.Count; i++)
        {
            switch (band.Items[i])
            {
                case SectionText text:
                    sb.AppendLine(LayoutDrawing.Text(FillPlaceholders(text.Text, band.Info), text.FontSize, text.Alignment, g.Left, g.Right, text.X, top - text.Y));
                    break;
                case SectionImage image:
                    var box = ImageBox.Compute(image.Image, image.Width, image.Height, image.Options);
                    var element = new PdfImageElement(image.Image.Data, g.Left + image.X + box.OffsetX, top - image.Y - box.BoxHeight + box.OffsetY, box.DrawWidth, box.DrawHeight);
                    sb.AppendLine(element.ToContent(imageName(image.Image.Data)));
                    break;
                case SectionTable table:
                    sb.Append(PdfTableRenderer.RenderAt(table.Table, band.Layouts[i]!, g.Left + table.Table.X, top - table.Table.Y, imageName));
                    break;
                case SectionRectangle rectangle:
                    sb.AppendLine(LayoutDrawing.Rectangle(g.Left + rectangle.X, top - rectangle.Y - rectangle.Height, rectangle.Width, rectangle.Height, rectangle.Color));
                    break;
            }
        }

        sb.AppendLine("Q"); // Restore graphics state
    }

    // Replaces {page} and {pages}; {{ and }} write literal braces, and other braces are kept as written.
    private static string FillPlaceholders(string text, PdfPageInfo info)
    {
        if (text.IndexOf('{') < 0 && text.IndexOf('}') < 0) return text;

        var sb = new StringBuilder(text.Length + 8);
        for (int i = 0; i < text.Length; i++)
        {
            if (string.CompareOrdinal(text, i, "{{", 0, 2) == 0 || string.CompareOrdinal(text, i, "}}", 0, 2) == 0)
            {
                sb.Append(text[i]);
                i++;
            }
            else if (string.CompareOrdinal(text, i, "{pages}", 0, 7) == 0)
            {
                sb.Append(info.PageCount.ToString(CultureInfo.InvariantCulture));
                i += 6;
            }
            else if (string.CompareOrdinal(text, i, "{page}", 0, 6) == 0)
            {
                sb.Append(info.PageNumber.ToString(CultureInfo.InvariantCulture));
                i += 5;
            }
            else
            {
                sb.Append(text[i]);
            }
        }
        return sb.ToString();
    }

    // The geometry of a page from the document settings and the page settings, without Last-page settings, which may
    // change content only (WR-030, WR-032).
    private PageGeometry Geometry(int number, PdfPageSize size) => PageGeometry.Create(size, SettingsFor(number));

    private static string MaskKey(PdfImageData mask) => "mask:" + mask.ContentKey;

    private bool NeedsFlowRoot() => (_pageEntries.Count == 0 ? Body.Items.Count : _pageEntries[0].FlowStart) > 0;

    // What the bookmark builder needs from one AddPages call: its source-to-document page map and its titles.
    private static OutlineImport OutlineImportOf(PageImport import)
    {
        var pageMap = new Dictionary<int, int>();
        for (int i = 0; i < import.Entries.Count; i++)
            pageMap[import.File.Pages[import.FirstPage - 1 + i].ObjectNumber] = import.Entries[i].PageObject.ObjectNumber;
        return new OutlineImport(
            import.File.Source,
            pageMap,
            import.File.Pages[import.FirstPage - 1].ObjectNumber,
            import.BookmarkTitle,
            import.File.Title,
            import.File.FileName);
    }

    private void PrepareDocumentForSave()
    {
        // Plan every page first. Any layout error is thrown here, before a page object is added or anything is written.
        var roots = Roots();
        var planner = new PagePlanner(Geometry, checkBody: UsesLayout) { MaxPages = Body.MaxPages };
        CheckQueries(roots, planner);
        var (planned, plans) = planner.Finish(roots, Body.Items);
        CheckPageLimit(planned);

        // Band content is built for every page before any page object exists: callbacks and band tables can fail.
        bool drawLayout = UsesLayout;
        var headers = new BandContent?[planned.Count];
        var footers = new BandContent?[planned.Count];
        if (drawLayout)
        {
            for (int i = 0; i < planned.Count; i++)
            {
                var page = planned[i];
                if (page.IsImported) continue;
                var info = new PdfPageInfo(page.Number, planned.Count);
                if (page.Geometry.HeaderHeight > 0) headers[i] = PrepareBand(SectionFor(page.Number, planned.Count, footer: false), info);
                if (page.Geometry.FooterHeight > 0) footers[i] = PrepareBand(SectionFor(page.Number, planned.Count, footer: true), info);
            }
        }

        // Materialize the pages the plan needs: the library's first flow page, then continuation pages, root by root.
        if (NeedsFlowRoot() && _flowRoot is null)
            _flowRoot = CreatePageEntry(_flowRootSize ?? PageSize, pageNumber: 1, continuationIndex: 0);
        var rootEntries = (_flowRoot is not null ? new[] { _flowRoot } : Array.Empty<PageEntry>()).Concat(_pageEntries).ToList();
        int[] used = new int[rootEntries.Count];
        foreach (var page in planned) used[page.RootIndex] = Math.Max(used[page.RootIndex], page.ChainIndex + 1);

        var visible = new List<PageEntry>();
        var unused = new List<PageEntry>();
        for (int i = 0; i < rootEntries.Count; i++)
        {
            var root = rootEntries[i];
            int chain = Math.Max(0, used[i] - 1);
            while (root.Chain.Count < chain) AddContinuationPage(root);
            if (used[i] > 0) visible.Add(root);
            else unused.Add(root);
            visible.AddRange(root.Chain.Take(chain));
            unused.AddRange(root.Chain.Skip(chain));
        }

        // Imported pages keep the content they were copied with (WR-036).
        var contents = new PdfPageContent?[visible.Count];
        var sinks = new PageDrawSink?[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            if (visible[i].Builder is not { } builder) continue;
            var page = planned[i];
            var header = headers[i];
            var footer = footers[i];
            int index = i;
            Action<StringBuilder, Func<PdfImageData, string>>? after = drawLayout
                ? (sb, name) => DrawLayout(sb, sinks[index] = new PageDrawSink(name), page, header, footer, plans)
                : null;
            contents[i] = builder.BuildContent(plans, page.Carried, after);
        }

        // Store each distinct image once per document, and each standard font HTML text uses (WR-038). Objects added by
        // an earlier save are reused.
        foreach (var content in contents)
        {
            foreach (var image in content?.Images ?? Array.Empty<PdfImageData>())
            {
                RegisterImage(image);
            }
        }
        foreach (var sink in sinks)
        {
            foreach (var font in sink?.Fonts ?? Array.Empty<StandardFont>())
            {
                RegisterStandardFont(font);
            }
        }

        // Where each element id first appears, for links that jump to it (WR-038).
        var targets = new Dictionary<string, (int PageObject, double X, double Y)>(StringComparer.Ordinal);
        for (int i = 0; i < visible.Count; i++)
        {
            foreach (var mark in sinks[i]?.Marks ?? new List<PageMark>())
            {
                if (mark.Id is not null && !targets.ContainsKey(mark.Id)) targets[mark.Id] = (visible[i].PageObject.ObjectNumber, mark.X, mark.Y);
            }
        }

        // Commit page content and rewrite each page dictionary with the images and fonts it uses and its links.
        for (int i = 0; i < visible.Count; i++)
        {
            var entry = visible[i];
            if (entry.Builder is null || contents[i] is not { } content) continue;
            entry.Builder.CommitContent(content.Data);
            entry.Page!.Resources = CreateResources(content.Images, sinks[i]?.Fonts ?? Array.Empty<StandardFont>());
            entry.Page.Annots = sinks[i] is { Links.Count: > 0 } sink
                ? LinkAnnotations.Syntax(sink.Links, id => targets.TryGetValue(id, out var target) ? target : null)
                : null;
            entry.PageObject.Content = entry.Page.ToString();
        }

        foreach (var entry in unused)
        {
            if (entry.Builder is null) continue;
            entry.Builder.CommitContent(Array.Empty<byte>());
            entry.Page!.Resources = new PdfDictionary();
            entry.Page.Annots = null;
            entry.PageObject.Content = entry.Page.ToString();
        }

        // The output declares the highest version among the imported files, and at least 1.7 (WR-036).
        _fileHeader.Version = _imports
            .Select(import => import.File.Source.Version)
            .Append("1.7")
            .OrderByDescending(version => Version.TryParse(version, out var parsed) ? parsed : new Version(0, 0))
            .First();

        // List the pages in reading order in the main pages object (object 2).
        _pages.Kids.Clear();
        _pages.Kids.AddRange(visible.Select(entry => new PdfIndirectReference(objectNumber: entry.PageObject.ObjectNumber)));
        var pagesObject = _objects.Objects.First(o => o.ObjectNumber == 2);
        pagesObject.Content = _pages.ToString();

        // Bookmarks for the imported files (WR-036; without wrappers for split parts, WR-037). Their objects are created
        // at the first save that needs them and rewritten by later saves; more are appended only when later imports add
        // bookmarks. A document with no bookmark to write gets no outline.
        // HTML heading bookmarks (WR-038) join the same outline, in page order with the imports.
        var catalog = new PdfCatalog(new PdfIndirectReference(objectNumber: 2));
        var headings = new List<(int Page, OutlineSource Source)>();
        for (int i = 0; i < visible.Count; i++)
        {
            foreach (var mark in sinks[i]?.Marks ?? new List<PageMark>())
            {
                if (mark.HeadingLevel > 0)
                    headings.Add((i, OutlineSource.ForHeading(new OutlineHeading(mark.Title ?? string.Empty, mark.HeadingLevel, visible[i].PageObject.ObjectNumber, mark.X, mark.Y))));
            }
        }
        if (_imports.Count > 0 || headings.Count > 0)
        {
            var pagePosition = new Dictionary<PageEntry, int>();
            for (int i = 0; i < visible.Count; i++) pagePosition[visible[i]] = i;
            var outlineImports = _imports
                .Select(import => (Page: import.Entries.Count > 0 && pagePosition.TryGetValue(import.Entries[0], out int p) ? p : int.MaxValue, Source: OutlineSource.ForImport(OutlineImportOf(import))))
                .ToList();

            // A stable merge: imports and headings on earlier pages first; on the same page, imports first.
            var sources = outlineImports.Select((item, order) => (item.Page, Kind: 0, Order: order, item.Source))
                .Concat(headings.Select((item, order) => (item.Page, Kind: 1, Order: order, item.Source)))
                .OrderBy(item => item.Page).ThenBy(item => item.Kind).ThenBy(item => item.Order)
                .Select(item => item.Source)
                .ToList();
            int needed = OutlineBuilder.Build(sources, index => index + 1, WrapImportBookmarks).Count;
            while (_outlineObjects.Count < needed)
            {
                var placeholder = new PdfObject(_objects.Objects.Max(o => o.ObjectNumber) + 1, string.Empty);
                _objects.AddObject(placeholder);
                _outlineObjects.Add(placeholder);
            }

            var outlines = needed == 0
                ? new List<string>()
                : OutlineBuilder.Build(sources, index => _outlineObjects[index].ObjectNumber, WrapImportBookmarks);
            for (int i = 0; i < _outlineObjects.Count; i++)
                _outlineObjects[i].Content = i < outlines.Count ? outlines[i] : "null";
            if (outlines.Count > 0) catalog.Outlines = new PdfIndirectReference(objectNumber: _outlineObjects[0].ObjectNumber);
        }
        if (_imports.Count > 0)
        {
            catalog.OptionalContent = OptionalContentMerger.Build(_imports
                .Select(import => import.File)
                .Distinct()
                .Select(file => (file.Source, (IReadOnlyDictionary<int, int>)_importedObjects[file])));
        }
        _objects.Objects.First(o => o.ObjectNumber == 1).Content = catalog.ToString();
    }

    // Appends a standard font's dictionary as a new object numbered after every existing object, once per document.
    private void RegisterStandardFont(StandardFont font)
    {
        if (_standardFontObjects.ContainsKey(font)) return;
        int number = _objects.Objects.Max(o => o.ObjectNumber) + 1;
        _objects.AddObject(new PdfObject(number, new StandardFontObject(font).ToString()));
        _standardFontObjects[font] = number;
    }

    // Appends the image, and its mask unless an identical mask is already stored, as new objects numbered after every
    // existing object, keeping object numbers contiguous in body order as the xref table requires (WR-002).
    private void RegisterImage(PdfImageData image)
    {
        if (_imageObjects.ContainsKey(image.ContentKey)) return;

        int imageNumber = _objects.Objects.Max(o => o.ObjectNumber) + 1;
        int? maskNumber = null;
        bool newMask = false;
        if (image.SoftMask is not null)
        {
            // Masks are keyed apart from images, so a mask is only ever shared with other masks.
            newMask = !_imageObjects.TryGetValue(MaskKey(image.SoftMask), out var existingMask);
            maskNumber = newMask ? imageNumber + 1 : existingMask;
        }

        _objects.AddObject(new PdfImageXObject(imageNumber, image, maskNumber));
        _imageObjects[image.ContentKey] = imageNumber;

        if (newMask)
        {
            _objects.AddObject(new PdfImageXObject(maskNumber!.Value, image.SoftMask!, softMaskObjectNumber: null));
            _imageObjects[MaskKey(image.SoftMask!)] = maskNumber.Value;
        }
    }

    // The root pages in reading order: the library's first flow page when flow content comes before any caller page,
    // then every caller page (WR-033).
    private List<PlanRoot> Roots()
    {
        var roots = new List<PlanRoot>(_pageEntries.Count + 1);
        if (NeedsFlowRoot()) roots.Add(new PlanRoot(null, _flowRoot?.PageSize ?? _flowRootSize ?? PageSize, Array.Empty<PdfTable>(), 0));
        foreach (var entry in _pageEntries)
        {
            roots.Add(entry.Builder is { } builder
                ? new PlanRoot(builder, entry.PageSize, builder.Tables.ToList(), entry.FlowStart)
                : new PlanRoot(null, entry.PageSize, Array.Empty<PdfTable>(), entry.FlowStart, IsImported: true));
        }
        return roots;
    }

    // The header or footer whose content a page shows: from the most specific matching page settings that set content,
    // Last-page settings included, or the document's (WR-032).
    private PdfPageSection SectionFor(int number, int pageCount, bool footer)
    {
        PdfPageSection section = footer ? Footer : Header;
        foreach (var rule in Ordered(number, number == pageCount))
        {
            var candidate = footer ? rule.Footer : rule.Header;
            if (candidate.HasContent) section = candidate;
        }
        return section;
    }

    // Matching page settings from least to most specific; between page settings of the same kind, the later ones come
    // last and win.
    private IEnumerable<PdfPageSettings> Ordered(int number, bool isLast) =>
        _rules.Where(rule => rule.Matches(number, isLast)).OrderBy(rule => rule.Specificity).ThenBy(rule => rule.Order);

    private EffectiveSettings SettingsFor(int number)
    {
        double headerHeight = Header.Height, footerHeight = Footer.Height;
        bool headerHidden = Header.IsHidden, footerHidden = Footer.IsHidden;
        double topGap = BodyMarginTop, bottomGap = BodyMarginBottom, left = MarginLeft, right = MarginRight;

        foreach (var rule in Ordered(number, isLast: false))
        {
            if (rule.Header.HeightSet) headerHeight = rule.Header.Height;
            if (rule.Header.HiddenSet) headerHidden = rule.Header.IsHidden;
            if (rule.Footer.HeightSet) footerHeight = rule.Footer.Height;
            if (rule.Footer.HiddenSet) footerHidden = rule.Footer.IsHidden;
            topGap = rule.BodyMarginTop ?? topGap;
            bottomGap = rule.BodyMarginBottom ?? bottomGap;
            left = rule.MarginLeft ?? left;
            right = rule.MarginRight ?? right;
        }

        return new EffectiveSettings(MarginTop, MarginBottom, left, right, headerHeight, headerHidden, footerHeight, footerHidden, topGap, bottomGap);
    }

    // Fingerprints everything the planner reads from the caller pages: each root's size and flow start, and each
    // page-coordinate table's position, width, header-row settings, and row heights (WR-033).
    private string LivePlanStamp(IReadOnlyList<PlanRoot> roots)
    {
        var sb = new StringBuilder();
        sb.Append(_layoutVersion).Append('|').Append(roots.Count);
        foreach (var root in roots)
        {
            sb.Append('|').Append(root.Size).Append(';').Append(root.FlowStart).Append(';').Append(root.Tables.Count).Append(root.IsImported ? ";i" : "");
            foreach (var table in root.Tables)
            {
                sb.Append(':')
                    .Append(PdfNumber.Format(table.X)).Append(';')
                    .Append(PdfNumber.Format(table.Y)).Append(';')
                    .Append(PdfNumber.Format(table.Width)).Append(';')
                    .Append(table.HeaderRowCount).Append(';')
                    .Append(table.RepeatHeaderRows).Append(';')
                    .Append(string.Join(",", PdfTableRenderer.Layout(table, 0, 0).RowHeights.Select(PdfNumber.Format)));
            }
        }
        return sb.ToString();
    }

    private string SettingsSignature() => string.Join(
        ";",
        PageSize.ToString(),
        PdfNumber.Format(MarginTop),
        PdfNumber.Format(MarginBottom),
        PdfNumber.Format(MarginLeft),
        PdfNumber.Format(MarginRight),
        PdfNumber.Format(Header.Height),
        Header.IsHidden.ToString(),
        PdfNumber.Format(Footer.Height),
        Footer.IsHidden.ToString(),
        PdfNumber.Format(BodyMarginTop),
        PdfNumber.Format(BodyMarginBottom),
        string.Join(",", _rules.Select(rule => rule.LayoutSignature)));

    private void WriteToStream(Stream stream)
    {
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true);

        // Keep track of object offsets to build the xref table
        var offsets = new List<long>();

        // PDF Header
        writer.WriteLine(_fileHeader.ToString());
        writer.Flush();

        // Write PDF objects from the body
        foreach (var obj in _objects.Objects)
        {
            offsets.Add(stream.Position);
            if (obj is PdfRawObject raw)
            {
                // Copied objects are bytes: write them unchanged around the text writer.
                writer.WriteLine($"{raw.ObjectNumber} {raw.GenerationNumber} obj");
                writer.Flush();
                stream.Write(raw.Body, 0, raw.Body.Length);
                writer.WriteLine();
                if (raw.StreamData is not null)
                {
                    writer.WriteLine("stream");
                    writer.Flush();
                    stream.Write(raw.StreamData, 0, raw.StreamData.Length);
                    writer.WriteLine();
                    writer.WriteLine("endstream");
                }
                writer.WriteLine("endobj");
            }
            else if (obj is PdfStreamObject streamObj)
            {
                // Custom writing for stream objects to handle byte arrays
                writer.WriteLine($"{streamObj.ObjectNumber} {streamObj.GenerationNumber} obj");
                writer.WriteLine(streamObj.GetStreamDictionary());
                writer.WriteLine("stream");
                writer.Flush();
                stream.Write(streamObj.StreamData, 0, streamObj.StreamData.Length);
                writer.WriteLine();
                writer.WriteLine("endstream");
                writer.WriteLine("endobj");
            }
            else
            {
                writer.WriteLine(obj.ToString());
            }
            writer.Flush();
        }

        // XRef position
        long xrefPosition = stream.Position;
        writer.WriteLine("xref");

        int size = offsets.Count + 1;
        writer.WriteLine($"0 {size}");

        // special entry for object 0
        writer.WriteLine("0000000000 65535 f ");

        // Write offsets
        foreach (var offset in offsets)
        {
            writer.WriteLine($"{offset:D10} 00000 n ");
        }

        // Trailer
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Root 1 0 R /Size {size} >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefPosition);
        writer.WriteLine("%%EOF");
    }

    private async Task WriteToStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Disposed asynchronously: a synchronous dispose would write and flush the trailer synchronously, which streams
        // that allow only asynchronous I/O reject (WR-037).
        await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true);

        // Keep track of object offsets to build the xref table
        var offsets = new List<long>();

        // PDF Header
        await writer.WriteLineAsync(_fileHeader.ToString().AsMemory(), cancellationToken);
        await writer.FlushAsync();

        // Write PDF objects from the body
        foreach (var obj in _objects.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets.Add(stream.Position);
            if (obj is PdfRawObject raw)
            {
                // Copied objects are bytes: write them unchanged around the text writer.
                await writer.WriteLineAsync($"{raw.ObjectNumber} {raw.GenerationNumber} obj".AsMemory(), cancellationToken);
                await writer.FlushAsync();
                await stream.WriteAsync(raw.Body, cancellationToken);
                await writer.WriteLineAsync();
                if (raw.StreamData is not null)
                {
                    await writer.WriteLineAsync("stream".AsMemory(), cancellationToken);
                    await writer.FlushAsync();
                    await stream.WriteAsync(raw.StreamData, cancellationToken);
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync("endstream".AsMemory(), cancellationToken);
                }
                await writer.WriteLineAsync("endobj".AsMemory(), cancellationToken);
            }
            else if (obj is PdfStreamObject streamObj)
            {
                // Custom writing for stream objects to handle byte arrays
                await writer.WriteLineAsync($"{streamObj.ObjectNumber} {streamObj.GenerationNumber} obj".AsMemory(), cancellationToken);
                await writer.WriteLineAsync(streamObj.GetStreamDictionary().AsMemory(), cancellationToken);
                await writer.WriteLineAsync("stream".AsMemory(), cancellationToken);
                await writer.FlushAsync();
                await stream.WriteAsync(streamObj.StreamData, cancellationToken);
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("endstream".AsMemory(), cancellationToken);
                await writer.WriteLineAsync("endobj".AsMemory(), cancellationToken);
            }
            else
            {
                await writer.WriteLineAsync(obj.ToString().AsMemory(), cancellationToken);
            }
            await writer.FlushAsync();
        }

        // XRef position
        long xrefPosition = stream.Position;
        await writer.WriteLineAsync("xref".AsMemory(), cancellationToken);

        int size = offsets.Count + 1;
        await writer.WriteLineAsync($"0 {size}".AsMemory(), cancellationToken);

        // special entry for object 0
        await writer.WriteLineAsync("0000000000 65535 f ".AsMemory(), cancellationToken);

        // Write offsets
        foreach (var offset in offsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"{offset:D10} 00000 n ".AsMemory(), cancellationToken);
        }

        // Trailer
        await writer.WriteLineAsync("trailer".AsMemory(), cancellationToken);
        await writer.WriteLineAsync($"<< /Root 1 0 R /Size {size} >>".AsMemory(), cancellationToken);
        await writer.WriteLineAsync("startxref".AsMemory(), cancellationToken);
        await writer.WriteLineAsync(xrefPosition.ToString().AsMemory(), cancellationToken);
        await writer.WriteLineAsync("%%EOF".AsMemory(), cancellationToken);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the body flow: content that stacks below the previous item and continues on new pages the library adds
    /// (WR-033), its page limit, and the remaining-space query. Its distance from the header and footer bands is
    /// set by <see cref="BodyMarginTop"/> and <see cref="BodyMarginBottom"/>.
    /// </summary>
    public PdfBodyFlow Body { get; }

    /// <summary>
    /// Gets or sets the space between the body and the footer band on every page, in points. The default is 0. Page
    /// settings can override it (<see cref="PdfPageSettings.BodyMarginBottom"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    [AddedVersion("1.0.0")]
    public double BodyMarginBottom
    {
        get => _bodyMarginBottom;
        set
        {
            Guard.NonNegative(value, nameof(value));
            _bodyMarginBottom = value;
            LayoutChanged();
        }
    }

    /// <summary>
    /// Gets or sets the space between the header band and the body on every page, in points. The default is 0. Page
    /// settings can override it (<see cref="PdfPageSettings.BodyMarginTop"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    [AddedVersion("1.0.0")]
    public double BodyMarginTop
    {
        get => _bodyMarginTop;
        set
        {
            Guard.NonNegative(value, nameof(value));
            _bodyMarginTop = value;
            LayoutChanged();
        }
    }

    /// <summary>
    /// Gets the footer band drawn on every saved page: its height, whether it is hidden, and its content (WR-031).
    /// </summary>
    public PdfPageSection Footer { get; }

    /// <summary>
    /// Gets the header band drawn on every saved page: its height, whether it is hidden, and its content (WR-031).
    /// </summary>
    public PdfPageSection Header { get; }

    /// <summary>
    /// Gets or sets the bottom margin of every page, in points: the distance from the page's bottom edge to the footer
    /// band. The default is 0 (the page's bottom edge). A table continues on a new page when its next row would pass the
    /// body bottom, which equals this margin when the footer height and body bottom margin are 0. Content placed at absolute coordinates
    /// is not moved. Margins that leave no room between them make saving any table with rows fail.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    [AddedVersion("1.0.0")]
    public double MarginBottom
    {
        get => _marginBottom;
        set
        {
            EnsureMargin(value);
            _marginBottom = value;
            LayoutChanged();
        }
    }

    /// <summary>
    /// Gets or sets the left margin of every page, in points, shared by the header, body, and footer (WR-030). Header,
    /// footer, and flow x offsets are measured from it. The default is 0. Content placed at page coordinates is not
    /// moved.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    [AddedVersion("1.0.0")]
    public double MarginLeft
    {
        get => _marginLeft;
        set
        {
            EnsureMargin(value);
            _marginLeft = value;
            LayoutChanged();
        }
    }

    /// <summary>
    /// Gets or sets the right margin of every page, in points, shared by the header, body, and footer (WR-030). Right-
    /// and center-aligned text is placed within the width between the side margins. The default is 0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    [AddedVersion("1.0.0")]
    public double MarginRight
    {
        get => _marginRight;
        set
        {
            EnsureMargin(value);
            _marginRight = value;
            LayoutChanged();
        }
    }

    /// <summary>
    /// Gets or sets the top margin of every page, in points: the distance from the page's top edge to the header band.
    /// The default is 0 (the page's top edge). A table that continues on a new page resumes at the body top, which
    /// equals this margin when the header height and body top margin are 0; on its own page it still starts at its own y-coordinate.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    [AddedVersion("1.0.0")]
    public double MarginTop
    {
        get => _marginTop;
        set
        {
            EnsureMargin(value);
            _marginTop = value;
            LayoutChanged();
        }
    }

    // Whether any part of the page layout API is in use. A document that uses none of it keeps its output (WR-030).
    private bool UsesLayout =>
        Header.Height > 0 || Header.HasContent || Footer.Height > 0 || Footer.HasContent
        || BodyMarginTop > 0 || BodyMarginBottom > 0 || Body.Items.Count > 0 || Body.MaxPages is not null
        || MarginLeft > 0 || MarginRight > 0 || _rules.Count > 0 || _queries.Count > 0;

    /// <summary>
    /// Gets or sets the default page size for the document.
    /// </summary>
    public PdfPageSize PageSize { get; set; }

    /// <summary>
    /// Gets or sets whether each <see cref="AddPages"/> call adds one top-level bookmark holding the file's bookmarks
    /// (true, WR-036), or the file's bookmarks keep their own levels with no wrapper (false, split parts, WR-037).
    /// </summary>
    internal bool WrapImportBookmarks { get; set; } = true;

    #endregion

    #region Nested Types

    /// <summary>
    /// A band's content for one page, prepared before any page object exists: the page it is for, its items in drawing
    /// order, and the layout of each band table (null for other items).
    /// </summary>
    private sealed record BandContent(PdfPageInfo Info, IReadOnlyList<SectionItem> Items, IReadOnlyList<TableLayout?> Layouts);

    /// <summary>
    /// A page's builder together with its page dictionary, the object that serializes it, and its size. A root page
    /// also lists its continuation pages in order and records where the body flow starts on it. An imported page has
    /// no builder or dictionary: its object is the copied page, and its size is the one flow pages after it get.
    /// </summary>
    private sealed record PageEntry(PdfPage? Builder, PdfPageDictionary? Page, PdfObject PageObject, PdfPageSize PageSize)
    {
        public List<PageEntry> Chain { get; } = new();

        public int FlowStart { get; set; }
    }

    /// <summary>
    /// One <see cref="AddPages"/> call: the file, the 1-based page range, the bookmark title, and the page entries it
    /// added, in order.
    /// </summary>
    private sealed record PageImport(PdfFile File, int FirstPage, int LastPage, string? BookmarkTitle, List<PageEntry> Entries);

    #endregion
}
