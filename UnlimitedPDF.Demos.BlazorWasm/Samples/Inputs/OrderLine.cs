namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One line of a purchase order or a quotation.
/// </summary>
public sealed class OrderLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the item or service description.
    /// </summary>
    public string Description { get; set; } = "New item";

    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of one unit.
    /// </summary>
    public decimal UnitPrice { get; set; } = 100m;

    #endregion
}
