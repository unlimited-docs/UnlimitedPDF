namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Medical report demo. Every name and value is fictional.
/// </summary>
public sealed class MedicalReportInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the patient's age in years.
    /// </summary>
    public int Age { get; set; } = 34;

    /// <summary>
    /// Gets or sets the date the sample was collected.
    /// </summary>
    public DateTime CollectedOn { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the patient's identifier.
    /// </summary>
    public string PatientId { get; set; } = "PID-000123";

    /// <summary>
    /// Gets or sets the patient's name.
    /// </summary>
    public string PatientName { get; set; } = "Alex Sample";

    /// <summary>
    /// Gets or sets the referring doctor.
    /// </summary>
    public string ReferredBy { get; set; } = "Dr. Jamie Example";

    /// <summary>
    /// Gets or sets the date the report was issued.
    /// </summary>
    public DateTime ReportedOn { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets the test results. The form adds and removes rows.
    /// </summary>
    public List<LabResult> Results { get; } =
    [
        new LabResult { Test = "Hemoglobin", Result = "13.5", Unit = "g/dL", Low = 12.0, High = 15.5 },
        new LabResult { Test = "White blood cells", Result = "11.8", Unit = "x10^3/uL", Low = 4.0, High = 11.0 },
        new LabResult { Test = "Red blood cells", Result = "4.6", Unit = "x10^6/uL", Low = 4.2, High = 5.4 },
        new LabResult { Test = "Platelets", Result = "250", Unit = "x10^3/uL", Low = 150, High = 400 },
        new LabResult { Test = "Fasting glucose", Result = "108", Unit = "mg/dL", Low = 70, High = 99 }
    ];

    /// <summary>
    /// Gets or sets the patient's sex as recorded.
    /// </summary>
    public string Sex { get; set; } = "Female";

    #endregion
}
