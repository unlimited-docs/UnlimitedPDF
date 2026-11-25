// Pick pages: the chosen pages of one PDF, in the order written, such as "3, 1-2".
// A complete console program: it writes picked-pages.pdf.
// It first writes report.pdf, a small sample PDF to pick the pages from.
//
// Run it:
//   1. dotnet new console -n PickPagesExample
//   2. cd PickPagesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open picked-pages.pdf in the PickPagesExample folder.

using UnlimitedPDF;

namespace PickPagesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: the file and the pages to take from it, in order: page numbers and
        // ranges separated by commas. Change them and run again.
        string path = "report.pdf";
        string pages = "3, 1-2";

        // This part only makes a sample input file; use your own PDF instead.
        WriteSamplePdf("report.pdf", "Report", 5);

        var file = PdfFile.FromFile(path);
        var ranges = PdfPageRange.Parse(pages);

        // Each range is one AddPages call: its pages are added in order, under one bookmark.
        var document = new PdfDocument();
        foreach (var range in ranges)
        {
            string title = range.FirstPage == range.LastPage
                ? $"Page {range.FirstPage}"
                : $"Pages {range}";
            document.AddPages(file, range.FirstPage, range.LastPage, bookmarkTitle: title);
        }

        document.Save("picked-pages.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("picked-pages.pdf")}");
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
}
