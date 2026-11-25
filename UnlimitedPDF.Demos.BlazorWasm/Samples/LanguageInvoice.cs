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
/// <param name="Currency">The currency sign written after amounts in the European style (<c>1 500,00 €</c>), or null
/// for rupees (<c>₹ 1,500.00</c>).</param>
/// <param name="SignFirst">Whether <paramref name="Currency"/> goes before the amount, with comma thousands and a point
/// for decimals (<c>₱1,500.00</c>), as the Philippines writes pesos.</param>
/// <param name="RightToLeft">Whether the invoice is written right to left (Arabic, Persian, Urdu, Hebrew, Dhivehi):
/// the document and its tables read from the right, and amounts are written <c>1,500.00</c> with the currency after
/// (WR-041).</param>
public sealed record LanguageInvoice(
    string Id,
    string Name,
    InvoiceLabels Labels,
    string SellerName,
    string SellerAddress,
    string BuyerName,
    string BuyerAddress,
    IReadOnlyList<(string Description, int Quantity, decimal Rate)> Items,
    string? Currency = null,
    bool SignFirst = false,
    bool RightToLeft = false)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Formats an amount the way this invoice writes money: <c>₹ 1,500.00</c>, <c>1 500,00 €</c> with another currency,
    /// or <c>₱1,500.00</c> with the sign first.
    /// Formatting is fixed rather than taken from a culture, so it is the same in every browser.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The formatted amount.</returns>
    public string Money(decimal amount)
    {
        string number = amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
        if (Currency is null) return "₹ " + number;
        if (RightToLeft) return number + " " + Currency;
        return SignFirst ? Currency + number : number.Replace(",", " ", StringComparison.Ordinal).Replace(".", ",", StringComparison.Ordinal) + " " + Currency;
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
