namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One line item of a Fonts and languages invoice.
/// </summary>
public sealed class LanguageInvoiceLine
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the line amount: quantity times rate.
    /// </summary>
    public decimal Amount => Quantity * Rate;

    /// <summary>
    /// Gets or sets the item description, in any script.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of one item.
    /// </summary>
    public decimal Rate { get; set; }

    #endregion
}
