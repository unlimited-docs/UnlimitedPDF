// Headers and footers: a multi-page report with page numbers and first- and last-page settings.
// A complete console program: it writes headers-and-footers.pdf.
//
// Run it:
//   1. dotnet new console -n HeadersFootersExample
//   2. cd HeadersFootersExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open headers-and-footers.pdf in the HeadersFootersExample folder.

using UnlimitedPDF;

namespace HeadersFootersExample;

// The body flow creates pages as the content needs them. Each page has a header and a footer
// with "Page n of m"; page settings give the first page its own header and the last page its own
// footer; each section heading stays with its table; and a sign-off block stays together at the
// end.
public static class Program
{
    private static readonly PdfColor Accent = new(0.0, 0.67, 0.55);
    private static readonly PdfColor HeaderTint = new(0.88, 0.95, 0.93);

    public static void Main()
    {
        // The sample data: change it, run again, and open headers-and-footers.pdf.
        string title = "Regional sales report";
        int sectionCount = 5;
        int rowsPerSection = 12;
        bool differentFirstPage = true;
        int maxPages = 0;
        string[] regions = ["North", "South", "East", "West", "Central", "Coastal"];

        var document = new PdfDocument
        {
            MarginTop = 30,
            MarginBottom = 24,
            MarginLeft = 50,
            MarginRight = 50,
        };

        // Every page: a title and a rule in the header, the page number in the footer.
        document.Header.Height = 36;
        document.Header.AddText(title, 0, 6, 12);
        document.Header.AddText("Confidential", 0, 8, 9, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 34, 495, 1.5, Accent);
        document.Footer.Height = 24;
        document.Footer.AddRectangle(0, 0, 495, 0.5, PdfColor.Black);
        document.Footer.AddText("Page {page} of {pages}", 0, 9, 9, PdfTextAlignment.Right);
        document.BodyMarginTop = 16;
        document.BodyMarginBottom = 12;

        // Page settings: a taller title on the first page, a closing note on the last page.
        if (differentFirstPage)
        {
            var first = document.AddPageSettings(PdfPageTarget.First);
            first.Header.Height = 70;
            first.Header.AddText(title, 0, 8, 22, PdfTextAlignment.Center);
            first.Header.AddText(
                "Prepared by the finance team", 0, 40, 10, PdfTextAlignment.Center);
            first.Header.AddRectangle(0, 66, 495, 2, Accent);
        }

        var last = document.AddPageSettings(PdfPageTarget.Last);
        last.Footer.AddRectangle(0, 0, 495, 0.5, PdfColor.Black);
        last.Footer.AddText("End of report", 0, 9, 9);
        last.Footer.AddText("Page {page} of {pages}", 0, 9, 9, PdfTextAlignment.Right);

        // The most pages the report may have, or 0 for no limit. A report that needs more pages
        // fails to save and names the content that does not fit.
        if (maxPages > 0) document.Body.MaxPages = maxPages;

        var body = document.Body;
        int sections = Math.Clamp(sectionCount, 1, 30);
        int rows = Math.Clamp(rowsPerSection, 1, 100);
        for (int s = 0; s < sections; s++)
        {
            // The heading stays on the page of the table's first row (keepWithNext).
            string region = regions[s % regions.Length];
            body.AddText(
                $"{s + 1}. {region} region",
                spacing: s == 0 ? 0 : 18,
                fontSize: 13,
                keepWithNext: true);

            var table = body.AddTable(495, spacing: 6);
            table.SetColumnWidths(60, 255, 90, 90);
            table.HeaderRowCount = 1;
            table.RepeatHeaderRows = true;
            var header = table.AddRow();
            string[] headings = ["Month", "Product", "Units", "Revenue"];
            for (int column = 0; column < headings.Length; column++)
            {
                header.GetCell(column).Text = headings[column];
                header.GetCell(column).BackgroundColor = HeaderTint;
            }

            for (int r = 0; r < rows; r++)
            {
                var row = table.AddRow();
                row.GetCell(0).Text = $"M{r % 12 + 1:00}";
                row.GetCell(1).Text = $"{region} product {r + 1}";
                Right(row.GetCell(2), (40 + (s * 7 + r * 3) % 60).ToString());
                Right(row.GetCell(3), ((40 + (s * 7 + r * 3) % 60) * 12.5m).ToString("F2"));
            }
        }

        // The sign-off block moves to a new page as one piece when it does not fit.
        var signOff = body.AddBlock(spacing: 24);
        signOff.AddRectangle(495, 1, PdfColor.Black);
        signOff.AddText("Reviewed and approved", spacing: 8, fontSize: 11);
        signOff.AddText("Name: ______________________", spacing: 14);
        signOff.AddText("Date: ______________________", spacing: 10);

        document.Save("headers-and-footers.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("headers-and-footers.pdf")}");
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }
}
