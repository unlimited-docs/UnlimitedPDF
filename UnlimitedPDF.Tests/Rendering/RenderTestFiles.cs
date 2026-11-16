using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// Builds small PDFs with a given page content for renderer tests, and renders them.
/// </summary>
internal static class RenderTestFiles
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a one-page PDF (595 by 842) whose content stream is <paramref name="content"/>.
    /// </summary>
    /// <param name="content">The content stream, uncompressed.</param>
    /// <param name="resources">The page's resources dictionary.</param>
    /// <param name="extraObjects">Further objects from number 5, for resources to refer to.</param>
    /// <param name="pageEntries">Further page dictionary entries.</param>
    public static byte[] OnePage(string content, string resources = "<< >>", IEnumerable<(int Number, string Dictionary, byte[]? Data)>? extraObjects = null, string pageEntries = "")
    {
        var builder = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources {resources} /Contents 4 0 R {pageEntries}>>")
            .AddStream(4, string.Empty, Encoding.Latin1.GetBytes(content));
        foreach (var (number, dictionary, data) in extraObjects ?? Enumerable.Empty<(int, string, byte[]?)>())
        {
            if (data is null) builder.AddObject(number, dictionary);
            else builder.AddStream(number, dictionary, data);
        }
        return builder.ToBytes();
    }

    /// <summary>
    /// Renders every page of <paramref name="pdf"/>.
    /// </summary>
    public static List<PageRenderResult> RenderAll(byte[] pdf, PageRenderLimits? limits = null)
    {
        var file = PdfFile.FromBytes(pdf);
        return Enumerable.Range(0, file.PageCount).Select(i => PdfPageRenderer.Render(file, i, $"t{i}-", limits)).ToList();
    }

    /// <summary>
    /// Asserts that every page of <paramref name="pdf"/> is drawn and that its text layer equals the page text the
    /// PDF's own text mapping gives (26111701 V-04).
    /// </summary>
    public static void AssertDrawnWithTheSameText(byte[] pdf)
    {
        var expected = PdfInspector.Read(pdf).PageText();
        var pages = RenderAll(pdf);
        Assert.Equal(expected.Count, pages.Count);
        for (int i = 0; i < pages.Count; i++)
        {
            Assert.True(pages[i].IsDrawn, $"Page {i + 1}: {pages[i].Reason}");
            Assert.Equal(expected[i], string.Join("\n", pages[i].TextLines));
        }
    }

    /// <summary>
    /// Renders the first page of <paramref name="pdf"/>.
    /// </summary>
    public static PageRenderResult RenderFirst(byte[] pdf, PageRenderLimits? limits = null) =>
        PdfPageRenderer.Render(PdfFile.FromBytes(pdf), 0, "t-", limits);

    #endregion

    #endregion
}
