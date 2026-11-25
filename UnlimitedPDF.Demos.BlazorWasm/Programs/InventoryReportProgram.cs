// Inventory report: stock grouped by warehouse, with subtotals and the header row on every page.
// A complete console program: it writes inventory-report.pdf.
//
// Run it:
//   1. dotnet new console -n InventoryReportExample
//   2. cd InventoryReportExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open inventory-report.pdf in the InventoryReportExample folder.

using UnlimitedPDF;

namespace InventoryReportExample;

public static class Program
{
    private const double Width = 495;

    private static readonly string[] Items =
    [
        "USB-C charger 65 W", "Wireless mouse", "Mechanical keyboard", "27-inch monitor",
        "Laptop stand", "HDMI cable 2 m", "Webcam 1080p", "Noise-cancelling headset",
        "Docking station", "Portable SSD 1 TB", "Ethernet adapter", "Desk lamp",
    ];

    private static readonly string[] WarehouseNames =
    [
        "Bengaluru", "Hyderabad", "Chennai", "Pune", "Mumbai", "Delhi", "Kolkata", "Ahmedabad",
    ];

    private static readonly PdfColor HeaderTint = new(0.86, 0.91, 0.97);
    private static readonly PdfColor SubtotalTint = new(0.93, 0.95, 0.98);

    public static void Main()
    {
        // The sample data: change it, run again, and open inventory-report.pdf.
        string title = "Stock on hand";
        int warehouses = 3; // 1 to 8
        int items = 24;     // items per warehouse

        // The stock figures are generated from a fixed seed, so the same input gives the same
        // report.
        var random = new Random(2026);

        var document = new PdfDocument
        {
            MarginTop = 40, MarginBottom = 30, MarginLeft = 50, MarginRight = 50,
        };
        document.Header.Height = 30;
        document.Header.AddText(title, 0, 6, 14);
        document.Header.AddText("Inventory report", 0, 8, 9, PdfTextAlignment.Right);
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 6, 8, PdfTextAlignment.Right);
        var body = document.Body;

        decimal grandTotal = 0;
        for (int w = 0; w < warehouses; w++)
        {
            // The warehouse heading stays on the page of its first rows.
            body.AddText(
                $"Warehouse: {WarehouseNames[w]}",
                spacing: w == 0 ? 0 : 20, fontSize: 13, keepWithNext: true);

            var table = body.AddTable(Width, spacing: 6);
            table.SetColumnWidths(70, 195, 60, 80, 90);
            table.HeaderRowCount = 1;
            table.RepeatHeaderRows = true;
            var header = table.AddRow();
            string[] headings = ["SKU", "Item", "Qty", "Unit cost", "Value"];
            for (int column = 0; column < headings.Length; column++)
            {
                header.GetCell(column).Text = headings[column];
                header.GetCell(column).BackgroundColor = HeaderTint;
            }

            decimal subtotal = 0;
            for (int i = 0; i < items; i++)
            {
                int quantity = random.Next(0, 400);
                decimal cost = Math.Round((decimal)(random.NextDouble() * 9000 + 250), 2);
                decimal value = quantity * cost;
                subtotal += value;

                var row = table.AddRow();
                row.GetCell(0).Text = $"{WarehouseNames[w][..3].ToUpperInvariant()}-{i + 1:0000}";
                row.GetCell(1).Text = Items[(i + w) % Items.Length];
                Right(row.GetCell(2), quantity.ToString());
                Right(row.GetCell(3), cost.ToString("N2"));
                Right(row.GetCell(4), value.ToString("N2"));
            }

            // The subtotal row closes the warehouse's table.
            var total = table.AddRow();
            total.GetCell(1).Text = $"Subtotal, {WarehouseNames[w]}";
            Right(total.GetCell(4), subtotal.ToString("N2"));
            for (int column = 0; column < 5; column++)
            {
                total.GetCell(column).BackgroundColor = SubtotalTint;
            }

            grandTotal += subtotal;
        }

        string noun = warehouses == 1 ? "warehouse" : "warehouses";
        body.AddText(
            $"Grand total across {warehouses} {noun}: INR {grandTotal:N2}",
            spacing: 18, fontSize: 12);

        document.Save("inventory-report.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("inventory-report.pdf")}");
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }
}
