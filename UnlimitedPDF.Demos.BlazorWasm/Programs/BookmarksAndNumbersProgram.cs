// Bookmarks & numbers: a bookmark per merged file, and page numbers that count every page.
// A complete console program: it writes bookmarks-and-numbers.pdf.
// It first writes the two small sample PDFs it merges: invoice.pdf and quotation.pdf.
//
// Run it:
//   1. dotnet new console -n BookmarksAndNumbersExample
//   2. cd BookmarksAndNumbersExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open bookmarks-and-numbers.pdf in the BookmarksAndNumbersExample folder.

using UnlimitedPDF;

namespace BookmarksAndNumbersExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open bookmarks-and-numbers.pdf.
        // {page} and {pages} count every page of the merged document.
        string footerText = "Page {page} of {pages}";
        MergeFile[] files =
        [
            new("invoice.pdf", "Invoice INV-001"),
            new("quotation.pdf", "Quotation"),
        ];

        // This part only makes sample input files to merge; use your own PDFs instead.
        WriteSamplePdf("invoice.pdf", "Invoice", 2);
        WriteSamplePdf("quotation.pdf", "Quotation", 2);

        // The footer is drawn on the built pages only; imported pages keep their own look.
        var document = new PdfDocument();
        document.Footer.Height = 30;
        document.Footer.AddText(footerText, 0, 10, 10, PdfTextAlignment.Center);

        var contents = document.AddPage();
        contents.AddText("Contents", 72, 720, 24);
        int line = 680;
        foreach (var file in files)
        {
            contents.AddText(file.BookmarkTitle, 72, line, 12);
            line -= 20;
        }

        // Each file gets one top-level bookmark with the title given; its own bookmarks are nested
        // under it.
        foreach (var file in files)
            document.AddPages(PdfFile.FromFile(file.Path), bookmarkTitle: file.BookmarkTitle);

        // Pages added after the imported ones are numbered after them.
        document.AddPage().AddText("End of document", 72, 720, 18);

        document.Save("bookmarks-and-numbers.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("bookmarks-and-numbers.pdf")}");
    }

    // Writes a simple sample PDF: a title and some text on each page, "Page x of y" in the footer.
    private static void WriteSamplePdf(string path, string title, int pageCount)
    {
        var document = new PdfDocument();
        document.Footer.Height = 30;
        document.Footer.AddText("Page {page} of {pages}", 0, 10, 10, PdfTextAlignment.Center);
        for (int page = 1; page <= pageCount; page++)
        {
            var pdfPage = document.AddPage();
            pdfPage.AddText(title, 72, 720, 24);
            pdfPage.AddText($"This is page {page} of the sample {title.ToLowerInvariant()}.",
                72, 680, 12);
        }
        document.Save(path);
    }

    private sealed record MergeFile(string Path, string BookmarkTitle);
}
