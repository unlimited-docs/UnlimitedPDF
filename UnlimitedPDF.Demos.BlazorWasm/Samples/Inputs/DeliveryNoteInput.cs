namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Delivery note demo. All names and numbers are fictional.
/// </summary>
public sealed class DeliveryNoteInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the delivery date.
    /// </summary>
    public DateTime DeliveryDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets how many generated lines to add after the listed ones (0 to 500), to see the note run onto more pages.
    /// </summary>
    public int GeneratedLines { get; set; }

    /// <summary>
    /// Gets the delivered items. The form adds and removes rows.
    /// </summary>
    public List<DeliveryLine> Items { get; } =
    [
        new DeliveryLine { Code = "PAL-120", Description = "Euro pallet, timber", Ordered = 40, Shipped = 40 },
        new DeliveryLine { Code = "WRP-500", Description = "Stretch wrap, 500 mm roll", Ordered = 24, Shipped = 20 },
        new DeliveryLine { Code = "BOX-L", Description = "Carton box, large", Ordered = 300, Shipped = 300 }
    ];

    /// <summary>
    /// Gets or sets the delivery note number.
    /// </summary>
    public string NoteNumber { get; set; } = "DN-2026-3307";

    /// <summary>
    /// Gets or sets the customer's order number.
    /// </summary>
    public string OrderNumber { get; set; } = "SO-88214";

    /// <summary>
    /// Gets or sets the recipient's address, one address line per text line.
    /// </summary>
    public string RecipientAddress { get; set; } = "Unit 7, Logistics Park\nNagpur, MH 440023";

    /// <summary>
    /// Gets or sets the recipient's name.
    /// </summary>
    public string RecipientName { get; set; } = "Central Distribution Hub";

    /// <summary>
    /// Gets or sets the sender's address, one address line per text line.
    /// </summary>
    public string SenderAddress { get; set; } = "Gate 3, Port Road\nMumbai, MH 400001";

    #endregion
}
