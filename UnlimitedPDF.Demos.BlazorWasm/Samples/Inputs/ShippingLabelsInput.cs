namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Shipping labels demo's inputs: the sender and how many labels to print.
/// </summary>
public sealed class ShippingLabelsInput
{
    #region Fields and Constants

    /// <summary>
    /// The most labels one run prints: ten sheets.
    /// </summary>
    public const int MaxCount = 210;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets how many labels to print, from 1 to <see cref="MaxCount"/>.
    /// </summary>
    public int Count { get; set; } = 21;

    /// <summary>
    /// Gets or sets the sender line on every label.
    /// </summary>
    public string Sender { get; set; } = "From: Contoso Retail, 12 MG Road, Bengaluru 560001";

    #endregion
}
