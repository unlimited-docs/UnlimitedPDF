using System.Diagnostics;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// How a picture fills its page in Images to PDF.
/// </summary>
public enum PictureFit
{
    /// <summary>
    /// The whole picture, as large as fits inside half-inch margins.
    /// </summary>
    InsideMargins,

    /// <summary>
    /// The whole picture, as large as fits on the page, edge to edge.
    /// </summary>
    EdgeToEdge,
}

/// <summary>
/// The work of each free tool, built on the library's public API so every tool can be tested without the page. Each
/// method saves in memory and returns the bytes the download hands over; a failure comes back as a message, never as an
/// exception (OWASP A10).
/// </summary>
public static class ToolOperations
{
    #region Fields and Constants

    /// <summary>
    /// The margin around a picture placed inside margins, in points (half an inch).
    /// </summary>
    public const double PictureMargin = 36;

    /// <summary>
    /// The message shown when the browser runs out of memory.
    /// </summary>
    public const string OutOfMemory = "This is too much for this device's memory. Try fewer or smaller files.";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Writes only the chosen pages of a PDF, in the order given.
    /// </summary>
    /// <param name="file">The PDF.</param>
    /// <param name="pages">The pages to keep, as typed, for example "1, 3-5".</param>
    /// <param name="fileName">The result's file name.</param>
    /// <returns>The result.</returns>
    public static Task<ToolResult> ExtractAsync(PdfFile file, string pages, string fileName) =>
        RunAsync(async () =>
        {
            var ranges = PdfPageRange.Parse(pages);
            using var output = new MemoryStream();
            await PdfOperations.ExtractAsync(file, ranges, output);
            int count = ranges.Sum(Span);
            return [new ToolPdf(fileName, output.ToArray(), count, 1, count)];
        });

    /// <summary>
    /// Converts HTML to a PDF, supplying the pictures the visitor added by file name; nothing is fetched (PR-018).
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="pictures">The added pictures, by file name.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="fileName">The result's file name.</param>
    /// <returns>The result, listing the pictures the HTML names that were not supplied.</returns>
    public static async Task<ToolResult> HtmlToPdfAsync(string html, IReadOnlyDictionary<string, byte[]> pictures, PdfPageSize pageSize, string fileName)
    {
        var leftOut = new List<string>();
        var result = await RunAsync(async () =>
        {
            var named = HtmlPictureScan.Scan(html);
            var loaded = await PdfHtml.FromStringAsync(html, new PdfHtmlLoadOptions { ResourceResolver = HtmlPictureScan.Resolver(pictures) });
            var supplied = new HashSet<string>(pictures.Keys, StringComparer.OrdinalIgnoreCase);
            leftOut.AddRange(named.Where(p => p.IsWebAddress || !supplied.Contains(p.FileName)).Select(p => p.Reference));
            using var output = new MemoryStream();
            await PdfOperations.ConvertHtmlAsync(loaded, output, new PdfHtmlConvertOptions { PageSize = pageSize });
            byte[] bytes = output.ToArray();
            int count = PdfFile.FromBytes(bytes).PageCount;
            return [new ToolPdf(fileName, bytes, count, 1, count)];
        });
        return result with { LeftOut = leftOut };
    }

    /// <summary>
    /// Makes one PDF with one picture on each page, in the order given.
    /// </summary>
    /// <param name="pictures">The pictures.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="fit">How each picture fills its page.</param>
    /// <param name="fileName">The result's file name.</param>
    /// <returns>The result.</returns>
    public static Task<ToolResult> ImagesToPdfAsync(IReadOnlyList<PdfImage> pictures, PdfPageSize pageSize, PictureFit fit, string fileName) =>
        RunAsync(async () =>
        {
            if (pictures.Count == 0) throw new ArgumentException("Add at least one picture.");
            var (width, height) = pageSize.ToDimensions();
            double margin = fit == PictureFit.InsideMargins ? PictureMargin : 0;
            var document = new PdfDocument(pageSize);
            foreach (var picture in pictures)
            {
                document.AddPage().AddImage(picture, margin, margin, width - (2 * margin), height - (2 * margin), new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
            }

            using var output = new MemoryStream();
            await document.SaveAsync(output);
            return [new ToolPdf(fileName, output.ToArray(), pictures.Count, 1, pictures.Count)];
        });

    /// <summary>
    /// Joins whole PDFs into one, in the order given.
    /// </summary>
    /// <param name="files">The PDFs.</param>
    /// <param name="fileName">The result's file name.</param>
    /// <returns>The result.</returns>
    public static Task<ToolResult> MergeAsync(IReadOnlyList<PdfFile> files, string fileName) =>
        RunAsync(async () =>
        {
            if (files.Count == 0) throw new ArgumentException("Add at least one PDF.");
            using var output = new MemoryStream();
            await PdfOperations.MergeAsync(output, files);
            int count = files.Sum(f => f.PageCount);
            return [new ToolPdf(fileName, output.ToArray(), count, 1, count)];
        });

    /// <summary>
    /// Writes a PDF without the chosen pages; the pages left keep their order.
    /// </summary>
    /// <param name="file">The PDF.</param>
    /// <param name="pages">The pages to remove, as typed, for example "2, 6".</param>
    /// <param name="fileName">The result's file name.</param>
    /// <returns>The result.</returns>
    public static Task<ToolResult> RemoveAsync(PdfFile file, string pages, string fileName) =>
        RunAsync(async () =>
        {
            var ranges = PdfPageRange.Parse(pages);
            using var output = new MemoryStream();
            await PdfOperations.RemoveAsync(file, ranges, output);
            int removed = ranges.SelectMany(r => Enumerable.Range(Math.Min(r.FirstPage, r.LastPage), Span(r))).Distinct().Count();
            int count = file.PageCount - removed;
            return [new ToolPdf(fileName, output.ToArray(), count, 1, count)];
        });

    /// <summary>
    /// Splits a PDF every <paramref name="pagesPerPart"/> pages.
    /// </summary>
    /// <param name="file">The PDF.</param>
    /// <param name="pagesPerPart">The pages in each part.</param>
    /// <param name="baseName">The parts' file name before their number, for example "handbook".</param>
    /// <returns>The result, one PDF per part.</returns>
    public static Task<ToolResult> SplitEveryAsync(PdfFile file, int pagesPerPart, string baseName) =>
        RunAsync(async () =>
        {
            var streams = new List<MemoryStream>();
            var parts = await PdfOperations.SplitAsync(file, pagesPerPart, _ => Open(streams));
            return Parts(parts, streams, baseName);
        });

    /// <summary>
    /// Splits a PDF into one part per typed page range.
    /// </summary>
    /// <param name="file">The PDF.</param>
    /// <param name="pages">The ranges, as typed, for example "1-2, 3-7".</param>
    /// <param name="baseName">The parts' file name before their number.</param>
    /// <returns>The result, one PDF per range.</returns>
    public static Task<ToolResult> SplitRangesAsync(PdfFile file, string pages, string baseName) =>
        RunAsync(async () =>
        {
            var ranges = PdfPageRange.Parse(pages);
            var streams = new List<MemoryStream>();
            var parts = await PdfOperations.SplitAsync(file, ranges, _ => Open(streams));
            return Parts(parts, streams, baseName);
        });

    #endregion

    #region Private Methods

    private static MemoryStream Open(List<MemoryStream> streams)
    {
        var stream = new MemoryStream();
        streams.Add(stream);
        return stream;
    }

    private static IReadOnlyList<ToolPdf> Parts(IReadOnlyList<PdfSplitPart> parts, List<MemoryStream> streams, string baseName)
    {
        var files = new List<ToolPdf>(parts.Count);
        for (int i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            files.Add(new ToolPdf($"{baseName}-{part.Number}.pdf", streams[i].ToArray(), Math.Abs(part.LastPage - part.FirstPage) + 1, part.FirstPage, part.LastPage));
            streams[i].Dispose();
        }

        return files;
    }

    private static async Task<ToolResult> RunAsync(Func<Task<IReadOnlyList<ToolPdf>>> work)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var files = await work();
            return new ToolResult(files, clock.Elapsed);
        }
        catch (OutOfMemoryException)
        {
            return ToolResult.Failed(OutOfMemory);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException
            or PdfFileException or PdfImageException or PdfHtmlException or PdfSplitException or IOException)
        {
            return ToolResult.Failed(ErrorText.Plain(exception));
        }
    }

    private static int Span(PdfPageRange range) => Math.Abs(range.LastPage - range.FirstPage) + 1;

    #endregion

    #endregion
}
