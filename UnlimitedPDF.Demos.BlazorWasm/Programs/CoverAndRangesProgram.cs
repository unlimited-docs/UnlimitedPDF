// Cover + ranges: a built cover page in front of the chosen page range of each file.
// A complete console program: it writes cover-and-ranges.pdf.
// It first writes the two small sample PDFs it merges: text-and-pages.pdf and tables.pdf.
//
// Run it:
//   1. dotnet new console -n CoverAndRangesExample
//   2. cd CoverAndRangesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open cover-and-ranges.pdf in the CoverAndRangesExample folder.

using UnlimitedPDF;

namespace CoverAndRangesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open cover-and-ranges.pdf.
        bool addCover = true;
        string coverTitle = "Merged documents";
        // A null first or last page means "from the start" or "to the end", such as new(..., 2, 3).
        MergeFile[] files =
        [
            new("Text and pages", "text-and-pages.pdf", null, null),
            new("Tables", "tables.pdf", null, null),
        ];

        // This part only makes sample input files to merge; use your own PDFs instead.
        WriteSamplePdf("text-and-pages.pdf", "Text and pages", 3);
        WriteSamplePdf("tables.pdf", "Tables", 2);

        // Files are loaded once with PdfFile.FromFile; AddPages copies the chosen pages.
        var loaded = files.Select(file => PdfFile.FromFile(file.Path)).ToArray();
        var document = new PdfDocument();

        if (addCover)
        {
            // The cover gets the page count of the whole merged document; imported pages keep
            // their own look.
            document.Footer.Height = 30;
            document.Footer.SetPageContent((footer, info) =>
                footer.AddText($"{info.PageCount} pages", 0, 10, 10, PdfTextAlignment.Center));
            var cover = document.AddPage();
            cover.AddText(coverTitle, 72, 720, 26);
            int line = 680;
            for (int i = 0; i < files.Length; i++)
            {
                cover.AddText($"{files[i].Name}: {Range(files[i], loaded[i])}", 72, line, 12);
                line -= 20;
            }
        }

        // Each file adds one top-level bookmark named after it.
        for (int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            document.AddPages(loaded[i], file.FirstPage, file.LastPage, bookmarkTitle: file.Name);
        }

        document.Save("cover-and-ranges.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("cover-and-ranges.pdf")}");
    }

    private static string Range(MergeFile file, PdfFile pdf)
    {
        int first = file.FirstPage ?? 1;
        int last = file.LastPage ?? pdf.PageCount;
        return first == 1 && last == pdf.PageCount
            ? $"all {last} pages"
            : $"pages {first} to {last}";
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

    private sealed record MergeFile(string Name, string Path, int? FirstPage, int? LastPage);
}
