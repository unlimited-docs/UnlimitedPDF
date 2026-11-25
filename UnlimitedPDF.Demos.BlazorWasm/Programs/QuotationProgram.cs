// Quotation: a services quote with a first-page header, a last-page footer, and kept terms.
// A complete console program: it writes quotation.pdf.
//
// Run it:
//   1. dotnet new console -n QuotationExample
//   2. cd QuotationExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the QuotationExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/quotation-logo.png
//   6. dotnet run
// Then open quotation.pdf in the QuotationExample folder.

using UnlimitedPDF;

namespace QuotationExample;

public static class Program
{
    private const double Width = 495;

    private static readonly PdfColor HeaderTint = new(0.88, 0.92, 0.98);
    private static readonly PdfColor Indigo = new(0.17, 0.23, 0.55);
    private static readonly PdfColor Sky = new(0.23, 0.65, 0.88);
    private static readonly PdfColor TotalTint = new(0.86, 0.93, 0.99);

    public static void Main()
    {
        // The sample data: change it, run again, and open quotation.pdf.
        // All names and numbers are fictional.
        string clientName = "Priya Menon";
        string clientCompany = "Coastal Retail Pvt. Ltd.";
        string clientAddress = "9 Harbour Street, Kochi, KL 682001";
        string quoteNumber = "Q-2026-117";
        DateTime quoteDate = DateTime.Today;
        int validDays = 30;
        decimal taxPercent = 18m;
        ServiceLine[] services =
        [
            new("Network assessment (on site)", 1, 1800m),
            new("Firewall setup and hardening", 2, 950m),
            new("Managed backup, per month", 12, 240m),
            new("Staff security training (half day)", 1, 600m),
        ];

        var document = new PdfDocument
        {
            MarginTop = 32,
            MarginBottom = 24,
            MarginLeft = 50,
            MarginRight = 50,
        };
        var logo = PdfImage.FromFile("quotation-logo.png");
        var fit = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        DateTime validUntil = quoteDate.AddDays(Math.Max(1, validDays));

        // Pages after the first: a slim header with the logo and the quote number.
        document.Header.Height = 44;
        document.Header.AddImage(logo, 0, 0, 120, 36, fit);
        document.Header.AddText(
            $"Quotation {quoteNumber} (continued)", 0, 14, 10, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 41, Width, 1.5, Sky);
        document.Footer.Height = 22;
        document.Footer.AddText("Summit IT Services  |  Quotation " + quoteNumber, 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);

        // The first page: a taller header with the quote details.
        var first = document.AddPageSettings(PdfPageTarget.First);
        first.Header.Height = 96;
        first.Header.AddImage(logo, 0, 0, 165, 55, fit);
        first.Header.AddText("QUOTATION", 0, 4, 24, PdfTextAlignment.Right);
        first.Header.AddText($"Quote no. {quoteNumber}", 0, 40, 10, PdfTextAlignment.Right);
        first.Header.AddText(
            $"Date {quoteDate:dd MMM yyyy}  |  Valid until {validUntil:dd MMM yyyy}",
            0, 56, 10, PdfTextAlignment.Right);
        first.Header.AddRectangle(0, 92, Width, 2, Indigo);

        // The last page: a closing line in the footer.
        var last = document.AddPageSettings(PdfPageTarget.Last).Footer;
        last.AddText("Thank you for your business", 0, 8, 8, PdfTextAlignment.Center);
        last.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);

        document.BodyMarginTop = 18;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        body.AddText("Prepared for", fontSize: 9);
        body.AddText(clientName, spacing: 2, fontSize: 12);
        body.AddText(clientCompany, spacing: 2, fontSize: 10);
        body.AddText(clientAddress, spacing: 2, fontSize: 10);

        var table = body.AddTable(Width, spacing: 16);
        table.SetColumnWidths(255, 50, 90, 100);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        var header = table.AddRow();
        string[] headings = ["Service", "Qty", "Rate", "Amount"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        decimal subtotal = 0;
        foreach (var line in services)
        {
            decimal amount = line.Quantity * line.UnitPrice;
            subtotal += amount;
            var row = table.AddRow();
            row.GetCell(0).Text = line.Description;
            Right(row.GetCell(1), line.Quantity.ToString());
            Right(row.GetCell(2), line.UnitPrice.ToString("F2"));
            Right(row.GetCell(3), amount.ToString("F2"));
        }

        decimal tax = subtotal * taxPercent / 100m;
        var totals = body.AddBlock(spacing: 12);
        var totalsTable = totals.AddTable(220, x: 275);
        totalsTable.SetColumnWidths(120, 100);
        AddTotal(totalsTable, "Subtotal", subtotal, highlight: false);
        AddTotal(totalsTable, $"Tax {taxPercent:0.##}%", tax, highlight: false);
        AddTotal(totalsTable, "Total", subtotal + tax, highlight: true);

        // The heading stays on the page of the terms, and the terms stay with the signature.
        body.AddText("Terms and acceptance", spacing: 24, fontSize: 12, keepWithNext: true);
        var terms = body.AddBlock(spacing: 6);
        terms.AddText($"1. Prices are valid until {validUntil:dd MMM yyyy}.", fontSize: 9);
        terms.AddText(
            "2. 50% is invoiced on acceptance and 50% on completion; payment is due in 15 days.",
            spacing: 3, fontSize: 9);
        terms.AddText(
            "3. Work starts within 10 working days of acceptance.", spacing: 3, fontSize: 9);
        terms.AddText(
            "Accepted by: ____________________          Date: ______________",
            spacing: 28, fontSize: 10);

        document.Save("quotation.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("quotation.pdf")}");
    }

    private static void AddTotal(PdfTable table, string label, decimal value, bool highlight)
    {
        var row = table.AddRow();
        row.GetCell(0).Text = label;
        Right(row.GetCell(1), value.ToString("F2"));
        if (highlight)
        {
            row.GetCell(0).BackgroundColor = TotalTint;
            row.GetCell(1).BackgroundColor = TotalTint;
        }
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    private sealed record ServiceLine(string Description, int Quantity, decimal UnitPrice);
}
