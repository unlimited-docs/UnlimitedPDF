// Extract pages: the chosen pages of one PDF, in the order written, copied into one new PDF.
// A complete console program: it writes report.pdf, then extracted-pages.pdf from it.
//
// Run it:
//   1. dotnet new console -n ExtractPagesExample
//   2. cd ExtractPagesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open extracted-pages.pdf in the ExtractPagesExample folder.

using UnlimitedPDF;

namespace ExtractPagesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open extracted-pages.pdf.
        // Page numbers and ranges separated by commas. A page can appear more than once, and
        // "9-7" gives pages 9, 8, 7.
        string pages = "4, 1-2";

        // This part only makes a sample input file; use your own PDF here instead.
        WriteSampleReport("report.pdf", pageCount: 10);

        // The new PDF keeps the pages' links between them, their bookmarks, and the file's title
        // and author, and carries only the fonts and images those pages use.
        var file = PdfFile.FromFile("report.pdf");
        PdfOperations.Extract(file, PdfPageRange.Parse(pages), "extracted-pages.pdf");

        Console.WriteLine($"Wrote {Path.GetFullPath("report.pdf")}");
        Console.WriteLine($"Wrote {Path.GetFullPath("extracted-pages.pdf")}");
    }

    // A report of several pages, each with a heading, some text and "Page n of m" in the footer.
    private static void WriteSampleReport(string path, int pageCount)
    {
        var document = new PdfDocument();
        document.Footer.Height = 24;
        document.Footer.AddText("Page {page} of {pages}", 0, 9, 9, PdfTextAlignment.Right);

        var body = document.Body;
        for (int page = 1; page <= pageCount; page++)
        {
            if (page > 1) body.PageBreak();
            body.AddText($"Section {page}", fontSize: 20);
            body.AddText($"This is page {page} of the sample report.", spacing: 12);
            body.AddText("Each page has its own heading, so you can see which pages were kept.");
        }

        document.Save(path);
    }
}
