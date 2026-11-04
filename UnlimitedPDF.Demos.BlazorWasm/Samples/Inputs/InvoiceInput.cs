namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Invoice demo, with fictional default data.
/// </summary>
public sealed class InvoiceInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the buyer's address line.
    /// </summary>
    public string BuyerAddress { get; set; } = "456 Code Street, Hyderabad, TS 500001";

    /// <summary>
    /// Gets or sets the buyer's name.
    /// </summary>
    public string BuyerName { get; set; } = "UnlimitedPDF";

    /// <summary>
    /// Gets or sets the invoice date.
    /// </summary>
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the invoice number.
    /// </summary>
    public string InvoiceNumber { get; set; } = $"INV-{DateTime.Today:yyyy-MM-dd}-001";

    /// <summary>
    /// Gets the line items. The form adds and removes rows.
    /// </summary>
    public List<InvoiceLine> Items { get; } =
    [
        new InvoiceLine { Description = "Wireless Keyboard", Photo = "keyboard.jpg", Quantity = 2, Rate = 1250.00m },
        new InvoiceLine { Description = "USB-C Docking Station", Photo = "dock.jpg", Quantity = 1, Rate = 4500.00m }
    ];

    /// <summary>
    /// Gets or sets the seller's address line.
    /// </summary>
    public string SellerAddress { get; set; } = "123 Tech Park, Bangalore, KA 560001";

    /// <summary>
    /// Gets or sets the seller's name.
    /// </summary>
    public string SellerName { get; set; } = "ABC Electronics Pvt. Ltd.";

    /// <summary>
    /// Gets or sets the seller's tax number.
    /// </summary>
    public string SellerTaxId { get; set; } = "GSTIN: 222222222222225";

    #endregion
}
