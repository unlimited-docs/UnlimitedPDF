namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One purchased item on a receipt.
/// </summary>
public sealed class ReceiptLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = "New item";

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    public decimal Price { get; set; } = 50m;

    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    public int Quantity { get; set; } = 1;

    #endregion
}
