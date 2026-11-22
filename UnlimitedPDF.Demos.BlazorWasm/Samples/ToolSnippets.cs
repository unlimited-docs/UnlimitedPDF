using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The C# each free tool shows, as methods that compile. A tool page shows the lines between a
/// <c>// snippet:&lt;name&gt;</c> marker and the <c>// end</c> after it, with the visitor's file names and choices put
/// in place of the literals here (PR-012), so a sample that stops compiling breaks the build.
/// </summary>
public static class ToolSnippets
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Pages of a PDF copied into a new PDF.
    /// </summary>
    /// <returns>A task that completes when the PDF is written.</returns>
    public static async Task ExtractPages()
    {
        // snippet:extract-pages
        var file = await PdfFile.FromFileAsync("contract.pdf");
        await PdfOperations.ExtractAsync(file, PdfPageRange.Parse("1, 3-5"), "contract-extract.pdf");
        // end
    }

    /// <summary>
    /// An HTML file converted, with the pictures beside it.
    /// </summary>
    /// <returns>A task that completes when the PDF is written.</returns>
    public static async Task HtmlToPdf()
    {
        // snippet:html-to-pdf
        var html = await PdfHtml.FromFileAsync("invoice.html", new PdfHtmlLoadOptions { BaseFolder = "." });
        await PdfOperations.ConvertHtmlAsync(html, "invoice.pdf", new PdfHtmlConvertOptions { PageSize = PdfPageSize.A4 });
        // end
    }

    /// <summary>
    /// Pictures placed one a page.
    /// </summary>
    /// <returns>A task that completes when the PDF is written.</returns>
    public static async Task ImagesToPdf()
    {
        // snippet:images-to-pdf
        var document = new PdfDocument(PdfPageSize.A4);
        foreach (var path in new[] { "receipt-1.jpg", "receipt-2.jpg", "id-card.png" })
        {
            document.AddPage().AddImage(PdfImage.FromFile(path), 36, 36, 523.28, 769.89,
                new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
        }
        await document.SaveAsync("pictures.pdf");
        // end
    }

    /// <summary>
    /// Whole PDFs joined in order.
    /// </summary>
    /// <returns>A task that completes when the PDF is written.</returns>
    public static async Task Merge()
    {
        // snippet:merge
        var files = new List<PdfFile>
        {
            await PdfFile.FromFileAsync("q1-report.pdf"),
            await PdfFile.FromFileAsync("q2-report.pdf"),
        };
        await PdfOperations.MergeAsync("merged.pdf", files);
        // end
    }

    /// <summary>
    /// Chosen pages removed.
    /// </summary>
    /// <returns>A task that completes when the PDF is written.</returns>
    public static async Task RemovePages()
    {
        // snippet:remove-pages
        var file = await PdfFile.FromFileAsync("scan.pdf");
        await PdfOperations.RemoveAsync(file, PdfPageRange.Parse("2, 6"), "scan-trimmed.pdf");
        // end
    }

    /// <summary>
    /// A PDF split every few pages.
    /// </summary>
    /// <returns>A task that completes when the parts are written.</returns>
    public static async Task SplitEvery()
    {
        // snippet:split-every
        var file = await PdfFile.FromFileAsync("handbook.pdf");
        await PdfOperations.SplitAsync(file, 4, "parts");
        // end
    }

    /// <summary>
    /// A PDF split by page ranges.
    /// </summary>
    /// <returns>A task that completes when the parts are written.</returns>
    public static async Task SplitRanges()
    {
        // snippet:split-ranges
        var file = await PdfFile.FromFileAsync("handbook.pdf");
        await PdfOperations.SplitAsync(file, PdfPageRange.Parse("1-2, 3-7"), "parts");
        // end
    }

    #endregion

    #endregion
}
