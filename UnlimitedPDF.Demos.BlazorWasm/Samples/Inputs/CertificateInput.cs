namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Certificate demo, with fictional default data.
/// </summary>
public sealed class CertificateInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the award date.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the line describing the achievement.
    /// </summary>
    public string Description { get; set; } = "for outstanding work building documents with UnlimitedPDF";

    /// <summary>
    /// Gets or sets the recipient's name.
    /// </summary>
    public string Recipient { get; set; } = "Taylor Sample";

    /// <summary>
    /// Gets or sets the signer's name.
    /// </summary>
    public string SignerName { get; set; } = "Jordan Example";

    /// <summary>
    /// Gets or sets the signer's role.
    /// </summary>
    public string SignerTitle { get; set; } = "Director, UnlimitedPDF Academy";

    /// <summary>
    /// Gets or sets the subtitle under the title.
    /// </summary>
    public string Subtitle { get; set; } = "OF ACHIEVEMENT";

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = "CERTIFICATE";

    #endregion
}
