using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Payslip: a one-page form. Earnings and deductions sit side by side and the net pay is computed. The page limit is
/// one, so a payslip with more lines than fit fails to save and says what does not fit, instead of running onto a
/// second page.
/// </summary>
public static class PayslipSample
{
    #region Fields and Constants

    private const double Width = 495;

    private static readonly PdfColor _green = new(0.18, 0.49, 0.20);
    private static readonly PdfColor _headerTint = new(0.88, 0.95, 0.88);
    private static readonly PdfColor _labelShade = new(0.96, 0.97, 0.96);
    private static readonly PdfColor _netTint = new(0.80, 0.91, 0.80);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The payslip data.</param>
    /// <param name="images">The employer's logo ("payslip-logo.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(PayslipInput input, SampleImages images)
    {
        var document = new PdfDocument { TopMargin = 36, BottomMargin = 28, LeftMargin = 50, RightMargin = 50 };

        // A payslip is one page: saving fails, naming what does not fit, if the lines need more.
        document.Body.MaxPages = 1;

        document.Header.Height = 64;
        document.Header.AddImage(images["payslip-logo.png"], 0, 0, 150, 50, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
        document.Header.AddText("PAYSLIP", 0, 6, 22, PdfTextAlignment.Right);
        document.Header.AddText($"Pay period: {input.PayPeriod}", 0, 36, 10, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 61, Width, 2, _green);
        document.Footer.Height = 22;
        document.Footer.AddText("This is a computer-generated payslip and needs no signature.", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.Body.TopGap = 18;
        var body = document.Body;

        // Employee details: label and value pairs in a four-column table.
        var details = body.AddTable(Width);
        details.SetColumnWidths(95, 150, 95, 155);
        string[,] pairs =
        {
            { "Employee", input.EmployeeName, "Employee ID", input.EmployeeId },
            { "Department", input.Department, "Designation", input.Designation },
            { "Pay period", input.PayPeriod, "Pay date", input.PayDate.ToString("dd MMM yyyy") }
        };
        for (int row = 0; row < pairs.GetLength(0); row++)
        {
            for (int column = 0; column < 4; column++)
            {
                var cell = details.GetOrCreateCell(row, column);
                cell.Text = pairs[row, column];
                if (column % 2 == 0) cell.Background = _labelShade;
            }
        }

        // Earnings and deductions side by side, with their totals in the last row.
        var lines = body.AddTable(Width, spacing: 16);
        lines.SetColumnWidths(160, 87, 160, 88);
        var header = lines.AddRow();
        string[] headings = ["Earnings", "Amount", "Deductions", "Amount"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).Background = _headerTint;
        }

        int count = Math.Max(input.Earnings.Count, input.Deductions.Count);
        for (int i = 0; i < count; i++)
        {
            var row = lines.AddRow();
            if (i < input.Earnings.Count)
            {
                row.GetCell(0).Text = input.Earnings[i].Label;
                Right(row.GetCell(1), input.Earnings[i].Amount.ToString("N2"));
            }
            if (i < input.Deductions.Count)
            {
                row.GetCell(2).Text = input.Deductions[i].Label;
                Right(row.GetCell(3), input.Deductions[i].Amount.ToString("N2"));
            }
        }

        decimal earnings = input.Earnings.Sum(line => line.Amount);
        decimal deductions = input.Deductions.Sum(line => line.Amount);
        var totals = lines.AddRow();
        totals.GetCell(0).Text = "Total earnings";
        Right(totals.GetCell(1), earnings.ToString("N2"));
        totals.GetCell(2).Text = "Total deductions";
        Right(totals.GetCell(3), deductions.ToString("N2"));
        for (int column = 0; column < 4; column++) totals.GetCell(column).Background = _labelShade;

        // Net pay in a highlighted row of its own. The cell backgrounds fill the row and the amount is right-aligned in
        // the same column edge as the totals above; flow items stack, so text never sits on a flow rectangle.
        var net = body.AddTable(Width, spacing: 16);
        net.SetColumnWidths(320, 175);
        var netRow = net.AddRow();
        netRow.Height = 28;
        netRow.GetCell(0).Text = "Net pay";
        Right(netRow.GetCell(1), (earnings - deductions).ToString("N2"));
        netRow.GetCell(0).Background = _netTint;
        netRow.GetCell(1).Background = _netTint;

        body.AddText("Paid by bank transfer to the account on file.", spacing: 14, fontSize: 9);

        return document;
    }

    #endregion

    #region Private Methods

    private static void Right(PdfTableCell cell, string text)
    {
        cell.Text = text;
        cell.HAlign = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
