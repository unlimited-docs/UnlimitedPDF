// Page ranges: one PDF split into one part per page range, such as "1-2, 3, 2-4".
// A complete console program: it writes report.pdf, then one part per range in page-ranges.
//
// Run it:
//   1. dotnet new console -n SplitRangesExample
//   2. cd SplitRangesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open page-ranges/report-{n}.pdf in the SplitRangesExample folder.

using UnlimitedPDF;

namespace SplitRangesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open the parts in page-ranges.
        // One part per range, in the order written. Ranges may overlap and leave pages out.
        string ranges = "1-2, 3, 2-4";
        string outputFolder = "page-ranges";

        // This part only makes a sample input file; use your own PDF here instead.
        WriteSampleReport("report.pdf", pageCount: 10);

        // Each part is named after the source file and its number, such as report-1.pdf. The
        // folder is created when missing, and every range is checked before anything is written.
        var file = PdfFile.FromFile("report.pdf");
        var parts = PdfOperations.Split(file, PdfPageRange.Parse(ranges), outputFolder);

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
