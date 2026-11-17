namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The KPI report demo's inputs: the period and the monthly revenue the bar chart draws.
/// </summary>
public sealed class KpiReportInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the company name in the header.
    /// </summary>
    public string Company { get; set; } = "Contoso Retail";

    /// <summary>
    /// Gets the monthly revenue in lakh rupees, one bar each, January first.
    /// </summary>
    public List<decimal> MonthlyRevenue { get; } = [42, 45, 51, 48, 56, 61, 58, 64, 70, 67, 74, 82];

    /// <summary>
    /// Gets or sets the period the report covers.
    /// </summary>
    public string Period { get; set; } = "Financial year 2025-26";

    #endregion
}
