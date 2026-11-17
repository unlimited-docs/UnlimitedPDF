namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Bulk invoices demo's inputs: how many invoices to make and who sells them.
/// </summary>
public sealed class BulkInvoicesInput
{
    #region Fields and Constants

    /// <summary>
    /// The most invoices one run makes.
    /// </summary>
    public const int MaxCount = 1000;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets how many invoices to make; the sample keeps it between 1 and <see cref="MaxCount"/>.
    /// </summary>
    public int Count { get; set; } = 25;

    /// <summary>
    /// Gets or sets the seller's name on every invoice.
    /// </summary>
    public string SellerName { get; set; } = "ABC Electronics Pvt. Ltd.";

    #endregion
}
