namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One transaction on an account statement: a credit when the amount is positive, a debit when it is negative.
/// </summary>
public sealed class StatementLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the amount: positive for a credit, negative for a debit.
    /// </summary>
    public decimal Amount { get; set; } = -100m;

    /// <summary>
    /// Gets or sets the transaction date.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = "New transaction";

    #endregion
}
