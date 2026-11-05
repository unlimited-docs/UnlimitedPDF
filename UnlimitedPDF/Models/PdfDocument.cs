using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF;

public class PdfDocument
{
    #region Fields and Constants

    private readonly PdfBody _body;
    private readonly PdfHeader _header;
    private readonly Dictionary<string, int> _imageObjects = new(StringComparer.Ordinal);
    private readonly List<PageEntry> _pageEntries = new();
    private readonly PdfPages _pages;
    private readonly Dictionary<PdfPageBuilder, PageEntry> _rootPages = new();
    private double _bottomMargin;
    private double _topMargin;

    #endregion

    #region Constructors and Finalizers

    public PdfDocument(PdfPageSize pageSize = PdfPageSize.A4)
    {
        PageSize = pageSize;
        _header = new PdfHeader();
        _body = new PdfBody();
        _pages = new PdfPages();

        // Build the core PDF document structure
        var catalog = new PdfObject(objectNumber: 1, content: new PdfCatalog(pages: new PdfIndirectReference(objectNumber: 2)).ToString());
        var pagesObject = new PdfObject (objectNumber: 2, content: _pages.ToString());

        // Define a font resource
        var font = new PdfObject(objectNumber: 3, content: new PdfFont().ToString());

        // Add all objects to the document body
        _body.AddObject(catalog);
        _body.AddObject(pagesObject);
        _body.AddObject(font);
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a new page to the PDF document and returns a page object for adding content.
    /// </summary>
    /// <returns>A <see cref="PdfPageBuilder"/> object to which content can be added.</returns>
    [AddedVersion("0.0.2")]
    [Description("Adds a new page to the PDF document and returns a page object for adding content.")]
    public PdfPageBuilder AddPage()
    {
        // Determine object numbers for the new page and its content stream.
        int pageObjectNumber = _body.Objects.Max(o => o.ObjectNumber) + 1;
        int contentStreamObjectNumber = pageObjectNumber + 1;

        // Create an empty content stream object. It will be populated with content just before the document is written.
        var contentStream = new PdfPageContentStream(contentStreamObjectNumber, string.Empty);

        // Create pdf page internal object
        var pageInternal = new PdfPage
        {
            Parent = new PdfIndirectReference(objectNumber: 2), // Parent is the /Pages object
            Contents = new PdfIndirectReference(objectNumber: contentStreamObjectNumber),
            MediaBox = PageSize.ToMediaBoxString()
        };

        var pageObject = new PdfObject(objectNumber: pageObjectNumber, content: pageInternal.ToString());

        // Add the new page and content stream objects to the document body.
        _body.AddObject(pageObject);
        _body.AddObject(contentStream);

        // Update the /Kids array in the /Pages object to include the new page.
        _pages.Kids.Add(new PdfIndirectReference(objectNumber: pageObjectNumber));

        // Create a new Page builder, associate it with the content stream, and keep the page so its
        // dictionary can be rewritten at save (resources are only known then).
        var pageBuilder = new PdfPageBuilder(contentStream, pageNumber: _pageEntries.Count + 1, document: this);
        var entry = new PageEntry(pageBuilder, pageInternal, pageObject, PageSize);
        _pageEntries.Add(entry);
        _rootPages[pageBuilder] = entry;

        return pageBuilder;
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
    /// Lays out <paramref name="table"/>, a table on <paramref name="page"/>, together with every other table on that
    /// page's caller page and its continuation pages, adding continuation pages as needed (WR-027).
    /// </summary>
    /// <param name="page">The page the table was added to.</param>
    /// <param name="table">The table.</param>
    /// <returns>The table's page plan.</returns>
    /// <exception cref="InvalidOperationException">A table on those pages cannot be laid out.</exception>
    internal TablePlan PlanTable(PdfPageBuilder page, PdfTable table)
    {
        return PlanChain(_rootPages[page]).Plans[table];
    }

    #endregion

    #region Private Methods

    private static void EnsureMargin(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The margin must be a finite number of points, zero or greater.");
    }

    // A table whose end the caller read must still lay out the same way, or content placed from that end could overlap
    // the table or land on a page that no longer holds it (WR-029).
    private static void EnsureQueriedTablesUnchanged(ChainPlan chain)
    {
        foreach (var pair in chain.Plans)
        {
            var queried = pair.Key.QueriedPlan;
            if (queried is null || queried.PlacesTableLike(pair.Value)) continue;

            var page = pair.Value.Parts[0].Page;
            int number = page.Tables.TakeWhile(table => table != pair.Key).Count() + 1;
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "Table {0} on {1} no longer lays out as it did when GetTableEnd was called. Call GetTableEnd again after changing the table or the document margins.",
                number,
                page.Description));
        }
    }

    private static PageEntry PageAt(PageEntry root, int index) => index == 0 ? root : root.Chain[index - 1];

    // Appends a continuation page of the root page's size, numbered after every existing object as AddPage does
    // (WR-002). Its place in /Kids, after the root page and its earlier continuation pages, is set at save.
    private void AddContinuationPage(PageEntry root)
    {
        int pageObjectNumber = _body.Objects.Max(o => o.ObjectNumber) + 1;
        int contentStreamObjectNumber = pageObjectNumber + 1;
        var contentStream = new PdfPageContentStream(contentStreamObjectNumber, string.Empty);

        var pageInternal = new PdfPage
        {
            Parent = new PdfIndirectReference(objectNumber: 2),
            Contents = new PdfIndirectReference(objectNumber: contentStreamObjectNumber),
            MediaBox = root.PageSize.ToMediaBoxString()
        };
        var pageObject = new PdfObject(objectNumber: pageObjectNumber, content: pageInternal.ToString());

        _body.AddObject(pageObject);
        _body.AddObject(contentStream);

        var builder = new PdfPageBuilder(contentStream, root.Builder.PageNumber, this, continuationIndex: root.Chain.Count + 1);
        root.Chain.Add(new PageEntry(builder, pageInternal, pageObject, root.PageSize));
        _rootPages[builder] = root;
    }

    // Lays out every table on the root page and its continuation pages, page by page and in the order the tables were
    // added. Tables that start on the same page share the following continuation pages, and a table on a continuation
    // page continues on the same chain (WR-027). Continuation pages are added when a table needs more than exist.
    private ChainPlan PlanChain(PageEntry root)
    {
        var plans = new Dictionary<PdfTable, TablePlan>();
        var carried = new Dictionary<PdfPageBuilder, List<(PdfTable, TablePart)>>();
        double pageHeight = root.PageSize.ToDimensions().Height;
        int pagesNeeded = 1;

        for (int index = 0; index < pagesNeeded; index++)
        {
            var page = PageAt(root, index).Builder;
            int tableNumber = 0;
            foreach (var table in page.Tables)
            {
                int number = ++tableNumber;
                var layout = PdfTableRenderer.Layout(table, page.PageNumber, number);
                var (splits, headerRows, endY) = TablePaginator.Paginate(
                    table,
                    layout,
                    pageHeight,
                    TopMargin,
                    BottomMargin,
                    () => string.Format(CultureInfo.InvariantCulture, "table {0} on {1}", number, page.Description));

                pagesNeeded = Math.Max(pagesNeeded, index + splits[splits.Count - 1].PageOffset + 1);
                while (root.Chain.Count < pagesNeeded - 1) AddContinuationPage(root);

                int start = index;
                var parts = splits
                    .Select(split => new TablePart(PageAt(root, start + split.PageOffset).Builder, split.FirstRow, split.EndRow, split.Top, split.RepeatsHeader))
                    .ToList();
                plans[table] = new TablePlan(layout, parts, headerRows, endY);

                foreach (var part in parts.Skip(1))
                {
                    if (!carried.TryGetValue(part.Page, out var list)) carried[part.Page] = list = new List<(PdfTable, TablePart)>();
                    list.Add((table, part));
                }
            }
        }

        return new ChainPlan(plans, carried, pagesNeeded);
    }

    // Pages without images keep an empty resource dictionary, exactly as before images existed.
    private PdfDictionary CreateResources(IReadOnlyList<PdfImageData> images)
    {
        var resources = new PdfDictionary();
        if (images.Count == 0) return resources;

        var entries = images.Select((image, i) => $"/Im{i + 1} {new PdfIndirectReference(_imageObjects[image.ContentKey])}");
        resources.AddEntry("XObject", $"<< {string.Join(" ", entries)} >>");
        return resources;
    }

    private static string MaskKey(PdfImageData mask) => "mask:" + mask.ContentKey;

    private void PrepareDocumentForSave()
    {
        // Lay out every table first; only continuation pages may be added. Any layout error is thrown here, before
        // anything is written or committed.
        var chains = _pageEntries.Select(PlanChain).ToList();
        foreach (var chain in chains)
        {
            EnsureQueriedTablesUnchanged(chain);
        }

        // Pages in reading order: each caller page, then the continuation pages its tables use. Continuation pages an
        // earlier save needed but this one does not are left out and kept empty, so object numbers stay contiguous.
        var visible = new List<PageEntry>();
        var unused = new List<PageEntry>();
        var chainOf = new List<ChainPlan>();
        for (int i = 0; i < _pageEntries.Count; i++)
        {
            var root = _pageEntries[i];
            int used = chains[i].PagesNeeded - 1;
            visible.Add(root);
            visible.AddRange(root.Chain.Take(used));
            unused.AddRange(root.Chain.Skip(used));
            chainOf.AddRange(Enumerable.Repeat(chains[i], used + 1));
        }

        var plans = chains.SelectMany(chain => chain.Plans).ToDictionary(pair => pair.Key, pair => pair.Value);
        var contents = new PdfPageContent[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            var builder = visible[i].Builder;
            IReadOnlyList<(PdfTable, TablePart)> carried = chainOf[i].Carried.TryGetValue(builder, out var parts)
                ? parts
                : Array.Empty<(PdfTable, TablePart)>();
            contents[i] = builder.BuildContent(plans, carried);
        }

        // Store each distinct image once per document. Objects added by an earlier save are reused.
        foreach (var content in contents)
        {
            foreach (var image in content.Images)
            {
                RegisterImage(image);
            }
        }

        // Commit page content and rewrite each page dictionary with the images it uses.
        for (int i = 0; i < visible.Count; i++)
        {
            var entry = visible[i];
            entry.Builder.CommitContent(contents[i].Data);
            entry.Page.Resources = CreateResources(contents[i].Images);
            entry.PageObject.Content = entry.Page.ToString();
        }

        foreach (var entry in unused)
        {
            entry.Builder.CommitContent(Array.Empty<byte>());
            entry.Page.Resources = new PdfDictionary();
            entry.PageObject.Content = entry.Page.ToString();
        }

        // List the pages in reading order in the main pages object (object 2).
        _pages.Kids.Clear();
        _pages.Kids.AddRange(visible.Select(entry => new PdfIndirectReference(objectNumber: entry.PageObject.ObjectNumber)));
        var pagesObject = _body.Objects.First(o => o.ObjectNumber == 2);
        pagesObject.Content = _pages.ToString();
    }

    // Appends the image, and its mask unless an identical mask is already stored, as new objects numbered after every
    // existing object, keeping object numbers contiguous in body order as the xref table requires (WR-002).
    private void RegisterImage(PdfImageData image)
    {
        if (_imageObjects.ContainsKey(image.ContentKey)) return;

        int imageNumber = _body.Objects.Max(o => o.ObjectNumber) + 1;
        int? maskNumber = null;
        bool newMask = false;
        if (image.SoftMask is not null)
        {
            // Masks are keyed apart from images, so a mask is only ever shared with other masks.
            newMask = !_imageObjects.TryGetValue(MaskKey(image.SoftMask), out var existingMask);
            maskNumber = newMask ? imageNumber + 1 : existingMask;
        }

        _body.AddObject(new PdfImageXObject(imageNumber, image, maskNumber));
        _imageObjects[image.ContentKey] = imageNumber;

        if (newMask)
        {
            _body.AddObject(new PdfImageXObject(maskNumber!.Value, image.SoftMask!, softMaskObjectNumber: null));
            _imageObjects[MaskKey(image.SoftMask!)] = maskNumber.Value;
        }
    }

    private void WriteToStream(Stream stream)
    {
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true);

        // Keep track of object offsets to build the xref table
        var offsets = new List<long>();

        // PDF Header
        writer.WriteLine(_header.ToString());
        writer.Flush();

        // Write PDF objects from the body
        foreach (var obj in _body.Objects)
        {
            offsets.Add(stream.Position);
            if (obj is PdfStreamObject streamObj)
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
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true);

        // Keep track of object offsets to build the xref table
        var offsets = new List<long>();

        // PDF Header
        await writer.WriteLineAsync(_header.ToString().AsMemory(), cancellationToken);
        await writer.FlushAsync();

        // Write PDF objects from the body
        foreach (var obj in _body.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets.Add(stream.Position);
            if (obj is PdfStreamObject streamObj)
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
    /// Gets or sets the bottom margin of every page, in points. The default is 0 (the page's bottom edge). A table
    /// continues on a new page when its next row would pass this margin. Content placed at absolute coordinates is not
    /// moved. Margins that leave no room between them make saving any table with rows fail.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double BottomMargin
    {
        get => _bottomMargin;
        set
        {
            EnsureMargin(value);
            _bottomMargin = value;
        }
    }

    /// <summary>
    /// Gets or sets the default page size for the document.
    /// </summary>
    public PdfPageSize PageSize { get; set; }

    /// <summary>
    /// Gets or sets the top margin of every page, in points. The default is 0 (the page's top edge). A table that
    /// continues on a new page resumes at this margin; on its own page it still starts at its own y-coordinate.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double TopMargin
    {
        get => _topMargin;
        set
        {
            EnsureMargin(value);
            _topMargin = value;
        }
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// The tables of a caller page and its continuation pages, laid out: each table's plan, the parts each
    /// continuation page draws for tables that started on an earlier page, and how many of the pages are used.
    /// </summary>
    private sealed record ChainPlan(
        Dictionary<PdfTable, TablePlan> Plans,
        Dictionary<PdfPageBuilder, List<(PdfTable, TablePart)>> Carried,
        int PagesNeeded);

    /// <summary>
    /// A page's builder together with its page dictionary, the object that serializes it, and its size. A caller page
    /// also lists its continuation pages in order.
    /// </summary>
    private sealed record PageEntry(PdfPageBuilder Builder, PdfPage Page, PdfObject PageObject, PdfPageSize PageSize)
    {
        public List<PageEntry> Chain { get; } = new();
    }

    #endregion
}
