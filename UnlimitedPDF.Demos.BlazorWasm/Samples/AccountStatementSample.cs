using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Account statement: a summary on the first page, then every transaction with a running balance. The table repeats
/// its header row on every page, keeps its last row with the closing balance, and the last page says where the
/// statement ends.
/// </summary>
public static class AccountStatementSample
{
    #region Fields and Constants

    private const double Width = 495;

    private static readonly PdfColor _headerTint = new(0.85, 0.93, 0.92);
    private static readonly PdfColor _navy = new(0.08, 0.21, 0.35);
    private static readonly PdfColor _seaGreen = new(0.18, 0.61, 0.56);
    private static readonly string[] _payees = ["Card payment, grocery store", "Electricity bill", "UPI transfer received", "ATM withdrawal", "Mobile recharge", "Interest credit", "Card payment, fuel"];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The statement data.</param>
    /// <param name="images">The bank's logo ("account-statement-logo.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(AccountStatementInput input, SampleImages images)
    {
        var document = new PdfDocument { MarginTop = 30, MarginBottom = 24, MarginLeft = 50, MarginRight = 50 };

        document.Header.Height = 60;
        document.Header.AddImage(images["account-statement-logo.png"], 0, 0, 150, 50, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
        document.Header.AddText("ACCOUNT STATEMENT", 0, 4, 16, PdfTextAlignment.Right);
        document.Header.AddText($"{input.AccountHolder}  |  {input.AccountNumber}", 0, 26, 9, PdfTextAlignment.Right);
        document.Header.AddText($"{input.PeriodStart:dd MMM yyyy} to {input.PeriodEnd:dd MMM yyyy}", 0, 40, 9, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 57, Width, 1.5, _seaGreen);
        document.Footer.Height = 22;
        document.Footer.AddText("Harborview Savings Bank. Report errors within 30 days.", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        var last = document.AddPageSettings(PdfPageTarget.Last).Footer;
        last.AddText("End of statement", 0, 8, 8);
        last.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 16;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        // Statements list transactions in date order; OrderBy keeps same-day transactions in the order given.
        var transactions = Transactions(input).OrderBy(line => line.Date).ToList();
        decimal credits = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        decimal debits = -transactions.Where(t => t.Amount < 0).Sum(t => t.Amount);
        decimal closing = input.OpeningBalance + credits - debits;

        // The summary comes first in the flow, so it is on the first page only.
        var summary = body.AddTable(Width);
        summary.SetColumnWidths(124, 124, 124, 123);
        var labels = summary.AddRow();
        var values = summary.AddRow();
        string[] names = ["Opening balance", "Total credits", "Total debits", "Closing balance"];
        decimal[] amounts = [input.OpeningBalance, credits, debits, closing];
        for (int column = 0; column < names.Length; column++)
        {
            labels.GetCell(column).Text = names[column];
            labels.GetCell(column).BackgroundColor = _headerTint;
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
            header.GetCell(column).BackgroundColor = _headerTint;
        }

        decimal balance = input.OpeningBalance;
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

        body.AddText($"Closing balance on {input.PeriodEnd:dd MMM yyyy}: {closing:N2}", spacing: 10, fontSize: 11, alignment: PdfTextAlignment.Right);
        body.AddRectangle(Width, 1, _navy, spacing: 4);

        return document;
    }

    #endregion

    #region Private Methods

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    // The listed transactions, then generated ones with fictional, repeatable values spread over the period.
    private static IEnumerable<StatementLine> Transactions(AccountStatementInput input)
    {
        foreach (var line in input.Transactions) yield return line;
        int generated = Math.Clamp(input.GeneratedTransactions, 0, 1000);
        int days = Math.Max(1, (input.PeriodEnd - input.PeriodStart).Days + 1);
        for (int i = 1; i <= generated; i++)
        {
            string payee = _payees[i % _payees.Length];
            decimal amount = payee.EndsWith("received") || payee.StartsWith("Interest") ? 150m + i % 11 * 85m : -(40m + i % 13 * 37.5m);
            yield return new StatementLine { Date = input.PeriodStart.AddDays(i * days / (generated + 1)), Description = payee, Amount = amount };
        }
    }

    #endregion

    #endregion
}
