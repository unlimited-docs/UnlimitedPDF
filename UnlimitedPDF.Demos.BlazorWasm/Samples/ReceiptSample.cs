using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Extensions;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Receipt: a compact A6 page with a store logo, items, tax, total, and payment details. A long receipt continues on
/// more A6 pages, each with the logo band and the closing line, and the totals stay together after the last item.
/// </summary>
public static class ReceiptSample
{
    #region Fields and Constants

    private const double Inset = 16;
    private const double RowHeight = 18;

    // Greys only, so the receipt still prints cleanly on a black-and-white printer.
    private static readonly PdfColor _bandTint = new(0.93, 0.93, 0.93);
    private static readonly PdfColor _dark = new(0.2, 0.2, 0.2);
    private static readonly PdfColor _headerTint = new(0.86, 0.86, 0.86);
    private static readonly PdfColor _totalTint = new(0.80, 0.80, 0.80);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The receipt data.</param>
    /// <param name="images">The store logo ("receipt-logo.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(ReceiptInput input, SampleImages images)
    {
        var document = new PdfDocument(PdfPageSize.A6) { BottomMargin = 8 };
        var (pageWidth, _) = PdfPageSize.A6.ToDimensions();

        // A grey band behind the logo with a dark accent bar under it, and a closing line, on every page.
        document.Header.Height = 69;
        document.Header.AddRectangle(0, 0, pageWidth, 66, _bandTint);
        document.Header.AddRectangle(0, 66, pageWidth, 3, _dark);
        document.Header.AddImage(images["receipt-logo.png"], (pageWidth - 100) / 2, 15, 100, 42, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });

        document.Footer.Height = 40;
        document.Footer.AddRectangle(Inset, 0, pageWidth - 2 * Inset, 1.5, _dark);
        document.Footer.AddText("Thank you for shopping with us!", 0, 8, 9, PdfTextAlignment.Center);
        document.Footer.AddText("Page {page} of {pages}", 0, 24, 7, PdfTextAlignment.Center);

        document.Body.TopGap = 10;
        document.Body.BottomGap = 8;
        var body = document.Body;

        body.AddText(input.StoreName, fontSize: 12, alignment: PdfTextAlignment.Center);
        body.AddText(input.StoreAddress, spacing: 2, fontSize: 8, alignment: PdfTextAlignment.Center);
        body.AddText($"Receipt {input.ReceiptNumber}", spacing: 6, fontSize: 8, x: Inset);
        body.AddText($"{input.Date:dd-MMM-yyyy}", fontSize: 8, alignment: PdfTextAlignment.Right, x: Inset);

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
            header.GetCell(column).Background = _headerTint;
        }

        decimal subtotal = 0;
        foreach (var line in input.Items)
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
        decimal tax = subtotal * input.TaxPercent / 100m;
        var closing = body.AddBlock(spacing: 8);
        var totals = closing.AddTable(135, x: 146);
        totals.SetColumnWidths(75, 60);
        totals.RowHeight = RowHeight;
        AddTotal(totals, 0, "Subtotal", subtotal);
        AddTotal(totals, 1, $"Tax {input.TaxPercent:0.##}%", tax);
        AddTotal(totals, 2, "Total", subtotal + tax);
        totals.GetCell(2, 0).Background = _totalTint;
        totals.GetCell(2, 1).Background = _totalTint;
        closing.AddText($"Paid by: {input.PaymentMethod}", spacing: 10, fontSize: 8, x: Inset);

        return document;
    }

    #endregion

    #region Private Methods

    private static void AddTotal(PdfTable table, int row, string label, decimal value)
    {
        table.GetOrCreateCell(row, 0).Text = label;
        Right(table.GetOrCreateCell(row, 1), value.ToString("F2"));
    }

    private static void Right(PdfTableCell cell, string text)
    {
        cell.Text = text;
        cell.HAlign = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
