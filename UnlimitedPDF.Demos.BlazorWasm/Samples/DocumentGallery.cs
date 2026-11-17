namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The document demos whose card pictures are tracked against their sources and images, in catalogue order. The
/// captures of the other scenarios are listed in assets/ARTWORK.md.
/// </summary>
public static class DocumentGallery
{
    #region Fields and Constants

    // Built on first use, after the catalogue's demo properties exist.
    private static IReadOnlyList<GalleryDocument>? _documents;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the gallery documents in catalogue order.
    /// </summary>
    public static IReadOnlyList<GalleryDocument> Documents => _documents ??=
    [
        new(SampleCatalog.Invoice, "invoice", ["invoice-logo.png", "keyboard.jpg", "dock.jpg"]),
        new(SampleCatalog.Quotation, "quotation", ["quotation-logo.png"]),
        new(SampleCatalog.PurchaseOrder, "purchase-order", ["purchase-order-logo.png"]),
        new(SampleCatalog.DeliveryNote, "delivery-note", ["delivery-note-logo.png"]),
        new(SampleCatalog.AccountStatement, "account-statement", ["account-statement-logo.png"]),
        new(SampleCatalog.Payslip, "payslip", ["payslip-logo.png"]),
        new(SampleCatalog.MedicalReport, "medical-report", ["medical-report-logo.png"]),
        new(SampleCatalog.Receipt, "receipt", ["receipt-logo.png"])
    ];

    #endregion
}
