using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Inventory report: stock grouped by warehouse, each group a table whose header row repeats on every page it runs
/// onto, closed by a subtotal row, then the grand total. Page numbers in the footer count every page.
/// </summary>
public static class InventoryReportSample
{
    #region Fields and Constants

    private const double Width = 495;

    private static readonly string[] Items =
    [
        "USB-C charger 65 W", "Wireless mouse", "Mechanical keyboard", "27-inch monitor", "Laptop stand", "HDMI cable 2 m",
        "Webcam 1080p", "Noise-cancelling headset", "Docking station", "Portable SSD 1 TB", "Ethernet adapter", "Desk lamp",
    ];

    private static readonly string[] Warehouses = ["Bengaluru", "Hyderabad", "Chennai", "Pune", "Mumbai", "Delhi", "Kolkata", "Ahmedabad"];

    private static readonly PdfColor _headerTint = new(0.86, 0.91, 0.97);
    private static readonly PdfColor _subtotalTint = new(0.93, 0.95, 0.98);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the report. The stock figures are generated from a fixed seed, so the same input gives the same report.
    /// </summary>
    /// <param name="input">The title, the number of warehouses, and the items per warehouse.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(InventoryReportInput input)
    {
        int warehouses = Math.Clamp(input.Warehouses, 1, InventoryReportInput.MaxWarehouses);
        int items = Math.Clamp(input.ItemsPerWarehouse, 1, InventoryReportInput.MaxItems);
        var random = new Random(2026);

        var document = new PdfDocument { MarginTop = 40, MarginBottom = 30, MarginLeft = 50, MarginRight = 50 };
        document.Header.Height = 30;
        document.Header.AddText(input.Title, 0, 6, 14);
        document.Header.AddText("Inventory report", 0, 8, 9, PdfTextAlignment.Right);
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 6, 8, PdfTextAlignment.Right);
        var body = document.Body;

        decimal grandTotal = 0;
        for (int w = 0; w < warehouses; w++)
        {
            // The warehouse heading stays on the page of its first rows.
            body.AddText($"Warehouse: {Warehouses[w]}", spacing: w == 0 ? 0 : 20, fontSize: 13, keepWithNext: true);

            var table = body.AddTable(Width, spacing: 6);
            table.SetColumnWidths(70, 195, 60, 80, 90);
            table.HeaderRowCount = 1;
            table.RepeatHeaderRows = true;
            var header = table.AddRow();
            string[] headings = ["SKU", "Item", "Qty", "Unit cost", "Value"];
            for (int column = 0; column < headings.Length; column++)
            {
                header.GetCell(column).Text = headings[column];
                header.GetCell(column).BackgroundColor = _headerTint;
            }

            decimal subtotal = 0;
            for (int i = 0; i < items; i++)
            {
                int quantity = random.Next(0, 400);
                decimal cost = Math.Round((decimal)(random.NextDouble() * 9000 + 250), 2);
                decimal value = quantity * cost;
                subtotal += value;

                var row = table.AddRow();
                row.GetCell(0).Text = $"{Warehouses[w][..3].ToUpperInvariant()}-{i + 1:0000}";
                row.GetCell(1).Text = Items[(i + w) % Items.Length];
                Right(row.GetCell(2), quantity.ToString());
                Right(row.GetCell(3), cost.ToString("N2"));
                Right(row.GetCell(4), value.ToString("N2"));
            }

            var total = table.AddRow();
            total.GetCell(1).Text = $"Subtotal, {Warehouses[w]}";
            Right(total.GetCell(4), subtotal.ToString("N2"));
            for (int column = 0; column < 5; column++) total.GetCell(column).BackgroundColor = _subtotalTint;
            grandTotal += subtotal;
        }

        body.AddText($"Grand total across {warehouses} {(warehouses == 1 ? "warehouse" : "warehouses")}: INR {grandTotal:N2}", spacing: 18, fontSize: 12);
        return document;
    }

    #endregion

    #region Private Methods

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
