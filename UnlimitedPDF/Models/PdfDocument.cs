namespace UnlimitedPDF;

public class PdfDocument
{
    #region Fields and Constants

    private readonly PdfBody _body;
    private readonly PdfHeader _header;
    private readonly Dictionary<string, int> _imageObjects = new(StringComparer.Ordinal);
    private readonly List<PageEntry> _pageEntries = new();
    private readonly PdfPages _pages;

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
        var pageBuilder = new PdfPageBuilder(contentStream, pageNumber: _pageEntries.Count + 1);
        _pageEntries.Add(new PageEntry(pageBuilder, pageInternal, pageObject));

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

    #region Private Methods

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
        // Lay out every page first, without changing the document. Any layout error is thrown here, before
        // anything is written or committed.
        var contents = new PdfPageContent[_pageEntries.Count];
        for (int i = 0; i < _pageEntries.Count; i++)
        {
            contents[i] = _pageEntries[i].Builder.BuildContent();
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
        for (int i = 0; i < _pageEntries.Count; i++)
        {
            var entry = _pageEntries[i];
            entry.Builder.CommitContent(contents[i].Data);
            entry.Page.Resources = CreateResources(contents[i].Images);
            entry.PageObject.Content = entry.Page.ToString();
        }

        // Update the content of the main pages object (object 2) to reflect all added pages.
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
    /// Gets or sets the default page size for the document.
    /// </summary>
    public PdfPageSize PageSize { get; set; }

    #endregion

    #region Nested Types

    /// <summary>
    /// A page's builder together with its page dictionary and the object that serializes it.
    /// </summary>
    private sealed record PageEntry(PdfPageBuilder Builder, PdfPage Page, PdfObject PageObject);

    #endregion
}
