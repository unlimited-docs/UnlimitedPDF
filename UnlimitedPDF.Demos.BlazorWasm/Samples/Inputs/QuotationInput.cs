namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Quotation demo. All names and numbers are fictional.
/// </summary>
public sealed class QuotationInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the client's address.
    /// </summary>
    public string ClientAddress { get; set; } = "9 Harbour Street, Kochi, KL 682001";

    /// <summary>
    /// Gets or sets the client company.
    /// </summary>
    public string ClientCompany { get; set; } = "Coastal Retail Pvt. Ltd.";

    /// <summary>
    /// Gets or sets the client contact.
    /// </summary>
    public string ClientName { get; set; } = "Priya Menon";

    /// <summary>
    /// Gets or sets the quotation date.
    /// </summary>
    public DateTime QuoteDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the quotation number.
    /// </summary>
    public string QuoteNumber { get; set; } = "Q-2026-117";

    /// <summary>
    /// Gets the quoted services. The form adds and removes rows.
    /// </summary>
    public List<OrderLine> Services { get; } =
    [
        new OrderLine { Description = "Network assessment (on site)", Quantity = 1, UnitPrice = 1800m },
        new OrderLine { Description = "Firewall setup and hardening", Quantity = 2, UnitPrice = 950m },
        new OrderLine { Description = "Managed backup, per month", Quantity = 12, UnitPrice = 240m },
        new OrderLine { Description = "Staff security training (half day)", Quantity = 1, UnitPrice = 600m }
    ];

    /// <summary>
    /// Gets or sets the tax rate, in percent.
    /// </summary>
    public decimal TaxPercent { get; set; } = 18m;

    /// <summary>
    /// Gets or sets how many days the quotation is valid.
    /// </summary>
    public int ValidDays { get; set; } = 30;

    #endregion
}
