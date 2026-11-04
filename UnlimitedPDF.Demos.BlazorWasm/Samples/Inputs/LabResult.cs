namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One laboratory test result with its reference range.
/// </summary>
public sealed class LabResult
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the upper end of the reference range.
    /// </summary>
    public double High { get; set; } = 10;

    /// <summary>
    /// Gets or sets the lower end of the reference range.
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// Gets or sets the measured value as shown, for example "13.5".
    /// </summary>
    public string Result { get; set; } = "5";

    /// <summary>
    /// Gets or sets the test name.
    /// </summary>
    public string Test { get; set; } = "New test";

    /// <summary>
    /// Gets or sets the unit, for example "g/dL".
    /// </summary>
    public string Unit { get; set; } = "";

    #endregion
}
