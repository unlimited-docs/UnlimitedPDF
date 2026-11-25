// Receipt: a compact A6 store receipt with items, tax, and payment details.
// A complete console program: it writes receipt.pdf.
//
// Run it:
//   1. dotnet new console -n ReceiptExample
//   2. cd ReceiptExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the ReceiptExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/receipt-logo.png
//   6. dotnet run
// Then open receipt.pdf in the ReceiptExample folder.

using UnlimitedPDF;

namespace ReceiptExample;

public static class Program
{
    private const double Inset = 16;
    private const double RowHeight = 18;

    // Greys only, so the receipt still prints cleanly on a black-and-white printer.
    private static readonly PdfColor BandTint = new(0.93, 0.93, 0.93);
    private static readonly PdfColor Dark = new(0.2, 0.2, 0.2);
    private static readonly PdfColor HeaderTint = new(0.86, 0.86, 0.86);
    private static readonly PdfColor TotalTint = new(0.80, 0.80, 0.80);

    public static void Main()
    {
        // The sample data: change it, run again, and open receipt.pdf.
        string storeName = "UnlimitedPDF Mart";
        string storeAddress = "7 Market Road, Demo City";
        string receiptNumber = "R-000245";
        DateTime date = DateTime.Today;
        decimal taxPercent = 5m;
        string paymentMethod = "Card";
        ReceiptLine[] lines =
        [
            new("Coffee beans 250 g", 1, 450m),
            new("Oat milk 1 L", 2, 180m),
            new("Croissant", 3, 90m),
            new("Paper bag", 1, 10m),
        ];

        var document = new PdfDocument(PdfPageSize.A6) { MarginBottom = 8 };
        var (pageWidth, _) = PdfPageSize.A6.ToDimensions();

        // A grey band behind the logo with a dark accent bar under it, and a closing line, on
        // every page.
        document.Header.Height = 69;
        document.Header.AddRectangle(0, 0, pageWidth, 66, BandTint);
        document.Header.AddRectangle(0, 66, pageWidth, 3, Dark);
        var contain = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        document.Header.AddImage(
            PdfImage.FromFile("receipt-logo.png"), (pageWidth - 100) / 2, 15, 100, 42, contain);

        document.Footer.Height = 40;
        document.Footer.AddRectangle(Inset, 0, pageWidth - 2 * Inset, 1.5, Dark);
        document.Footer.AddText(
            "Thank you for shopping with us!", 0, 8, 9, PdfTextAlignment.Center);
        document.Footer.AddText("Page {page} of {pages}", 0, 24, 7, PdfTextAlignment.Center);

        document.BodyMarginTop = 10;
        document.BodyMarginBottom = 8;
        var body = document.Body;

        body.AddText(storeName, fontSize: 12, alignment: PdfTextAlignment.Center);
        body.AddText(
            storeAddress, spacing: 2, fontSize: 8, alignment: PdfTextAlignment.Center);
        body.AddText($"Receipt {receiptNumber}", spacing: 6, fontSize: 8, x: Inset);
        body.AddText(
            $"{date:dd-MMM-yyyy}", fontSize: 8, alignment: PdfTextAlignment.Right, x: Inset);

        var items = body.AddTable(265, spacing: 6, x: Inset);
        items.SetColumnWidths(125, 30, 50, 60);
        items.RowHeight = RowHeight;
        items.HeaderRowCount = 1;
        items.RepeatHeaderRows = true;

        var header = items.AddRow();
        string[] headings = ["Item", "Qty", "Price", "Total"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        decimal subtotal = 0;
        foreach (var line in lines)
        {
            decimal lineTotal = line.Quantity * line.Price;
            subtotal += lineTotal;
            var row = items.AddRow();
            row.GetCell(0).Text = line.Name;
            Right(row.GetCell(1), line.Quantity.ToString());
            Right(row.GetCell(2), line.Price.ToString("F2"));
            Right(row.GetCell(3), lineTotal.ToString("F2"));
        }

        // The totals and the payment line follow the items and stay together.
        decimal tax = subtotal * taxPercent / 100m;
        var closing = body.AddBlock(spacing: 8);
        var totals = closing.AddTable(135, x: 146);
        totals.SetColumnWidths(75, 60);
        totals.RowHeight = RowHeight;
        AddTotal(totals, 0, "Subtotal", subtotal);
        AddTotal(totals, 1, $"Tax {taxPercent:0.##}%", tax);
        AddTotal(totals, 2, "Total", subtotal + tax);
        totals.GetCell(2, 0).BackgroundColor = TotalTint;
        totals.GetCell(2, 1).BackgroundColor = TotalTint;
        closing.AddText($"Paid by: {paymentMethod}", spacing: 10, fontSize: 8, x: Inset);

        document.Save("receipt.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("receipt.pdf")}");
    }

    private static void AddTotal(PdfTable table, int row, string label, decimal value)
    {
        table.GetOrCreateCell(row, 0).Text = label;
        Right(table.GetOrCreateCell(row, 1), value.ToString("F2"));
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    private sealed record ReceiptLine(string Name, int Quantity, decimal Price);
}
