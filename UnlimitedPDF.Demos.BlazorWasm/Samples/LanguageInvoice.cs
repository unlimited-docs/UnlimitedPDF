using System.Globalization;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One language's sample invoice for the Fonts and languages demo: its labels, which never change, and the seller,
/// buyer, and line items the form starts with. All names are fictional.
/// </summary>
/// <param name="Id">The preset's id, for example <c>telugu</c>.</param>
/// <param name="Name">The language name shown on the preset button.</param>
/// <param name="Labels">The invoice labels in this language.</param>
/// <param name="SellerName">The seller's name.</param>
/// <param name="SellerAddress">The seller's address.</param>
/// <param name="BuyerName">The buyer's name.</param>
/// <param name="BuyerAddress">The buyer's address.</param>
/// <param name="Items">The line items: description, quantity, and rate.</param>
/// <param name="Euro">Whether amounts are in euros (French style) rather than rupees.</param>
public sealed record LanguageInvoice(
    string Id,
    string Name,
    InvoiceLabels Labels,
    string SellerName,
    string SellerAddress,
    string BuyerName,
    string BuyerAddress,
    IReadOnlyList<(string Description, int Quantity, decimal Rate)> Items,
    bool Euro = false)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Formats an amount the way this invoice writes money: <c>₹ 1,500.00</c>, or <c>1 500,00 €</c> for euros.
    /// Formatting is fixed rather than taken from a culture, so it is the same in every browser.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The formatted amount.</returns>
    public string Money(decimal amount)
    {
        string number = amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
        return Euro ? number.Replace(",", " ", StringComparison.Ordinal).Replace(".", ",", StringComparison.Ordinal) + " €" : "₹ " + number;
    }

    #endregion

    #endregion
}

/// <summary>
/// The fixed labels of a sample invoice.
/// </summary>
public sealed record InvoiceLabels(
    string Title,
    string Number,
    string Date,
    string Seller,
    string Buyer,
    string Item,
    string Quantity,
    string Rate,
    string Amount,
    string Total,
    string Thanks);
