using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Bulk invoices: many invoices made in one run with the Invoice demo's builder, each for its own buyer and items,
/// each saved as its own PDF, ready to be zipped.
/// </summary>
public static class BulkInvoicesSample
{
    #region Fields and Constants

    private static readonly string[] Buyers =
    [
        "Fabrikam Traders", "Contoso Retail", "Northwind Foods", "Tailspin Toys", "Wide World Importers", "Proseware Labs",
        "Litware Systems", "Adventure Works", "Alpine Ski House", "Coho Vineyard",
    ];

    private static readonly (string Description, decimal Rate)[] Products =
    [
        ("Wireless mouse", 899m), ("Mechanical keyboard", 4_499m), ("27-inch monitor", 18_999m), ("USB-C dock", 7_999m),
        ("Laptop stand", 1_299m), ("Webcam 1080p", 3_499m), ("Headset", 2_999m), ("Portable SSD 1 TB", 8_499m),
    ];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Makes the invoices.
    /// </summary>
    /// <param name="input">How many invoices (kept between 1 and <see cref="BulkInvoicesInput.MaxCount"/>) and the seller.</param>
    /// <param name="images">The invoice logo ("invoice-logo.png").</param>
    /// <returns>Each invoice's file name and bytes, in number order.</returns>
    public static IReadOnlyList<(string FileName, byte[] Bytes)> Build(BulkInvoicesInput input, SampleImages images)
    {
        int count = Math.Clamp(input.Count, 1, BulkInvoicesInput.MaxCount);
        var invoices = new List<(string, byte[])>(count);
        for (int i = 0; i < count; i++)
        {
            var invoice = Invoice(input, i);
            using var stream = new MemoryStream();
            InvoiceSample.Build(invoice, images).Save(stream);
            invoices.Add(($"{invoice.InvoiceNumber}.pdf", stream.ToArray()));
        }

        return invoices;
    }

    /// <summary>
    /// Returns the data of one invoice of the run: its number, buyer, date, and two to four items, all from its index.
    /// </summary>
    /// <param name="input">The run's inputs.</param>
    /// <param name="index">The 0-based invoice index.</param>
    /// <returns>The invoice data, without product photos.</returns>
    public static InvoiceInput Invoice(BulkInvoicesInput input, int index)
    {
        var invoice = new InvoiceInput
        {
            SellerName = input.SellerName,
            BuyerName = Buyers[index % Buyers.Length],
            InvoiceNumber = $"INV-2026-{index + 1:0000}",
            InvoiceDate = new DateTime(2026, 11, 1).AddDays(index % 28),
        };
        invoice.Items.Clear();
        for (int item = 0; item < 2 + index % 3; item++)
        {
            var (description, rate) = Products[(index + item * 3) % Products.Length];
            invoice.Items.Add(new InvoiceLine { Description = description, Rate = rate, Quantity = 1 + (index + item) % 5 });
        }

        return invoice;
    }

    #endregion

    #endregion
}
