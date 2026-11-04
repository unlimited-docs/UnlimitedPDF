namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One invoice line item.
/// </summary>
public sealed class InvoiceLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the item description.
    /// </summary>
    public string Description { get; set; } = "New item";

    /// <summary>
    /// Gets or sets the built-in product photo file, or an empty string for none.
    /// </summary>
    public string Photo { get; set; } = "";

    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    public decimal Rate { get; set; } = 100m;

    #endregion
}
