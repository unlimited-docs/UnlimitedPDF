namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One earning or deduction on a payslip.
/// </summary>
public sealed class PayslipLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the label, for example "Basic pay".
    /// </summary>
    public string Label { get; set; } = "New line";

    #endregion
}
