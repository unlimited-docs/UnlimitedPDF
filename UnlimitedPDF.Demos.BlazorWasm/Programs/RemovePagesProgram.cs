// Remove pages: one PDF written again without the chosen pages, the pages left in their order.
// A complete console program: it writes report.pdf, then remaining-pages.pdf from it.
//
// Run it:
//   1. dotnet new console -n RemovePagesExample
//   2. cd RemovePagesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open remaining-pages.pdf in the RemovePagesExample folder.

using UnlimitedPDF;

namespace RemovePagesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open remaining-pages.pdf.
        // Page numbers and ranges separated by commas, for example "1, 3-4".
        string pages = "1-4";

        // This part only makes a sample input file; use your own PDF here instead.
        WriteSampleReport("report.pdf", pageCount: 10);

        // The pages left keep their order, their links between them, the bookmarks that point to
        // them, and the file's title and author.
        var file = PdfFile.FromFile("report.pdf");
        PdfOperations.Remove(file, PdfPageRange.Parse(pages), "remaining-pages.pdf");

        Console.WriteLine($"Wrote {Path.GetFullPath("report.pdf")}");
        Console.WriteLine($"Wrote {Path.GetFullPath("remaining-pages.pdf")}");
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
