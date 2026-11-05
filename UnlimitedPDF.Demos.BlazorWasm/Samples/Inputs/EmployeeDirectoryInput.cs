namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Employee directory demo. Employees are generated from fictional names.
/// </summary>
public sealed class EmployeeDirectoryInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the page bottom margin, in points. The table continues on a new page when a row would pass it.
    /// </summary>
    public double BottomMargin { get; set; } = 50;

    /// <summary>
    /// Gets or sets the company name shown in the title.
    /// </summary>
    public string CompanyName { get; set; } = "UnlimitedPDF Demo Co.";

    /// <summary>
    /// Gets or sets how many employees to list (1 to 1,000).
    /// </summary>
    public int EmployeeCount { get; set; } = 120;

    /// <summary>
    /// Gets or sets whether the header row repeats at the top of every continuation page.
    /// </summary>
    public bool RepeatHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the page top margin, in points. Continued rows resume below it.
    /// </summary>
    public double TopMargin { get; set; } = 40;

    #endregion
}
