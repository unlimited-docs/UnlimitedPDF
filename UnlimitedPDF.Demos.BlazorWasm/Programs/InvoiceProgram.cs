// Invoice: a tax invoice with a logo, product photos in the line items, and computed totals.
// A complete console program: it writes invoice.pdf.
//
// Run it:
//   1. dotnet new console -n InvoiceExample
//   2. cd InvoiceExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the InvoiceExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/invoice-logo.png
//        https://unlimitedpdf.dev/images/keyboard.jpg
//        https://unlimitedpdf.dev/images/dock.jpg
//   6. dotnet run
// Then open invoice.pdf in the InvoiceExample folder.

using UnlimitedPDF;

namespace InvoiceExample;

public static class Program
{
    private const double Left = 50;

    // Light tints of the logo's navy and orange: text stays black, so fills carry the color.
    private static readonly PdfColor BandTint = new(0.92, 0.94, 0.98);
    private static readonly PdfColor HeaderTint = new(0.80, 0.86, 0.95);
    private static readonly PdfColor Orange = new(0.95, 0.42, 0.11);
    private static readonly PdfColor TotalTint = new(1.0, 0.89, 0.80);

    public static void Main()
    {
        // The sample data: change it, run again, and open invoice.pdf.
        string sellerName = "ABC Electronics Pvt. Ltd.";
        string sellerAddress = "123 Tech Park, Bangalore, KA 560001";
        string sellerTaxId = "GSTIN: 222222222222225";
        string buyerName = "UnlimitedPDF";
        string buyerAddress = "456 Code Street, Hyderabad, TS 500001";
        DateTime invoiceDate = DateTime.Today;
        string invoiceNumber = $"INV-{invoiceDate:yyyy-MM-dd}-001";
        LineItem[] items =
        [
            new("Wireless Keyboard", 2, 1250.00m, "keyboard.jpg"),
            new("USB-C Docking Station", 1, 4500.00m, "dock.jpg"),
        ];

        // The side margins stay 0 so the header band runs edge to edge; content is placed 50 pt in.
        var document = new PdfDocument { MarginBottom = 24 };
        var (pageWidth, _) = PdfPageSize.A4.ToDimensions();

        // The header and footer are drawn on every page the invoice needs.
        document.Header.Height = 76;
        document.Header.AddRectangle(0, 0, pageWidth, 72, BandTint);
        document.Header.AddRectangle(0, 72, pageWidth, 4, Orange);
        document.Header.AddText("INVOICE", Left, 22, 24);
        var logo = PdfImage.FromFile("invoice-logo.png");
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

        // Line items. Rows with a photo grow to fit it (AtLeast); the header row repeats on every
        // page.
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
        for (int index = 0; index < items.Length; index++)
        {
            var item = items[index];
            decimal amount = item.Quantity * item.Rate;
            subtotal += amount;

            var row = table.AddRow();
            row.HeightMode = PdfRowHeightMode.AtLeast;
            row.GetCell(0).Text = (index + 1).ToString();
            if (item.Photo.Length > 0)
            {
                row.GetCell(1).SetContent(PdfImage.FromFile(item.Photo), item.Description);
            }
            else
            {
                row.GetCell(1).Text = item.Description;
            }

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

        document.Save("invoice.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("invoice.pdf")}");
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

    private sealed record LineItem(string Description, int Quantity, decimal Rate, string Photo);
}
