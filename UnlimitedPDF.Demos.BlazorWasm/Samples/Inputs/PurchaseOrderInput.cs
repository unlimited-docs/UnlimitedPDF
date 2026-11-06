namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Purchase order demo. All names and numbers are fictional.
/// </summary>
public sealed class PurchaseOrderInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets how many generated lines to add after the listed ones (0 to 500), to see the order run onto more pages.
    /// </summary>
    public int GeneratedLines { get; set; }

    /// <summary>
    /// Gets the order lines. The form adds and removes rows.
    /// </summary>
    public List<OrderLine> Lines { get; } =
    [
        new OrderLine { Description = "Steel brackets, 40 mm", Quantity = 500, UnitPrice = 0.85m },
        new OrderLine { Description = "Hex bolts M8 x 30", Quantity = 2000, UnitPrice = 0.12m },
        new OrderLine { Description = "Bearing 6204-2RS", Quantity = 120, UnitPrice = 4.60m },
        new OrderLine { Description = "Conveyor belt, 5 m", Quantity = 4, UnitPrice = 185.00m }
    ];

    /// <summary>
    /// Gets or sets the order date.
    /// </summary>
    public DateTime OrderDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the purchase order number.
    /// </summary>
    public string OrderNumber { get; set; } = "PO-2026-0418";

    /// <summary>
    /// Gets or sets the shipping cost.
    /// </summary>
    public decimal Shipping { get; set; } = 75m;

    /// <summary>
    /// Gets or sets the delivery address.
    /// </summary>
    public string ShipToAddress { get; set; } = "Plant 2, 18 Foundry Road, Pune, MH 411019";

    /// <summary>
    /// Gets or sets the tax rate, in percent.
    /// </summary>
    public decimal TaxPercent { get; set; } = 18m;

    /// <summary>
    /// Gets or sets the vendor's address.
    /// </summary>
    public string VendorAddress { get; set; } = "44 Industrial Estate, Chennai, TN 600058";

    /// <summary>
    /// Gets or sets the vendor's name.
    /// </summary>
    public string VendorName { get; set; } = "Apex Components Ltd.";

    #endregion
}
