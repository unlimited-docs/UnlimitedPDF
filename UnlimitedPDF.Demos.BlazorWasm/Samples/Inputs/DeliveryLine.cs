namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One line of a delivery note: what was ordered and what was shipped.
/// </summary>
public sealed class DeliveryLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the item code.
    /// </summary>
    public string Code { get; set; } = "NEW-001";

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = "New item";

    /// <summary>
    /// Gets or sets the quantity ordered.
    /// </summary>
    public int Ordered { get; set; } = 1;

    /// <summary>
    /// Gets or sets the quantity shipped.
    /// </summary>
    public int Shipped { get; set; } = 1;

    #endregion
}
