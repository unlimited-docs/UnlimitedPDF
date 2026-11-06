using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Quotation: a services quote. Page rules give the first page a taller header with the quote details and the last
/// page its own footer; the terms heading stays with the terms and the acceptance signature.
/// </summary>
public static class QuotationSample
{
    #region Fields and Constants

    private const double Width = 495;

    private static readonly PdfColor _headerTint = new(0.88, 0.92, 0.98);
    private static readonly PdfColor _indigo = new(0.17, 0.23, 0.55);
    private static readonly PdfColor _sky = new(0.23, 0.65, 0.88);
    private static readonly PdfColor _totalTint = new(0.86, 0.93, 0.99);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The quotation data.</param>
    /// <param name="images">The company logo ("quotation-logo.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(QuotationInput input, SampleImages images)
    {
        var document = new PdfDocument { TopMargin = 32, BottomMargin = 24, LeftMargin = 50, RightMargin = 50 };
        var logo = images["quotation-logo.png"];
        var fit = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        DateTime validUntil = input.QuoteDate.AddDays(Math.Max(1, input.ValidDays));

        // Pages after the first: a slim header with the logo and the quote number.
        document.Header.Height = 44;
        document.Header.AddImage(logo, 0, 0, 120, 36, fit);
        document.Header.AddText($"Quotation {input.QuoteNumber} (continued)", 0, 14, 10, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 41, Width, 1.5, _sky);
        document.Footer.Height = 22;
        document.Footer.AddText("Summit IT Services  |  Quotation " + input.QuoteNumber, 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);

        // The first page: a taller header with the quote details.
        var first = document.AddPageRule(PdfPageRuleTarget.First);
        first.Header.Height = 96;
        first.Header.AddImage(logo, 0, 0, 165, 55, fit);
        first.Header.AddText("QUOTATION", 0, 4, 24, PdfTextAlignment.Right);
        first.Header.AddText($"Quote no. {input.QuoteNumber}", 0, 40, 10, PdfTextAlignment.Right);
        first.Header.AddText($"Date {input.QuoteDate:dd MMM yyyy}  |  Valid until {validUntil:dd MMM yyyy}", 0, 56, 10, PdfTextAlignment.Right);
        first.Header.AddRectangle(0, 92, Width, 2, _indigo);

        // The last page: a closing line in the footer.
        var last = document.AddPageRule(PdfPageRuleTarget.Last).Footer;
        last.AddText("Thank you for your business", 0, 8, 8, PdfTextAlignment.Center);
        last.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);

        document.Body.TopGap = 18;
        document.Body.BottomGap = 14;
        var body = document.Body;

        body.AddText("Prepared for", fontSize: 9);
        body.AddText(input.ClientName, spacing: 2, fontSize: 12);
        body.AddText(input.ClientCompany, spacing: 2, fontSize: 10);
        body.AddText(input.ClientAddress, spacing: 2, fontSize: 10);

        var services = body.AddTable(Width, spacing: 16);
        services.SetColumnWidths(255, 50, 90, 100);
        services.HeaderRowCount = 1;
        services.RepeatHeaderRows = true;
        var header = services.AddRow();
        string[] headings = ["Service", "Qty", "Rate", "Amount"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).Background = _headerTint;
        }

        decimal subtotal = 0;
        foreach (var line in input.Services)
        {
            decimal amount = line.Quantity * line.UnitPrice;
            subtotal += amount;
            var row = services.AddRow();
            row.GetCell(0).Text = line.Description;
            Right(row.GetCell(1), line.Quantity.ToString());
            Right(row.GetCell(2), line.UnitPrice.ToString("F2"));
            Right(row.GetCell(3), amount.ToString("F2"));
        }

        decimal tax = subtotal * input.TaxPercent / 100m;
        var totals = body.AddBlock(spacing: 12);
        var table = totals.AddTable(220, x: 275);
        table.SetColumnWidths(120, 100);
        AddTotal(table, "Subtotal", subtotal, highlight: false);
        AddTotal(table, $"Tax {input.TaxPercent:0.##}%", tax, highlight: false);
        AddTotal(table, "Total", subtotal + tax, highlight: true);

        // The heading stays on the page of the terms, and the terms stay with the signature.
        body.AddText("Terms and acceptance", spacing: 24, fontSize: 12, keepWithNext: true);
        var terms = body.AddBlock(spacing: 6);
        terms.AddText($"1. Prices are valid until {validUntil:dd MMM yyyy}.", fontSize: 9);
        terms.AddText("2. 50% is invoiced on acceptance and 50% on completion; payment is due in 15 days.", spacing: 3, fontSize: 9);
        terms.AddText("3. Work starts within 10 working days of acceptance.", spacing: 3, fontSize: 9);
        terms.AddText("Accepted by: ____________________          Date: ______________", spacing: 28, fontSize: 10);

        return document;
    }

    #endregion

    #region Private Methods

    private static void AddTotal(PdfTable table, string label, decimal value, bool highlight)
    {
        var row = table.AddRow();
        row.GetCell(0).Text = label;
        Right(row.GetCell(1), value.ToString("F2"));
        if (highlight)
        {
            row.GetCell(0).Background = _totalTint;
            row.GetCell(1).Background = _totalTint;
        }
    }

    private static void Right(PdfTableCell cell, string text)
    {
        cell.Text = text;
        cell.HAlign = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
