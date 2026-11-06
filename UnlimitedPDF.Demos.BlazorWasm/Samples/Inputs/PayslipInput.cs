namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Payslip demo. All names and numbers are fictional. A payslip is one page, so too many lines make it fail to save.
/// </summary>
public sealed class PayslipInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the deductions. The form adds and removes rows.
    /// </summary>
    public List<PayslipLine> Deductions { get; } =
    [
        new PayslipLine { Label = "Provident fund", Amount = 5400m },
        new PayslipLine { Label = "Professional tax", Amount = 200m },
        new PayslipLine { Label = "Income tax (TDS)", Amount = 6150m }
    ];

    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    public string Department { get; set; } = "Warehouse operations";

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string Designation { get; set; } = "Shift supervisor";

    /// <summary>
    /// Gets the earnings. The form adds and removes rows.
    /// </summary>
    public List<PayslipLine> Earnings { get; } =
    [
        new PayslipLine { Label = "Basic pay", Amount = 45000m },
        new PayslipLine { Label = "House rent allowance", Amount = 18000m },
        new PayslipLine { Label = "Shift allowance", Amount = 4500m },
        new PayslipLine { Label = "Overtime", Amount = 3200m }
    ];

    /// <summary>
    /// Gets or sets the employee ID.
    /// </summary>
    public string EmployeeId { get; set; } = "GL-10482";

    /// <summary>
    /// Gets or sets the employee's name.
    /// </summary>
    public string EmployeeName { get; set; } = "Sanjay Kulkarni";

    /// <summary>
    /// Gets or sets the payment date.
    /// </summary>
    public DateTime PayDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the pay period, for example "October 2026".
    /// </summary>
    public string PayPeriod { get; set; } = DateTime.Today.AddMonths(-1).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    #endregion
}
