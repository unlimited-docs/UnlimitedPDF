namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Receipt demo, with fictional default data.
/// </summary>
public sealed class ReceiptInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the purchase date.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets the purchased items. The form adds and removes rows.
    /// </summary>
    public List<ReceiptLine> Items { get; } =
    [
        new ReceiptLine { Name = "Coffee beans 250 g", Quantity = 1, Price = 450m },
        new ReceiptLine { Name = "Oat milk 1 L", Quantity = 2, Price = 180m },
        new ReceiptLine { Name = "Croissant", Quantity = 3, Price = 90m },
        new ReceiptLine { Name = "Paper bag", Quantity = 1, Price = 10m }
    ];

    /// <summary>
    /// Gets or sets how the purchase was paid.
    /// </summary>
    public string PaymentMethod { get; set; } = "Card";

    /// <summary>
    /// Gets or sets the receipt number.
    /// </summary>
    public string ReceiptNumber { get; set; } = "R-000245";

    /// <summary>
    /// Gets or sets the store's address line.
    /// </summary>
    public string StoreAddress { get; set; } = "7 Market Road, Demo City";

    /// <summary>
    /// Gets or sets the store name.
    /// </summary>
    public string StoreName { get; set; } = "UnlimitedPDF Mart";

    /// <summary>
    /// Gets or sets the tax rate, in percent.
    /// </summary>
    public decimal TaxPercent { get; set; } = 5m;

    #endregion
}
