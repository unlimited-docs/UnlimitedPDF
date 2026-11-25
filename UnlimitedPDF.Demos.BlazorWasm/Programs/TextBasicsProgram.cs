// Text and pages: a page size, text at a chosen position and size, and several pages.
// A complete console program: it writes text-and-pages.pdf.
//
// Run it:
//   1. dotnet new console -n TextBasicsExample
//   2. cd TextBasicsExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open text-and-pages.pdf in the TextBasicsExample folder.

using UnlimitedPDF;

namespace TextBasicsExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open text-and-pages.pdf.
        PdfPageSize pageSize = PdfPageSize.A4;
        int pageCount = 2;
        string heading = "Hello from UnlimitedPDF";
        string bodyText =
            "This PDF was generated in your browser by UnlimitedPDF, a pure .NET library.";
        int fontSize = 26;
        int x = 50;
        int y = 760;

        // Coordinates are in points (1/72 inch), measured from the bottom-left corner of the page.
        var document = new PdfDocument(pageSize);
        pageCount = Math.Clamp(pageCount, 1, 100);

        for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var page = document.AddPage();

            // Text is written as typed: the library escapes \ ( and ) itself.
            page.AddText(heading, x, y, fontSize);
            page.AddText(bodyText, x, y - fontSize - 14, 12);
            page.AddText($"Page {pageNumber} of {pageCount}", x, 40, 9);
        }

        document.Save("text-and-pages.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("text-and-pages.pdf")}");
    }
}
