// Purchase order: an item table that runs across pages, with the totals kept together at the end.
// A complete console program: it writes purchase-order.pdf.
//
// Run it:
//   1. dotnet new console -n PurchaseOrderExample
//   2. cd PurchaseOrderExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the PurchaseOrderExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/purchase-order-logo.png
//   6. dotnet run
// Then open purchase-order.pdf in the PurchaseOrderExample folder.

using UnlimitedPDF;

namespace PurchaseOrderExample;

public static class Program
{
    private const double Width = 495;

    private static readonly PdfColor Amber = new(0.91, 0.64, 0.09);
    private static readonly PdfColor HeaderTint = new(0.90, 0.92, 0.94);
    private static readonly PdfColor Slate = new(0.18, 0.24, 0.31);
    private static readonly PdfColor TotalTint = new(0.99, 0.94, 0.82);

    public static void Main()
    {
        // The sample data: change it, run again, and open purchase-order.pdf.
        // All names and numbers are fictional.
        string orderNumber = "PO-2026-0418";
        DateTime orderDate = DateTime.Today;
        string vendorName = "Apex Components Ltd.";
        string vendorAddress = "44 Industrial Estate, Chennai, TN 600058";
        string shipToAddress = "Plant 2, 18 Foundry Road, Pune, MH 411019";
        decimal taxPercent = 18m;
        decimal shipping = 75m;
        OrderLine[] lines =
        [
            new("Steel brackets, 40 mm", 500, 0.85m),
            new("Hex bolts M8 x 30", 2000, 0.12m),
            new("Bearing 6204-2RS", 120, 4.60m),
            new("Conveyor belt, 5 m", 4, 185.00m),
        ];
        // Generated lines added after the listed ones (0 to 500): raise it to see the order run
        // onto more pages.
        int generatedLines = 0;

        var document = new PdfDocument
        {
            MarginTop = 32,
            MarginBottom = 24,
            MarginLeft = 50,
            MarginRight = 50,
        };

        // Every page: the buyer's logo and the order number in the header, the page number in
        // the footer.
        document.Header.Height = 62;
        var contain = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        document.Header.AddImage(
            PdfImage.FromFile("purchase-order-logo.png"), 0, 0, 150, 50, contain);
        document.Header.AddText("PURCHASE ORDER", 0, 6, 20, PdfTextAlignment.Right);
        document.Header.AddText(
            $"{orderNumber}  |  {orderDate:dd MMM yyyy}", 0, 34, 10, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 58, Width, 2, Amber);
        document.Footer.Height = 22;
        document.Footer.AddRectangle(0, 0, Width, 0.5, Slate);
        document.Footer.AddText($"Purchase order {orderNumber}", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 18;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        // Vendor and delivery address side by side, in a table without borders.
        var parties = body.AddTable(Width);
        parties.SetColumnWidths(250, 245);
        AddDetailRow(parties, "Vendor", "Ship to");
        AddDetailRow(parties, vendorName, "Brightline Manufacturing");
        AddDetailRow(parties, vendorAddress, shipToAddress);

        // Order lines; the header row repeats on every page.
        var items = body.AddTable(Width, spacing: 16);
        items.SetColumnWidths(40, 235, 60, 75, 85);
        items.HeaderRowCount = 1;
        items.RepeatHeaderRows = true;
        var header = items.AddRow();
        string[] headings = ["No.", "Description", "Qty", "Unit price", "Amount"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        decimal subtotal = 0;
        int number = 0;
        foreach (var line in AllLines(lines, generatedLines))
        {
            decimal amount = line.Quantity * line.UnitPrice;
            subtotal += amount;
            var row = items.AddRow();
            row.GetCell(0).Text = (++number).ToString();
            row.GetCell(1).Text = line.Description;
            Right(row.GetCell(2), line.Quantity.ToString());
            Right(row.GetCell(3), line.UnitPrice.ToString("F2"));
            Right(row.GetCell(4), amount.ToString("F2"));
        }

        // Totals and the authorization stay together after the last line.
        decimal tax = subtotal * taxPercent / 100m;
        decimal total = subtotal + tax + shipping;
        var closing = body.AddBlock(spacing: 14);
        var totals = closing.AddTable(220, x: 275);
        totals.SetColumnWidths(120, 100);
        AddTotal(totals, "Subtotal", subtotal, highlight: false);
        AddTotal(totals, $"Tax {taxPercent:0.##}%", tax, highlight: false);
        AddTotal(totals, "Shipping", shipping, highlight: false);
        AddTotal(totals, "Total", total, highlight: true);
        closing.AddText(
            "Deliver within 14 days of this order. Quote the PO number on every invoice.",
            spacing: 18, fontSize: 9);
        closing.AddText(
            "Authorized by: ____________________          Date: ______________",
            spacing: 26, fontSize: 10);

        document.Save("purchase-order.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("purchase-order.pdf")}");
    }

    // One row of text without borders: cell borders take the white background.
    private static void AddDetailRow(PdfTable table, string left, string right)
    {
        var row = table.AddRow();
        row.Height = 16;
        string[] texts = [left, right];
        for (int column = 0; column < texts.Length; column++)
        {
            var cell = row.GetCell(column);
            cell.Text = texts[column];
            cell.Padding = 0;
            foreach (var border in new[]
                { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
            {
                border.Color = PdfColor.White;
            }
        }
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

    // The listed lines, then generated ones with fictional, repeatable values.
    private static IEnumerable<OrderLine> AllLines(OrderLine[] lines, int generatedLines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }

        int generated = Math.Clamp(generatedLines, 0, 500);
        for (int i = 1; i <= generated; i++)
        {
            yield return new OrderLine(
                $"Spare part SP-{1000 + i}", i % 9 + 1, 2.50m + i % 7 * 1.25m);
        }
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    private sealed record OrderLine(string Description, int Quantity, decimal UnitPrice);
}
