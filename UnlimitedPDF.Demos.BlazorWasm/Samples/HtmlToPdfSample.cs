namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Converts HTML to PDF in the browser: the HTML is loaded with its images, added to a document with page numbers in
/// the footer and headings as bookmarks, and laid out.
/// </summary>
public static class HtmlToPdfSample
{
    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="images">Returns an image the HTML names, or null. The library itself never uses the network.</param>
    /// <param name="fallback">Fonts for characters the standard fonts don't have, such as Arabic or Hebrew letters, or
    /// null for none.</param>
    /// <returns>The document, and the loaded HTML with its warnings.</returns>
    public static async Task<(PdfDocument Document, PdfHtml Html)> BuildAsync(string html, Func<string, CancellationToken, Task<Stream?>> images, IReadOnlyList<PdfFont>? fallback = null)
    {
        var loaded = await PdfHtml.FromStringAsync(html, new PdfHtmlLoadOptions { ResourceResolverAsync = images });

        var document = new PdfDocument { MarginTop = 36, MarginBottom = 36, MarginLeft = 36, MarginRight = 36 };
        foreach (var font in fallback ?? Array.Empty<PdfFont>()) document.FontFallback.Add(font);
        document.Footer.Height = 18;
        document.Footer.AddText("Page {page} of {pages}", 0, 6, 8, PdfTextAlignment.Right);
        document.Body.AddHtml(loaded, headingBookmarks: true);

        // Laying the document out now fills in the layout warnings, such as clipped content.
        _ = document.Body.RemainingHeight;
        return (document, loaded);
    }
}
