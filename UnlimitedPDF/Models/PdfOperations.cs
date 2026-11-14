namespace UnlimitedPDF;

/// <summary>
/// One-call operations on whole PDF files (PR-017). Each is built on <see cref="PdfFile"/> and
/// <see cref="PdfDocument"/>: <see cref="Merge(string, PdfFile[])"/> gives the same bytes as adding each file to a new
/// document with <see cref="PdfDocument.AddPages"/> and saving it, and each split part is a new document holding one
/// page range of the file (WR-037). To add a cover page, choose page ranges, or set bookmark titles, use
/// <see cref="PdfDocument"/> directly.
/// </summary>
public static class PdfOperations
{
    #region Fields and Constants

    private const string DefaultPartName = "part";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Converts loaded HTML into a PDF file at <paramref name="outputPath"/> (WR-038). The result has the same bytes as a
    /// new <see cref="PdfDocument"/> with the options' page size and margins, the HTML added to its body with
    /// <see cref="PdfBodyFlow.AddHtml"/>, and <see cref="PdfDocument.Save(string)"/>. Layout warnings are added to
    /// <see cref="PdfHtml.Warnings"/>.
    /// </summary>
    /// <param name="html">The loaded HTML.</param>
    /// <param name="outputPath">The path of the file to create or overwrite.</param>
    /// <param name="options">The page size, margins, and bookmark switch, or null for A4 with 36 pt margins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> or <paramref name="outputPath"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A layout warning occurred in strict mode; nothing is written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Converts loaded HTML into a PDF file at a path.")]
    public static void ConvertHtml(PdfHtml html, string outputPath, PdfHtmlConvertOptions? options = null)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        HtmlDocument(html, options).Save(outputPath);
    }

    /// <summary>
    /// Converts loaded HTML into a PDF written to <paramref name="output"/> from its current position (WR-038); the
    /// stream is left open. See <see cref="ConvertHtml(PdfHtml, string, PdfHtmlConvertOptions?)"/>.
    /// </summary>
    /// <param name="html">The loaded HTML.</param>
    /// <param name="output">The stream to write to.</param>
    /// <param name="options">The page size, margins, and bookmark switch, or null for A4 with 36 pt margins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A layout warning occurred in strict mode; nothing is written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Converts loaded HTML into a PDF written to a stream.")]
    public static void ConvertHtml(PdfHtml html, Stream output, PdfHtmlConvertOptions? options = null)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        HtmlDocument(html, options).Save(output);
    }

    /// <summary>
    /// Asynchronously converts loaded HTML into a PDF file at <paramref name="outputPath"/> (WR-038). See
    /// <see cref="ConvertHtml(PdfHtml, string, PdfHtmlConvertOptions?)"/>.
    /// </summary>
    /// <param name="html">The loaded HTML.</param>
    /// <param name="outputPath">The path of the file to create or overwrite.</param>
    /// <param name="options">The page size, margins, and bookmark switch, or null for A4 with 36 pt margins.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> or <paramref name="outputPath"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A layout warning occurred in strict mode; nothing is written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously converts loaded HTML into a PDF file at a path.")]
    public static Task ConvertHtmlAsync(PdfHtml html, string outputPath, PdfHtmlConvertOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        return HtmlDocument(html, options).SaveAsync(outputPath, cancellationToken);
    }

    /// <summary>
    /// Asynchronously converts loaded HTML into a PDF written to <paramref name="output"/> from its current position
    /// (WR-038); the stream is left open. See <see cref="ConvertHtml(PdfHtml, string, PdfHtmlConvertOptions?)"/>.
    /// </summary>
    /// <param name="html">The loaded HTML.</param>
    /// <param name="output">The stream to write to.</param>
    /// <param name="options">The page size, margins, and bookmark switch, or null for A4 with 36 pt margins.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the PDF is written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="PdfHtmlException">A layout warning occurred in strict mode; nothing is written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously converts loaded HTML into a PDF written to a stream.")]
    public static Task ConvertHtmlAsync(PdfHtml html, Stream output, PdfHtmlConvertOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        return HtmlDocument(html, options).SaveAsync(output, cancellationToken);
    }

    /// <summary>
    /// Joins whole PDF files, in the order given, into one file at <paramref name="outputPath"/>. Each input adds one
    /// top-level bookmark. Everything is checked before the file is opened, so a failure leaves an existing file
    /// unchanged.
    /// </summary>
    /// <param name="outputPath">The path of the merged file to create or overwrite.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outputPath"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Joins whole PDF files, in the order given, into one file at a path.")]
    public static void Merge(string outputPath, params PdfFile[] inputs)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        Build(inputs, nameof(inputs)).Save(outputPath);
    }

    /// <summary>
    /// Joins whole PDF files, in the order given, into one file written to <paramref name="output"/> from its current
    /// position. The stream is left open. Each input adds one top-level bookmark.
    /// </summary>
    /// <param name="output">The stream to write the merged file to.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Joins whole PDF files, in the order given, into one file written to a stream.")]
    public static void Merge(Stream output, params PdfFile[] inputs)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        Build(inputs, nameof(inputs)).Save(output);
    }

    /// <summary>
    /// Asynchronously joins whole PDF files, in the order given, into one file at <paramref name="outputPath"/>. Each
    /// input adds one top-level bookmark. Everything is checked before the file is opened, so a failure leaves an
    /// existing file unchanged.
    /// </summary>
    /// <param name="outputPath">The path of the merged file to create or overwrite.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outputPath"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously joins whole PDF files, in the order given, into one file at a path.")]
    public static Task MergeAsync(string outputPath, IEnumerable<PdfFile> inputs, CancellationToken cancellationToken = default)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        return Build(inputs, nameof(inputs)).SaveAsync(outputPath, cancellationToken);
    }

    /// <summary>
    /// Asynchronously joins whole PDF files, in the order given, into one file written to <paramref name="output"/>
    /// from its current position. The stream is left open. Each input adds one top-level bookmark.
    /// </summary>
    /// <param name="output">The stream to write the merged file to.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously joins whole PDF files, in the order given, into one file written to a stream.")]
    public static Task MergeAsync(Stream output, IEnumerable<PdfFile> inputs, CancellationToken cancellationToken = default)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        return Build(inputs, nameof(inputs)).SaveAsync(output, cancellationToken);
    }

    /// <summary>
    /// Splits a PDF into one part per page range, in the order given, and writes each part to the stream that
    /// <paramref name="openPart"/> returns for it. Ranges may overlap and leave pages out. Parts are written one at a
    /// time, each from its stream's current position; streams are left open. Each part keeps the file's bookmarks that
    /// point into it, with no wrapper bookmark.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="ranges">The pages of each part.</param>
    /// <param name="openPart">Returns the stream for a part; called just before that part is written.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/>, <paramref name="ranges"/>, a range, or
    /// <paramref name="openPart"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ranges"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range goes past the file's last page.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Splits a PDF into one part per page range and writes each part to a stream.")]
    public static IReadOnlyList<PdfSplitPart> Split(PdfFile file, IEnumerable<PdfPageRange> ranges, Func<PdfSplitPart, Stream> openPart)
    {
        var parts = Plan(file, ranges);
        if (openPart is null) throw new ArgumentNullException(nameof(openPart));
        return WriteToStreams(file, parts, openPart);
    }

    /// <summary>
    /// Splits a PDF into one part per page range, in the order given, and writes each part to
    /// <paramref name="outputFolder"/> as <c>&lt;name&gt;-&lt;number&gt;.pdf</c> (see <see cref="PdfSplitPart.FileName"/>).
    /// The folder is created when missing, and existing files with those names are overwritten. If a part cannot be
    /// written, the parts before it are kept and the half-written part file is removed.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="ranges">The pages of each part.</param>
    /// <param name="outputFolder">The folder to write the parts to.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/>, <paramref name="ranges"/>, a range, or
    /// <paramref name="outputFolder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ranges"/> is empty, or <paramref name="outputFolder"/> is
    /// empty or not a valid path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range goes past the file's last page.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Splits a PDF into one part per page range and writes the parts to a folder.")]
    public static IReadOnlyList<PdfSplitPart> Split(PdfFile file, IEnumerable<PdfPageRange> ranges, string outputFolder)
    {
        var parts = Plan(file, ranges);
        return WriteToFolder(file, parts, FullFolder(outputFolder), OpenFile);
    }

    /// <summary>
    /// Splits a PDF every <paramref name="pagesPerPart"/> pages, from the first page (the last part may be shorter),
    /// and writes each part to the stream that <paramref name="openPart"/> returns for it. Parts are written one at a
    /// time, each from its stream's current position; streams are left open. Each part keeps the file's bookmarks that
    /// point into it, with no wrapper bookmark.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="pagesPerPart">The number of pages in each part; 1 gives one part per page.</param>
    /// <param name="openPart">Returns the stream for a part; called just before that part is written.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="openPart"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerPart"/> is less than 1.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Splits a PDF every given number of pages and writes each part to a stream.")]
    public static IReadOnlyList<PdfSplitPart> Split(PdfFile file, int pagesPerPart, Func<PdfSplitPart, Stream> openPart)
    {
        var parts = Plan(file, pagesPerPart);
        if (openPart is null) throw new ArgumentNullException(nameof(openPart));
        return WriteToStreams(file, parts, openPart);
    }

    /// <summary>
    /// Splits a PDF every <paramref name="pagesPerPart"/> pages, from the first page (the last part may be shorter),
    /// and writes each part to <paramref name="outputFolder"/> as <c>&lt;name&gt;-&lt;number&gt;.pdf</c> (see
    /// <see cref="PdfSplitPart.FileName"/>). The folder is created when missing, and existing files with those names
    /// are overwritten. If a part cannot be written, the parts before it are kept and the half-written part file is
    /// removed.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="pagesPerPart">The number of pages in each part; 1 gives one part per page.</param>
    /// <param name="outputFolder">The folder to write the parts to.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="outputFolder"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outputFolder"/> is empty or not a valid path.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerPart"/> is less than 1.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    [AddedVersion("1.0.0")]
    [Description("Splits a PDF every given number of pages and writes the parts to a folder.")]
    public static IReadOnlyList<PdfSplitPart> Split(PdfFile file, int pagesPerPart, string outputFolder)
    {
        var parts = Plan(file, pagesPerPart);
        return WriteToFolder(file, parts, FullFolder(outputFolder), OpenFile);
    }

    /// <summary>
    /// Asynchronously splits a PDF into one part per page range, in the order given, and writes each part to the
    /// stream that <paramref name="openPart"/> returns for it. Parts are written one at a time, each from its stream's
    /// current position; streams are left open. Arguments are checked before the task starts.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="ranges">The pages of each part.</param>
    /// <param name="openPart">Returns the stream for a part; called just before that part is written.</param>
    /// <param name="cancellationToken">A token to cancel the split.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/>, <paramref name="ranges"/>, a range, or
    /// <paramref name="openPart"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ranges"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range goes past the file's last page.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    /// <exception cref="OperationCanceledException">The split was cancelled; finished parts were kept.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously splits a PDF into one part per page range and writes each part to a stream.")]
    public static Task<IReadOnlyList<PdfSplitPart>> SplitAsync(PdfFile file, IEnumerable<PdfPageRange> ranges, Func<PdfSplitPart, Stream> openPart, CancellationToken cancellationToken = default)
    {
        var parts = Plan(file, ranges);
        if (openPart is null) throw new ArgumentNullException(nameof(openPart));
        return WriteToStreamsAsync(file, parts, openPart, cancellationToken);
    }

    /// <summary>
    /// Asynchronously splits a PDF into one part per page range, in the order given, and writes each part to
    /// <paramref name="outputFolder"/> as <c>&lt;name&gt;-&lt;number&gt;.pdf</c>. The folder is created when missing,
    /// and existing files with those names are overwritten. If a part cannot be written or the split is cancelled, the
    /// parts before it are kept and the half-written part file is removed. Arguments are checked before the task
    /// starts.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="ranges">The pages of each part.</param>
    /// <param name="outputFolder">The folder to write the parts to.</param>
    /// <param name="cancellationToken">A token to cancel the split.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/>, <paramref name="ranges"/>, a range, or
    /// <paramref name="outputFolder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ranges"/> is empty, or <paramref name="outputFolder"/> is
    /// empty or not a valid path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range goes past the file's last page.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    /// <exception cref="OperationCanceledException">The split was cancelled; finished parts were kept.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously splits a PDF into one part per page range and writes the parts to a folder.")]
    public static Task<IReadOnlyList<PdfSplitPart>> SplitAsync(PdfFile file, IEnumerable<PdfPageRange> ranges, string outputFolder, CancellationToken cancellationToken = default)
    {
        var parts = Plan(file, ranges);
        return WriteToFolderAsync(file, parts, FullFolder(outputFolder), OpenFile, cancellationToken);
    }

    /// <summary>
    /// Asynchronously splits a PDF every <paramref name="pagesPerPart"/> pages, from the first page (the last part may
    /// be shorter), and writes each part to the stream that <paramref name="openPart"/> returns for it. Parts are
    /// written one at a time, each from its stream's current position; streams are left open. Arguments are checked
    /// before the task starts.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="pagesPerPart">The number of pages in each part; 1 gives one part per page.</param>
    /// <param name="openPart">Returns the stream for a part; called just before that part is written.</param>
    /// <param name="cancellationToken">A token to cancel the split.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="openPart"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerPart"/> is less than 1.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    /// <exception cref="OperationCanceledException">The split was cancelled; finished parts were kept.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously splits a PDF every given number of pages and writes each part to a stream.")]
    public static Task<IReadOnlyList<PdfSplitPart>> SplitAsync(PdfFile file, int pagesPerPart, Func<PdfSplitPart, Stream> openPart, CancellationToken cancellationToken = default)
    {
        var parts = Plan(file, pagesPerPart);
        if (openPart is null) throw new ArgumentNullException(nameof(openPart));
        return WriteToStreamsAsync(file, parts, openPart, cancellationToken);
    }

    /// <summary>
    /// Asynchronously splits a PDF every <paramref name="pagesPerPart"/> pages, from the first page (the last part may
    /// be shorter), and writes each part to <paramref name="outputFolder"/> as <c>&lt;name&gt;-&lt;number&gt;.pdf</c>.
    /// The folder is created when missing, and existing files with those names are overwritten. If a part cannot be
    /// written or the split is cancelled, the parts before it are kept and the half-written part file is removed.
    /// Arguments are checked before the task starts.
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="pagesPerPart">The number of pages in each part; 1 gives one part per page.</param>
    /// <param name="outputFolder">The folder to write the parts to.</param>
    /// <param name="cancellationToken">A token to cancel the split.</param>
    /// <returns>The parts, in the order written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="outputFolder"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outputFolder"/> is empty or not a valid path.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerPart"/> is less than 1.</exception>
    /// <exception cref="PdfSplitException">A part could not be written; the parts before it were written.</exception>
    /// <exception cref="OperationCanceledException">The split was cancelled; finished parts were kept.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously splits a PDF every given number of pages and writes the parts to a folder.")]
    public static Task<IReadOnlyList<PdfSplitPart>> SplitAsync(PdfFile file, int pagesPerPart, string outputFolder, CancellationToken cancellationToken = default)
    {
        var parts = Plan(file, pagesPerPart);
        return WriteToFolderAsync(file, parts, FullFolder(outputFolder), OpenFile, cancellationToken);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Splits a PDF every <paramref name="pagesPerPart"/> pages into <paramref name="outputFolder"/>, opening each part
    /// file with <paramref name="openFile"/>, so tests can fail or pause a write part-way (V-10).
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="pagesPerPart">The number of pages in each part.</param>
    /// <param name="outputFolder">The folder to write the parts to.</param>
    /// <param name="openFile">Opens the stream for a part file's full path.</param>
    /// <returns>The parts, in the order written.</returns>
    internal static IReadOnlyList<PdfSplitPart> Split(PdfFile file, int pagesPerPart, string outputFolder, Func<string, Stream> openFile)
    {
        var parts = Plan(file, pagesPerPart);
        return WriteToFolder(file, parts, FullFolder(outputFolder), openFile);
    }

    /// <summary>
    /// Asynchronously splits a PDF every <paramref name="pagesPerPart"/> pages into <paramref name="outputFolder"/>,
    /// opening each part file with <paramref name="openFile"/>, so tests can fail or pause a write part-way (V-10).
    /// </summary>
    /// <param name="file">The loaded file to split.</param>
    /// <param name="pagesPerPart">The number of pages in each part.</param>
    /// <param name="outputFolder">The folder to write the parts to.</param>
    /// <param name="openFile">Opens the stream for a part file's full path.</param>
    /// <param name="cancellationToken">A token to cancel the split.</param>
    /// <returns>The parts, in the order written.</returns>
    internal static Task<IReadOnlyList<PdfSplitPart>> SplitAsync(PdfFile file, int pagesPerPart, string outputFolder, Func<string, Stream> openFile, CancellationToken cancellationToken)
    {
        var parts = Plan(file, pagesPerPart);
        return WriteToFolderAsync(file, parts, FullFolder(outputFolder), openFile, cancellationToken);
    }

    #endregion

    #region Private Methods

    // The document a one-call HTML conversion saves (WR-038): exactly what a caller would build by hand.
    private static PdfDocument HtmlDocument(PdfHtml html, PdfHtmlConvertOptions? options)
    {
        if (html is null) throw new ArgumentNullException(nameof(html));
        options ??= new PdfHtmlConvertOptions();
        var document = new PdfDocument(options.PageSize)
        {
            MarginTop = options.MarginTop,
            MarginBottom = options.MarginBottom,
            MarginLeft = options.MarginLeft,
            MarginRight = options.MarginRight
        };
        foreach (var font in options.FontFallback) document.FontFallback.Add(font);
        document.Body.AddHtml(html, headingBookmarks: options.HeadingBookmarks);
        return document;
    }

    // The base of every part file name: the source file name without its extension, else "part". Characters that are
    // not allowed in a file name, including directory separators, become "_", so a part is always written straight
    // inside the output folder (A01).
    private static string BaseName(PdfFile file)
    {
        string name = file.FileName is null ? string.Empty : Path.GetFileNameWithoutExtension(file.FileName);
        if (name.Length == 0 || name == "." || name == "..") return DefaultPartName;
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar || invalid.Contains(c) ? '_' : c);
        return new string(chars.ToArray());
    }

    // The merged document: one AddPages call per input, so its bytes equal the AddPages path (WR-036).
    private static PdfDocument Build(IEnumerable<PdfFile>? inputs, string parameter)
    {
        if (inputs is null) throw new ArgumentNullException(parameter);
        var files = inputs.ToList();
        if (files.Count == 0) throw new ArgumentException("At least one file is needed.", parameter);
        if (files.Any(file => file is null)) throw new ArgumentNullException(parameter, "An input file is null.");

        var document = new PdfDocument();
        foreach (var file in files) document.AddPages(file);
        return document;
    }

    // One split part: a new document holding the part's pages, with the file's bookmarks and no wrapper (WR-037).
    private static PdfDocument BuildPart(PdfFile file, PdfSplitPart part)
    {
        var document = new PdfDocument { WrapImportBookmarks = false };
        document.AddPages(file, part.FirstPage, part.LastPage);
        return document;
    }

    // Closes a part file whose write failed and removes it. Errors while closing or removing are ignored, so the
    // failure that stopped the split is the one reported (WR-037, A08, A10).
    private static void CloseFailed(Stream? stream, string path)
    {
        if (stream is null) return;
        try
        {
            stream.Dispose();
        }
        catch (Exception)
        {
            // The part is removed below whatever closing it did; the original failure is reported.
        }
        RemoveHalfWritten(path);
    }

    // Asynchronous twin of CloseFailed.
    private static async Task CloseFailedAsync(Stream? stream, string path)
    {
        if (stream is null) return;
        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The part is removed below whatever closing it did; the original failure is reported.
        }
        RemoveHalfWritten(path);
    }

    private static PdfSplitException Failure(PdfSplitPart part, int partCount, Exception cause) => new(
        string.Create(CultureInfo.InvariantCulture,
            $"Part {part.Number} of {partCount} (pages {part.FirstPage} to {part.LastPage}) could not be written; {part.Number - 1} parts were written. {cause.Message}"),
        part.Number,
        part.Number - 1,
        cause);

    private static string FullFolder(string? outputFolder)
    {
        if (outputFolder is null) throw new ArgumentNullException(nameof(outputFolder));
        if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("The output folder must not be empty.", nameof(outputFolder));
        try
        {
            return Path.GetFullPath(outputFolder);
        }
        catch (Exception exception) when (exception is NotSupportedException or PathTooLongException or ArgumentException)
        {
            throw new ArgumentException("The output folder is not a valid path.", nameof(outputFolder), exception);
        }
    }

    // Only the caller's own token cancels a split; any other exception, including a cancellation raised by a callback
    // or a stream for its own reasons, is a failure of the part (WR-037).
    private static bool IsCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private static Stream OpenFile(string path) => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

    // Numbers the parts and names them <base>-<number>.pdf, the number zero-padded to the part count's width (WR-037).
    private static List<PdfSplitPart> Parts(PdfFile file, List<(int First, int Last)> ranges)
    {
        string baseName = BaseName(file);
        int width = ranges.Count.ToString(CultureInfo.InvariantCulture).Length;
        return ranges
            .Select((range, index) =>
            {
                string number = (index + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
                return new PdfSplitPart(index + 1, range.First, range.Last, baseName + "-" + number + ".pdf");
            })
            .ToList();
    }

    // The parts of a range split, in the order given. Every range is checked before anything is written.
    private static List<PdfSplitPart> Plan(PdfFile? file, IEnumerable<PdfPageRange>? ranges)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (ranges is null) throw new ArgumentNullException(nameof(ranges));

        var list = ranges.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one page range is needed.", nameof(ranges));
        for (int i = 0; i < list.Count; i++)
        {
            var range = list[i];
            if (range is null)
                throw new ArgumentNullException(nameof(ranges), string.Create(CultureInfo.InvariantCulture, $"Page range {i + 1} is null."));
            if (range.LastPage > file.PageCount)
                throw new ArgumentOutOfRangeException(nameof(ranges), range.ToString(),
                    string.Create(CultureInfo.InvariantCulture, $"Page range {i + 1} ({range}) goes past the file's last page, {file.PageCount}."));
        }
        return Parts(file, list.Select(range => (range.FirstPage, range.LastPage)).ToList());
    }

    // The parts of an every-N split. Positions are counted as long so a very large pagesPerPart cannot overflow.
    private static List<PdfSplitPart> Plan(PdfFile? file, int pagesPerPart)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (pagesPerPart < 1)
            throw new ArgumentOutOfRangeException(nameof(pagesPerPart), pagesPerPart, "Each part must have at least one page.");

        var ranges = new List<(int First, int Last)>();
        for (long first = 1; first <= file.PageCount; first += pagesPerPart)
            ranges.Add(((int)first, (int)Math.Min(first + pagesPerPart - 1, file.PageCount)));
        return Parts(file, ranges);
    }

    // Deletes the half-written part this call created; an error here must not hide the failure that caused it.
    private static void RemoveHalfWritten(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static IReadOnlyList<PdfSplitPart> WriteToFolder(PdfFile file, List<PdfSplitPart> parts, string folder, Func<string, Stream> openFile)
    {
        Directory.CreateDirectory(folder);
        foreach (var part in parts)
        {
            string path = Path.Combine(folder, part.FileName);
            Stream? stream = null;
            try
            {
                stream = openFile(path);
                BuildPart(file, part).Save(new PdfPartStream(stream));
                stream.Dispose();
            }
            catch (Exception exception)
            {
                // The synchronous overloads take no token, so every exception here is a failure of the part.
                CloseFailed(stream, path);
                throw Failure(part, parts.Count, exception);
            }
        }
        return parts;
    }

    private static async Task<IReadOnlyList<PdfSplitPart>> WriteToFolderAsync(PdfFile file, List<PdfSplitPart> parts, string folder, Func<string, Stream> openFile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(folder);
        foreach (var part in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(folder, part.FileName);
            Stream? stream = null;
            try
            {
                stream = openFile(path);
                await BuildPart(file, part).SaveAsync(new PdfPartStream(stream), cancellationToken).ConfigureAwait(false);
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await CloseFailedAsync(stream, path).ConfigureAwait(false);
                if (IsCancellation(exception, cancellationToken)) throw;
                throw Failure(part, parts.Count, exception);
            }
        }
        return parts;
    }

    private static IReadOnlyList<PdfSplitPart> WriteToStreams(PdfFile file, List<PdfSplitPart> parts, Func<PdfSplitPart, Stream> openPart)
    {
        foreach (var part in parts)
        {
            try
            {
                var stream = openPart(part) ?? throw new InvalidOperationException("The stream for the part is null.");
                BuildPart(file, part).Save(new PdfPartStream(stream));
            }
            catch (Exception exception)
            {
                // The synchronous overloads take no token, so every exception here is a failure of the part.
                throw Failure(part, parts.Count, exception);
            }
        }
        return parts;
    }

    private static async Task<IReadOnlyList<PdfSplitPart>> WriteToStreamsAsync(PdfFile file, List<PdfSplitPart> parts, Func<PdfSplitPart, Stream> openPart, CancellationToken cancellationToken)
    {
        foreach (var part in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = openPart(part) ?? throw new InvalidOperationException("The stream for the part is null.");
                await BuildPart(file, part).SaveAsync(new PdfPartStream(stream), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
            {
                throw Failure(part, parts.Count, exception);
            }
        }
        return parts;
    }

    #endregion

    #endregion
}
