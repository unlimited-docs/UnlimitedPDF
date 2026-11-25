// Payslip: a one-page payslip with earnings, deductions, and net pay, held to one page.
// A complete console program: it writes payslip.pdf.
//
// Run it:
//   1. dotnet new console -n PayslipExample
//   2. cd PayslipExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the PayslipExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/payslip-logo.png
//   6. dotnet run
// Then open payslip.pdf in the PayslipExample folder.

using UnlimitedPDF;

namespace PayslipExample;

public static class Program
{
    private const double Width = 495;

    private static readonly PdfColor Green = new(0.18, 0.49, 0.20);
    private static readonly PdfColor HeaderTint = new(0.88, 0.95, 0.88);
    private static readonly PdfColor LabelShade = new(0.96, 0.97, 0.96);
    private static readonly PdfColor NetTint = new(0.80, 0.91, 0.80);

    public static void Main()
    {
        // The sample data: change it, run again, and open payslip.pdf.
        // All names and numbers are fictional. A payslip is one page, so too many lines make it
        // fail to save.
        string employeeName = "Sanjay Kulkarni";
        string employeeId = "GL-10482";
        string department = "Warehouse operations";
        string designation = "Shift supervisor";
        string payPeriod = DateTime.Today.AddMonths(-1)
            .ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        DateTime payDate = DateTime.Today;
        PayLine[] earningLines =
        [
            new("Basic pay", 45000m),
            new("House rent allowance", 18000m),
            new("Shift allowance", 4500m),
            new("Overtime", 3200m),
        ];
        PayLine[] deductionLines =
        [
            new("Provident fund", 5400m),
            new("Professional tax", 200m),
            new("Income tax (TDS)", 6150m),
        ];

        var document = new PdfDocument
        {
            MarginTop = 36,
            MarginBottom = 28,
            MarginLeft = 50,
            MarginRight = 50,
        };

        // A payslip is one page: saving fails, naming what does not fit, if the lines need more.
        document.Body.MaxPages = 1;

        document.Header.Height = 64;
        var contain = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        document.Header.AddImage(PdfImage.FromFile("payslip-logo.png"), 0, 0, 150, 50, contain);
        document.Header.AddText("PAYSLIP", 0, 6, 22, PdfTextAlignment.Right);
        document.Header.AddText($"Pay period: {payPeriod}", 0, 36, 10, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 61, Width, 2, Green);
        document.Footer.Height = 22;
        document.Footer.AddText(
            "This is a computer-generated payslip and needs no signature.", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 18;
        var body = document.Body;

        // Employee details: label and value pairs in a four-column table.
        var details = body.AddTable(Width);
        details.SetColumnWidths(95, 150, 95, 155);
        string[,] pairs =
        {
            { "Employee", employeeName, "Employee ID", employeeId },
            { "Department", department, "Designation", designation },
            { "Pay period", payPeriod, "Pay date", payDate.ToString("dd MMM yyyy") },
        };
        for (int row = 0; row < pairs.GetLength(0); row++)
        {
            for (int column = 0; column < 4; column++)
            {
                var cell = details.GetOrCreateCell(row, column);
                cell.Text = pairs[row, column];
                if (column % 2 == 0)
                {
                    cell.BackgroundColor = LabelShade;
                }
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
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        int count = Math.Max(earningLines.Length, deductionLines.Length);
        for (int i = 0; i < count; i++)
        {
            var row = lines.AddRow();
            if (i < earningLines.Length)
            {
                row.GetCell(0).Text = earningLines[i].Label;
                Right(row.GetCell(1), earningLines[i].Amount.ToString("N2"));
            }

            if (i < deductionLines.Length)
            {
                row.GetCell(2).Text = deductionLines[i].Label;
                Right(row.GetCell(3), deductionLines[i].Amount.ToString("N2"));
            }
        }

        decimal earnings = earningLines.Sum(line => line.Amount);
        decimal deductions = deductionLines.Sum(line => line.Amount);
        var totals = lines.AddRow();
        totals.GetCell(0).Text = "Total earnings";
        Right(totals.GetCell(1), earnings.ToString("N2"));
        totals.GetCell(2).Text = "Total deductions";
        Right(totals.GetCell(3), deductions.ToString("N2"));
        for (int column = 0; column < 4; column++)
        {
            totals.GetCell(column).BackgroundColor = LabelShade;
        }

        // Net pay in a highlighted row of its own. The cell backgrounds fill the row and the
        // amount is right-aligned in the same column edge as the totals above; flow items stack,
        // so text never sits on a flow rectangle.
        var net = body.AddTable(Width, spacing: 16);
        net.SetColumnWidths(320, 175);
        var netRow = net.AddRow();
        netRow.Height = 28;
        netRow.GetCell(0).Text = "Net pay";
        Right(netRow.GetCell(1), (earnings - deductions).ToString("N2"));
        netRow.GetCell(0).BackgroundColor = NetTint;
        netRow.GetCell(1).BackgroundColor = NetTint;

        body.AddText("Paid by bank transfer to the account on file.", spacing: 14, fontSize: 9);

        document.Save("payslip.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("payslip.pdf")}");
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    private sealed record PayLine(string Label, decimal Amount);
}
