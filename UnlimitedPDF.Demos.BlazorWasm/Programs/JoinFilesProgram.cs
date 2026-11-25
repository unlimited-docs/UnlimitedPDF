// Join files: whole PDFs joined in one call, in the order given, each under its own bookmark.
// A complete console program: it writes joined.pdf.
// It first writes the two small sample PDFs it joins: text-and-pages.pdf and tables.pdf.
//
// Run it:
//   1. dotnet new console -n JoinFilesExample
//   2. cd JoinFilesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open joined.pdf in the JoinFilesExample folder.

using UnlimitedPDF;

namespace JoinFilesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: the files to join, in this order. Change the order and run again.
        string[] paths = ["text-and-pages.pdf", "tables.pdf"];

        // This part only makes sample input files to join; use your own PDFs instead.
        WriteSamplePdf("text-and-pages.pdf", "Text and pages", 3);
        WriteSamplePdf("tables.pdf", "Tables", 2);

        // Each file is loaded once. Merge joins the whole files, in order, into a new PDF and gives
        // each file one top-level bookmark that holds the file's own bookmarks.
        PdfFile[] files = paths.Select(path => PdfFile.FromFile(path)).ToArray();
        PdfOperations.Merge("joined.pdf", files);
        Console.WriteLine($"Wrote {Path.GetFullPath("joined.pdf")}");
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
            pdfPage.AddText($"This is page {page} of the sample {title.ToLowerInvariant()} file.",
                72, 680, 12);
        }
        document.Save(path);
    }
}
