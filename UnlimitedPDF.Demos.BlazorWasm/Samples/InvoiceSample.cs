using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Invoice: a header band with the logo and a footer with page numbers on every page, then seller and buyer details,
/// line items with product photos, and the totals, which stay together after the last item however many items there are.
/// </summary>
public static class InvoiceSample
{
    #region Fields and Constants

    private const decimal CgstRate = 0.09m;
    private const double Left = 50;
    private const decimal SgstRate = 0.09m;

    // Light tints of the logo's navy and orange: text stays black, so fills carry the color.
    private static readonly PdfColor _bandTint = new(0.92, 0.94, 0.98);
    private static readonly PdfColor _headerTint = new(0.80, 0.86, 0.95);
    private static readonly PdfColor _orange = new(0.95, 0.42, 0.11);
    private static readonly PdfColor _totalTint = new(1.0, 0.89, 0.80);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The invoice data.</param>
    /// <param name="images">The logo ("invoice-logo.png") and the product photos the items use.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(InvoiceInput input, SampleImages images)
    {
        // The side margins stay 0 so the header band runs edge to edge; content is placed 50 pt in.
        var document = new PdfDocument { MarginBottom = 24 };
        var (pageWidth, _) = PdfPageSize.A4.ToDimensions();

        // The header and footer are drawn on every page the invoice needs.
        document.Header.Height = 76;
        document.Header.AddRectangle(0, 0, pageWidth, 72, _bandTint);
        document.Header.AddRectangle(0, 72, pageWidth, 4, _orange);
        document.Header.AddText("INVOICE", Left, 22, 24);
        document.Header.AddImage(images["invoice-logo.png"], 432, 14, 130, 45, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });

        document.Footer.Height = 30;
        document.Footer.AddRectangle(Left, 0, 495, 3, _orange);
        document.Footer.AddText($"Invoice No: {input.InvoiceNumber}", Left, 12, 9);
        document.Footer.AddText("Page {page} of {pages}", Left, 12, 9, PdfTextAlignment.Right);

        document.BodyMarginTop = 20;
        document.BodyMarginBottom = 20;
        var body = document.Body;

        // Seller and buyer side by side, in a table without borders.
        var parties = body.AddTable(512, x: Left);
        parties.SetColumnWidths(300, 212);
        AddDetailRow(parties, "Seller:", "Buyer:");
        AddDetailRow(parties, input.SellerName, input.BuyerName);
        AddDetailRow(parties, input.SellerAddress, input.BuyerAddress);
        AddDetailRow(parties, input.SellerTaxId, "");

        body.AddText($"Invoice No: {input.InvoiceNumber}", spacing: 14, fontSize: 10, x: Left);
        body.AddText($"Date: {input.InvoiceDate:dd-MMM-yyyy}", fontSize: 10, x: Left);

        // Line items. Rows with a photo grow to fit it (AtLeast); the header row repeats on every page.
        var items = body.AddTable(512, spacing: 12, x: Left);
        items.SetColumnWidths(40, 210, 60, 100, 102);
        items.HeaderRowCount = 1;
        items.RepeatHeaderRows = true;

        var header = items.AddRow();
        string[] headings = ["S.No.", "Item Description", "Qty", "Rate (INR)", "Amount (INR)"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = _headerTint;
        }

        decimal subtotal = 0;
        for (int index = 0; index < input.Items.Count; index++)
        {
            var line = input.Items[index];
            decimal amount = line.Quantity * line.Rate;
            subtotal += amount;

            var row = items.AddRow();
            row.HeightMode = PdfRowHeightMode.AtLeast;
            row.GetCell(0).Text = (index + 1).ToString();
            if (line.Photo.Length > 0)
            {
                row.GetCell(1).SetContent(images[line.Photo], line.Description);
            }
            else
            {
                row.GetCell(1).Text = line.Description;
            }

            SetRight(row.GetCell(2), line.Quantity.ToString());
            SetRight(row.GetCell(3), line.Rate.ToString("F2"));
            SetRight(row.GetCell(4), amount.ToString("F2"));
        }

        // The totals and terms follow the last item and move to the next page together when they do not fit.
        decimal cgst = subtotal * CgstRate;
        decimal sgst = subtotal * SgstRate;
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
        closing.AddText("2. Goods once sold will not be taken back.", spacing: 2, fontSize: 9, x: Left);

        return document;
    }

    #endregion

    #region Private Methods

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
            foreach (var border in new[] { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
            {
                border.Color = PdfColor.White;
            }
        }
    }

    private static void AddTotal(PdfTable table, int row, string label, decimal value, bool highlight)
    {
        var labelCell = table.GetOrCreateCell(row, 0);
        labelCell.Text = label;
        var valueCell = table.GetOrCreateCell(row, 1);
        SetRight(valueCell, value.ToString("F2"));

        if (highlight)
        {
            labelCell.BackgroundColor = _totalTint;
            valueCell.BackgroundColor = _totalTint;
        }
    }

    private static void SetRight(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
