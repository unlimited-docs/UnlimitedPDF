using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Extensions;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Receipt: a compact A6 page with a store logo, items, tax, total, and payment details.
/// </summary>
public static class ReceiptSample
{
    #region Fields and Constants

    private const double ItemsTop = 298;
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
        var document = new PdfDocument(PdfPageSize.A6);
        var (pageWidth, pageHeight) = PdfPageSize.A6.ToDimensions();
        var page = document.AddPage();

        // Shapes first: the page paints in call order, so text added later sits on top.
        // A grey header band behind the logo, with a dark accent bar under it.
        AddBand(page, 0, pageHeight, pageWidth, 66, _bandTint);
        AddBand(page, 0, pageHeight - 66, pageWidth, 3, _dark);

        page.AddImage(images["receipt-logo.png"], (pageWidth - 100) / 2, 362, 100, 42, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
        AddCentered(page, input.StoreName, 336, 12, pageWidth);
        AddCentered(page, input.StoreAddress, 323, 8, pageWidth);
        page.AddText(PdfText.Escape($"Receipt {input.ReceiptNumber}"), 16, 306, 8);
        page.AddText($"{input.Date:dd-MMM-yyyy}", 220, 306, 8);

        // Every row is exactly RowHeight tall, so the totals can be placed right below the last item.
        var items = page.AddTable(16, ItemsTop, 265);
        items.SetColumnWidths(125, 30, 50, 60);
        items.RowHeight = RowHeight;

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

        decimal tax = subtotal * input.TaxPercent / 100m;
        double totalsTop = ItemsTop - (input.Items.Count + 1) * RowHeight - 8;
        var totals = page.AddTable(146, totalsTop, 135);
        totals.SetColumnWidths(75, 60);
        totals.RowHeight = RowHeight;
        AddTotal(totals, 0, "Subtotal", subtotal);
        AddTotal(totals, 1, $"Tax {input.TaxPercent:0.##}%", tax);
        AddTotal(totals, 2, "Total", subtotal + tax);
        totals.GetCell(2, 0).Background = _totalTint;
        totals.GetCell(2, 1).Background = _totalTint;

        page.AddText(PdfText.Escape($"Paid by: {input.PaymentMethod}"), 16, (int)(totalsTop - 3 * RowHeight - 18), 8);
        AddBand(page, 16, 44, pageWidth - 32, 1.5, _dark);
        AddCentered(page, "Thank you for shopping with us!", 30, 9, pageWidth);

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

    // AddText has no centering, so estimate the text width (about half the font size per character).
    private static void AddCentered(PdfPageBuilder page, string text, int y, int fontSize, double pageWidth)
    {
        int x = (int)Math.Max(8, (pageWidth - text.Length * fontSize * 0.5) / 2);
        page.AddText(PdfText.Escape(text), x, y, fontSize);
    }

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
