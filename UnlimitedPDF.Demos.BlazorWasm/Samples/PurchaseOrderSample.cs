using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Purchase order: a buyer's order to a vendor. The item table continues across pages with its header row repeated,
/// and the totals stay together with the authorization block after the last item.
/// </summary>
public static class PurchaseOrderSample
{
    #region Fields and Constants

    private const double Width = 495;

    private static readonly PdfColor _amber = new(0.91, 0.64, 0.09);
    private static readonly PdfColor _headerTint = new(0.90, 0.92, 0.94);
    private static readonly PdfColor _slate = new(0.18, 0.24, 0.31);
    private static readonly PdfColor _totalTint = new(0.99, 0.94, 0.82);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The order data.</param>
    /// <param name="images">The buyer's logo ("purchase-order-logo.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(PurchaseOrderInput input, SampleImages images)
    {
        var document = new PdfDocument { MarginTop = 32, MarginBottom = 24, MarginLeft = 50, MarginRight = 50 };

        // Every page: the buyer's logo and the order number in the header, the page number in the footer.
        document.Header.Height = 62;
        document.Header.AddImage(images["purchase-order-logo.png"], 0, 0, 150, 50, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
        document.Header.AddText("PURCHASE ORDER", 0, 6, 20, PdfTextAlignment.Right);
        document.Header.AddText($"{input.OrderNumber}  |  {input.OrderDate:dd MMM yyyy}", 0, 34, 10, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 58, Width, 2, _amber);
        document.Footer.Height = 22;
        document.Footer.AddRectangle(0, 0, Width, 0.5, _slate);
        document.Footer.AddText($"Purchase order {input.OrderNumber}", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 18;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        // Vendor and delivery address side by side, in a table without borders.
        var parties = body.AddTable(Width);
        parties.SetColumnWidths(250, 245);
        AddDetailRow(parties, "Vendor", "Ship to");
        AddDetailRow(parties, input.VendorName, "Brightline Manufacturing");
        AddDetailRow(parties, input.VendorAddress, input.ShipToAddress);

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
            header.GetCell(column).BackgroundColor = _headerTint;
        }

        decimal subtotal = 0;
        int number = 0;
        foreach (var line in Lines(input))
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
        decimal tax = subtotal * input.TaxPercent / 100m;
        decimal total = subtotal + tax + input.Shipping;
        var closing = body.AddBlock(spacing: 14);
        var totals = closing.AddTable(220, x: 275);
        totals.SetColumnWidths(120, 100);
        AddTotal(totals, "Subtotal", subtotal, highlight: false);
        AddTotal(totals, $"Tax {input.TaxPercent:0.##}%", tax, highlight: false);
        AddTotal(totals, "Shipping", input.Shipping, highlight: false);
        AddTotal(totals, "Total", total, highlight: true);
        closing.AddText("Deliver within 14 days of this order. Quote the PO number on every invoice.", spacing: 18, fontSize: 9);
        closing.AddText("Authorized by: ____________________          Date: ______________", spacing: 26, fontSize: 10);

        return document;
    }

    #endregion

    #region Private Methods

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
            foreach (var border in new[] { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
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
            row.GetCell(0).BackgroundColor = _totalTint;
            row.GetCell(1).BackgroundColor = _totalTint;
        }
    }

    // The listed lines, then generated ones with fictional, repeatable values.
    private static IEnumerable<OrderLine> Lines(PurchaseOrderInput input)
    {
        foreach (var line in input.Lines) yield return line;
        int generated = Math.Clamp(input.GeneratedLines, 0, 500);
        for (int i = 1; i <= generated; i++)
            yield return new OrderLine { Description = $"Spare part SP-{1000 + i}", Quantity = i % 9 + 1, UnitPrice = 2.50m + i % 7 * 1.25m };
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
