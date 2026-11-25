// Account statement: a summary on the first page, then every transaction with a running balance.
// A complete console program: it writes account-statement.pdf.
//
// Run it:
//   1. dotnet new console -n AccountStatementExample
//   2. cd AccountStatementExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the AccountStatementExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/account-statement-logo.png
//   6. dotnet run
// Then open account-statement.pdf in the AccountStatementExample folder.

using UnlimitedPDF;

namespace AccountStatementExample;

public static class Program
{
    private const double Width = 495;

    private static readonly PdfColor HeaderTint = new(0.85, 0.93, 0.92);
    private static readonly PdfColor Navy = new(0.08, 0.21, 0.35);
    private static readonly PdfColor SeaGreen = new(0.18, 0.61, 0.56);

    private static readonly string[] Payees =
    [
        "Card payment, grocery store", "Electricity bill", "UPI transfer received",
        "ATM withdrawal", "Mobile recharge", "Interest credit", "Card payment, fuel",
    ];

    public static void Main()
    {
        // The sample data: change it, run again, and open account-statement.pdf.
        // All names and numbers are fictional. The period is last month.
        string accountHolder = "Rahul Verma";
        string accountNumber = "XXXX-XXXX-4821";
        decimal openingBalance = 25400.00m;
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime periodStart = thisMonth.AddMonths(-1);
        DateTime periodEnd = thisMonth.AddDays(-1);
        StatementLine[] listed =
        [
            new(periodStart, "Salary credit", 68000m),
            new(periodStart.AddDays(2), "Rent transfer", -22000m),
            new(periodStart.AddDays(4), "Card payment, grocery store", -3460.50m),
        ];

        // How many generated transactions to add after the listed ones, to see the statement run
        // onto more pages.
        int generatedTransactions = 60;

        var document = new PdfDocument
        {
            MarginTop = 30, MarginBottom = 24, MarginLeft = 50, MarginRight = 50,
        };

        document.Header.Height = 60;
        var logo = PdfImage.FromFile("account-statement-logo.png");
        var contain = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        document.Header.AddImage(logo, 0, 0, 150, 50, contain);
        document.Header.AddText("ACCOUNT STATEMENT", 0, 4, 16, PdfTextAlignment.Right);
        document.Header.AddText(
            $"{accountHolder}  |  {accountNumber}", 0, 26, 9, PdfTextAlignment.Right);
        document.Header.AddText(
            $"{periodStart:dd MMM yyyy} to {periodEnd:dd MMM yyyy}",
            0, 40, 9, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 57, Width, 1.5, SeaGreen);
        document.Footer.Height = 22;
        document.Footer.AddText("Harborview Savings Bank. Report errors within 30 days.", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        var last = document.AddPageSettings(PdfPageTarget.Last).Footer;
        last.AddText("End of statement", 0, 8, 8);
        last.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 16;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        // Statements list transactions in date order; OrderBy keeps same-day transactions in the
        // order given.
        var transactions = Transactions(listed, generatedTransactions, periodStart, periodEnd)
            .OrderBy(line => line.Date)
            .ToList();
        decimal credits = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        decimal debits = -transactions.Where(t => t.Amount < 0).Sum(t => t.Amount);
        decimal closing = openingBalance + credits - debits;

        // The summary comes first in the flow, so it is on the first page only.
        var summary = body.AddTable(Width);
        summary.SetColumnWidths(124, 124, 124, 123);
        var labels = summary.AddRow();
        var values = summary.AddRow();
        string[] names = ["Opening balance", "Total credits", "Total debits", "Closing balance"];
        decimal[] amounts = [openingBalance, credits, debits, closing];
        for (int column = 0; column < names.Length; column++)
        {
            labels.GetCell(column).Text = names[column];
            labels.GetCell(column).BackgroundColor = HeaderTint;
            Right(values.GetCell(column), amounts[column].ToString("N2"));
        }

        // The last row stays on the page of the closing line (keepWithNext).
        var table = body.AddTable(Width, spacing: 16, keepWithNext: true);
        table.SetColumnWidths(70, 195, 75, 75, 80);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        var header = table.AddRow();
        string[] headings = ["Date", "Description", "Debit", "Credit", "Balance"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        decimal balance = openingBalance;
        foreach (var line in transactions)
        {
            balance += line.Amount;
            var row = table.AddRow();
            row.GetCell(0).Text = line.Date.ToString("dd MMM");
            row.GetCell(1).Text = line.Description;
            Right(row.GetCell(2), line.Amount < 0 ? (-line.Amount).ToString("N2") : "");
            Right(row.GetCell(3), line.Amount > 0 ? line.Amount.ToString("N2") : "");
            Right(row.GetCell(4), balance.ToString("N2"));
        }

        body.AddText(
            $"Closing balance on {periodEnd:dd MMM yyyy}: {closing:N2}",
            spacing: 10, fontSize: 11, alignment: PdfTextAlignment.Right);
        body.AddRectangle(Width, 1, Navy, spacing: 4);

        document.Save("account-statement.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("account-statement.pdf")}");
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    // The listed transactions, then generated ones with fictional, repeatable values spread over
    // the period.
    private static IEnumerable<StatementLine> Transactions(
        StatementLine[] listed, int generated, DateTime periodStart, DateTime periodEnd)
    {
        foreach (var line in listed) yield return line;
        int days = Math.Max(1, (periodEnd - periodStart).Days + 1);
        for (int i = 1; i <= generated; i++)
        {
            string payee = Payees[i % Payees.Length];
            decimal amount = payee.EndsWith("received") || payee.StartsWith("Interest")
                ? 150m + i % 11 * 85m
                : -(40m + i % 13 * 37.5m);
            yield return new StatementLine(
                periodStart.AddDays(i * days / (generated + 1)), payee, amount);
        }
    }

    // One transaction: a positive amount is a credit, a negative one a debit.
    private sealed record StatementLine(DateTime Date, string Description, decimal Amount);
}
