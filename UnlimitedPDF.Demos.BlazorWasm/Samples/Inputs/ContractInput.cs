namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Contract demo's inputs: the two parties, the role, the start date, and the salary.
/// </summary>
public sealed class ContractInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the yearly salary in rupees.
    /// </summary>
    public decimal AnnualSalary { get; set; } = 1_800_000m;

    /// <summary>
    /// Gets or sets the employee's name.
    /// </summary>
    public string EmployeeName { get; set; } = "Priya Sharma";

    /// <summary>
    /// Gets or sets the employer's name.
    /// </summary>
    public string EmployerName { get; set; } = "Northwind Software Pvt. Ltd.";

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string Role { get; set; } = "Senior Software Engineer";

    /// <summary>
    /// Gets or sets the first working day.
    /// </summary>
    public DateTime StartDate { get; set; } = new(2026, 12, 1);

    #endregion
}
