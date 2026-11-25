// Bulk invoices: many invoices made in one run, each for its own buyer and items, each its own PDF.
// A complete console program: it writes INV-2026-0001.pdf to INV-2026-0025.pdf and invoices.zip.
//
// Run it:
//   1. dotnet new console -n BulkInvoicesExample
//   2. cd BulkInvoicesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the BulkInvoicesExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/invoice-logo.png
//   6. dotnet run
// Then open INV-2026-0001.pdf to INV-2026-0025.pdf in the invoices folder, or invoices.zip, in
// the BulkInvoicesExample folder.

using UnlimitedPDF;

namespace BulkInvoicesExample;

public static class Program
{
    private const double Left = 50;

    // Light tints of the logo's navy and orange: text stays black, so fills carry the color.
    private static readonly PdfColor BandTint = new(0.92, 0.94, 0.98);
    private static readonly PdfColor HeaderTint = new(0.80, 0.86, 0.95);
    private static readonly PdfColor Orange = new(0.95, 0.42, 0.11);
    private static readonly PdfColor TotalTint = new(1.0, 0.89, 0.80);

    private static readonly string[] Buyers =
    [
        "Fabrikam Traders", "Contoso Retail", "Northwind Foods", "Tailspin Toys",
        "Wide World Importers", "Proseware Labs", "Litware Systems", "Adventure Works",
        "Alpine Ski House", "Coho Vineyard",
    ];

    private static readonly (string Description, decimal Rate)[] Products =
    [
        ("Wireless mouse", 899m), ("Mechanical keyboard", 4_499m), ("27-inch monitor", 18_999m),
        ("USB-C dock", 7_999m), ("Laptop stand", 1_299m), ("Webcam 1080p", 3_499m),
        ("Headset", 2_999m), ("Portable SSD 1 TB", 8_499m),
    ];

    public static void Main()
    {
        // The sample data: change it, run again, and open the invoices.
        int count = 25;
        string sellerName = "ABC Electronics Pvt. Ltd.";
        string sellerAddress = "123 Tech Park, Bangalore, KA 560001";
        string sellerTaxId = "GSTIN: 222222222222225";
        string buyerAddress = "456 Code Street, Hyderabad, TS 500001";

        // The logo is read once and used by every invoice.
        var logo = PdfImage.FromFile("invoice-logo.png");
        // A new run replaces the invoices of the last one.
        if (Directory.Exists("invoices")) Directory.Delete("invoices", recursive: true);
        Directory.CreateDirectory("invoices");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int index = 0; index < count; index++)
        {
            // Each invoice's number, buyer, date, and two to four items come from its index.
            string buyerName = Buyers[index % Buyers.Length];
            string invoiceNumber = $"INV-2026-{index + 1:0000}";
            DateTime invoiceDate = new DateTime(2026, 11, 1).AddDays(index % 28);
            var items = new List<LineItem>();
            for (int item = 0; item < 2 + index % 3; item++)
            {
                var (description, rate) = Products[(index + item * 3) % Products.Length];
                items.Add(new LineItem(description, 1 + (index + item) % 5, rate));
            }

            var document = BuildInvoice(
                logo, sellerName, sellerAddress, sellerTaxId, buyerName, buyerAddress,
                invoiceNumber, invoiceDate, items);
            document.Save(Path.Combine("invoices", $"{invoiceNumber}.pdf"));
        }

        watch.Stop();
        Console.WriteLine(
            $"Made {count} invoices in {watch.ElapsedMilliseconds} ms, " +
            $"{watch.Elapsed.TotalMilliseconds / count:0.#} ms each.");

        // The demo hands the invoices over as one zip file; this does the same.
        File.Delete("invoices.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory("invoices", "invoices.zip");
        Console.WriteLine($"Wrote {Path.GetFullPath("invoices")} and invoices.zip");
    }

    // The Invoice demo's builder: a header band with the logo, the parties, the items, and the
    // totals.
    private static PdfDocument BuildInvoice(
        PdfImage logo, string sellerName, string sellerAddress, string sellerTaxId,
        string buyerName, string buyerAddress, string invoiceNumber, DateTime invoiceDate,
        List<LineItem> items)
    {
        // The side margins stay 0 so the header band runs edge to edge; content is placed 50 pt in.
        var document = new PdfDocument { MarginBottom = 24 };
        var (pageWidth, _) = PdfPageSize.A4.ToDimensions();

        // The header and footer are drawn on every page the invoice needs.
        document.Header.Height = 76;
        document.Header.AddRectangle(0, 0, pageWidth, 72, BandTint);
        document.Header.AddRectangle(0, 72, pageWidth, 4, Orange);
        document.Header.AddText("INVOICE", Left, 22, 24);
        var contain = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        document.Header.AddImage(logo, 432, 14, 130, 45, contain);

        document.Footer.Height = 30;
        document.Footer.AddRectangle(Left, 0, 495, 3, Orange);
        document.Footer.AddText($"Invoice No: {invoiceNumber}", Left, 12, 9);
        document.Footer.AddText("Page {page} of {pages}", Left, 12, 9, PdfTextAlignment.Right);

        document.BodyMarginTop = 20;
        document.BodyMarginBottom = 20;
        var body = document.Body;

        // Seller and buyer side by side, in a table without borders.
        var parties = body.AddTable(512, x: Left);
        parties.SetColumnWidths(300, 212);
        AddDetailRow(parties, "Seller:", "Buyer:");
        AddDetailRow(parties, sellerName, buyerName);
        AddDetailRow(parties, sellerAddress, buyerAddress);
        AddDetailRow(parties, sellerTaxId, "");

        body.AddText($"Invoice No: {invoiceNumber}", spacing: 14, fontSize: 10, x: Left);
        body.AddText($"Date: {invoiceDate:dd-MMM-yyyy}", fontSize: 10, x: Left);

        // Line items; the header row repeats on every page.
        var table = body.AddTable(512, spacing: 12, x: Left);
        table.SetColumnWidths(40, 210, 60, 100, 102);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;

        var header = table.AddRow();
        string[] headings = ["S.No.", "Item Description", "Qty", "Rate (INR)", "Amount (INR)"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        decimal subtotal = 0;
        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            decimal amount = item.Quantity * item.Rate;
            subtotal += amount;

            var row = table.AddRow();
            row.HeightMode = PdfRowHeightMode.AtLeast;
            row.GetCell(0).Text = (index + 1).ToString();
            row.GetCell(1).Text = item.Description;
            SetRight(row.GetCell(2), item.Quantity.ToString());
            SetRight(row.GetCell(3), item.Rate.ToString("F2"));
            SetRight(row.GetCell(4), amount.ToString("F2"));
        }

        // The totals and terms follow the last item and move to the next page together when they
        // do not fit.
        decimal cgst = subtotal * 0.09m;
        decimal sgst = subtotal * 0.09m;
        decimal total = subtotal + cgst + sgst;

        var closing = body.AddBlock(spacing: 16);
        var totals = closing.AddTable(212, x: 350);
        totals.SetColumnWidths(110, 102);
        AddTotal(totals, 0, "Subtotal", subtotal, highlight: false);
        AddTotal(totals, 1, "CGST @ 9%", cgst, highlight: false);
        AddTotal(totals, 2, "SGST @ 9%", sgst, highlight: false);
        AddTotal(totals, 3, "Total", total, highlight: true);

        closing.AddText("Terms & Conditions:", spacing: 20, fontSize: 10, x: Left);
        closing.AddText("1. Payment due within 30 days.", spacing: 2, fontSize: 9, x: Left);
        closing.AddText(
            "2. Goods once sold will not be taken back.", spacing: 2, fontSize: 9, x: Left);

        return document;
    }

    // One row of label or value text without borders: cell borders take the white background.
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
            cell.BorderLeft.Color = PdfColor.White;
            cell.BorderTop.Color = PdfColor.White;
            cell.BorderRight.Color = PdfColor.White;
            cell.BorderBottom.Color = PdfColor.White;
        }
    }

    private static void AddTotal(
        PdfTable table, int row, string label, decimal value, bool highlight)
    {
        var labelCell = table.GetOrCreateCell(row, 0);
        labelCell.Text = label;
        var valueCell = table.GetOrCreateCell(row, 1);
        SetRight(valueCell, value.ToString("F2"));

        if (highlight)
        {
            labelCell.BackgroundColor = TotalTint;
            valueCell.BackgroundColor = TotalTint;
        }
    }

    private static void SetRight(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    private sealed record LineItem(string Description, int Quantity, decimal Rate);
}
