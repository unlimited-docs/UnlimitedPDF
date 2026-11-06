namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Account statement demo. All names and numbers are fictional.
/// </summary>
public sealed class AccountStatementInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the account holder's name.
    /// </summary>
    public string AccountHolder { get; set; } = "Rahul Verma";

    /// <summary>
    /// Gets or sets the account number, shown masked.
    /// </summary>
    public string AccountNumber { get; set; } = "XXXX-XXXX-4821";

    /// <summary>
    /// Gets or sets how many generated transactions to add after the listed ones (0 to 1,000), to see the statement run onto more pages.
    /// </summary>
    public int GeneratedTransactions { get; set; } = 60;

    /// <summary>
    /// Gets or sets the balance at the start of the period.
    /// </summary>
    public decimal OpeningBalance { get; set; } = 25400.00m;

    /// <summary>
    /// Gets or sets the last day of the period.
    /// </summary>
    public DateTime PeriodEnd { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);

    /// <summary>
    /// Gets or sets the first day of the period.
    /// </summary>
    public DateTime PeriodStart { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

    /// <summary>
    /// Gets the listed transactions. The form adds and removes rows.
    /// </summary>
    public List<StatementLine> Transactions { get; } =
    [
        new StatementLine { Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1), Description = "Salary credit", Amount = 68000m },
        new StatementLine { Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1).AddDays(2), Description = "Rent transfer", Amount = -22000m },
        new StatementLine { Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1).AddDays(4), Description = "Card payment, grocery store", Amount = -3460.50m }
    ];

    #endregion
}
