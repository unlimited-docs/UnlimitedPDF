namespace UnlimitedPDF.Demo.ConsoleApp;

/// <summary>
/// Builds the merge demo: a cover page with page numbers, pages 2 to 3 of a short report, and the whole demo invoice.
/// Both inputs are made by UnlimitedPDF here and loaded back as existing PDFs, so the demo needs no other files.
/// </summary>
public static class MergeDemo
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the merged document from the demo images in <paramref name="assetsDirectory"/>.
    /// </summary>
    /// <param name="assetsDirectory">The folder holding the demo images used by the invoice.</param>
    /// <returns>The merged document, ready to save.</returns>
    public static PdfDocument Build(string assetsDirectory)
    {
        // Existing PDFs are loaded and checked once; a loaded file can give pages to any number of documents.
        var report = PdfFile.FromBytes(ToBytes(BuildReport()));
        var invoice = PdfFile.FromBytes(ToBytes(DemoDocument.Build(assetsDirectory)));

        var document = new PdfDocument();
        document.Footer.Height = 24;
        document.Footer.AddText("Pack page {page} of {pages}", 0, 6, 9, PdfTextAlignment.Right);

        var cover = document.AddPage();
        cover.AddText("Quarterly pack", 72, 700, 24);
        cover.AddText("Report pages 2 to 3, then the invoice.", 72, 670);

        // Imported pages keep their own look: the cover's footer is not drawn on them, but they count in its numbers.
        document.AddPages(report, firstPage: 2, lastPage: 3, bookmarkTitle: "Quarterly report");
        document.AddPages(invoice, bookmarkTitle: "Invoice");
        return document;
    }

    #endregion

    #region Private Methods

    private static PdfDocument BuildReport()
    {
        var report = new PdfDocument { MarginTop = 72, MarginLeft = 72, MarginRight = 72, MarginBottom = 72 };
        string[] sections = { "Introduction", "Results", "Outlook" };
        for (int i = 0; i < sections.Length; i++)
        {
            if (i > 0) report.Body.PageBreak();
            report.Body.AddText(sections[i], fontSize: 20);
            report.Body.AddText($"Section {i + 1} of the quarterly report.", spacing: 12);
        }
        return report;
    }

    private static byte[] ToBytes(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    #endregion

    #endregion
}
