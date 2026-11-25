// Every N pages: one PDF split into parts of the same number of pages; the last may be shorter.
// A complete console program: it writes report.pdf, then its parts in every-n-pages.
//
// Run it:
//   1. dotnet new console -n SplitEveryNExample
//   2. cd SplitEveryNExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open every-n-pages/report-{n}.pdf in the SplitEveryNExample folder.

using UnlimitedPDF;

namespace SplitEveryNExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open the parts in every-n-pages.
        // 1 makes one file per page. The last part is shorter when the pages don't divide evenly.
        int pagesPerPart = 2;
        string outputFolder = "every-n-pages";

        // This part only makes a sample input file; use your own PDF here instead.
        WriteSampleReport("report.pdf", pageCount: 10);

        // Each part is named after the source file and its number, such as report-1.pdf, and
        // keeps the file's bookmarks that point into it. The folder is created when missing.
        var file = PdfFile.FromFile("report.pdf");
        var parts = PdfOperations.Split(file, pagesPerPart, outputFolder);

        Console.WriteLine($"Wrote {Path.GetFullPath("report.pdf")}");
        foreach (var part in parts)
        {
            string path = Path.GetFullPath(Path.Combine(outputFolder, part.FileName));
            string pages = part.FirstPage == part.LastPage
                ? $"page {part.FirstPage}"
                : $"pages {part.FirstPage}-{part.LastPage}";
            Console.WriteLine($"Wrote {path} ({pages})");
        }
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
            body.AddText("Each page has its own heading, so you can see which pages a part has.");
        }

        document.Save(path);
    }
}
