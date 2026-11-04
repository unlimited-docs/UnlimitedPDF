using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Invoice: a logo, seller and buyer details, line items with product photos, computed tax and totals.
/// </summary>
public static class InvoiceSample
{
    #region Fields and Constants

    private const decimal CgstRate = 0.09m;
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
        var document = new PdfDocument();
        var page = document.AddPage();

        // Shapes first: the page paints in call order, so text added later sits on top.
        // A tinted header band across the top, with an orange accent bar under it.
        AddBand(page, 0, 842, 595, 72, _bandTint);
        AddBand(page, 0, 770, 595, 4, _orange);

        page.AddText("INVOICE", 50, 796, 24);

        // Logo on the right of the band, fitted inside a 130 x 45 pt box whatever its shape.
        page.AddImage(images["invoice-logo.png"], 432, 783, 130, 45, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });

        page.AddText("Seller:", 50, 744, 10);
        page.AddText(PdfText.Escape(input.SellerName), 50, 732, 10);
        page.AddText(PdfText.Escape(input.SellerAddress), 50, 720, 10);
        page.AddText(PdfText.Escape(input.SellerTaxId), 50, 708, 10);

        page.AddText("Buyer:", 350, 744, 10);
        page.AddText(PdfText.Escape(input.BuyerName), 350, 732, 10);
        page.AddText(PdfText.Escape(input.BuyerAddress), 350, 720, 10);

        page.AddText(PdfText.Escape($"Invoice No: {input.InvoiceNumber}"), 50, 682, 10);
        page.AddText($"Date: {input.InvoiceDate:dd-MMM-yyyy}", 50, 670, 10);

        // Line items. Rows with a photo grow to fit it (AtLeast); the photo sits above the description.
        var items = page.AddTable(50, 650, 512);
        items.SetColumnWidths(40, 210, 60, 100, 102);

        var header = items.AddRow();
        string[] headings = ["S.No.", "Item Description", "Qty", "Rate (INR)", "Amount (INR)"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).Background = _headerTint;
        }

        decimal subtotal = 0;
        for (int index = 0; index < input.Items.Count; index++)
        {
            var line = input.Items[index];
            decimal amount = line.Quantity * line.Rate;
            subtotal += amount;

            var row = items.AddRow();
            row.HeightRule = PdfRowHeightRule.AtLeast;
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

        // Totals sit at a fixed position below the items.
        decimal cgst = subtotal * CgstRate;
        decimal sgst = subtotal * SgstRate;
        decimal total = subtotal + cgst + sgst;

        var totals = page.AddTable(350, 470, 212);
        totals.SetColumnWidths(110, 102);
        AddTotal(totals, 0, "Subtotal", subtotal, highlight: false);
        AddTotal(totals, 1, "CGST @ 9%", cgst, highlight: false);
        AddTotal(totals, 2, "SGST @ 9%", sgst, highlight: false);
        AddTotal(totals, 3, "Total", total, highlight: true);

        page.AddText("Terms & Conditions:", 50, 370, 10);
        page.AddText("1. Payment due within 30 days.", 50, 358, 9);
        page.AddText("2. Goods once sold will not be taken back.", 50, 346, 9);

        // A matching accent bar closes the page.
        AddBand(page, 50, 60, 495, 3, _orange);

        return document;
    }

    #endregion

    #region Private Methods

    // A filled rectangle drawn as a one-cell table. The border takes the fill color, because cells always draw one.
    private static void AddBand(PdfPageBuilder page, double x, double top, double width, double height, PdfColor color)
    {
        var band = page.AddTable(x, top, width);
        band.SetColumnWidths(width);
        band.RowHeight = height;
        var cell = band.AddRow().GetCell(0);
        cell.Background = color;
        foreach (var border in new[] { cell.LeftBorder, cell.TopBorder, cell.RightBorder, cell.BottomBorder })
        {
            border.Color = color;
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
            labelCell.Background = _totalTint;
            valueCell.Background = _totalTint;
        }
    }

    private static void SetRight(PdfTableCell cell, string text)
    {
        cell.Text = text;
        cell.HAlign = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
