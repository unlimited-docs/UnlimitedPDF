using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The ten documents of the home page gallery, in the order of the "Real-life documents" demos. The page counts and
/// sizes shown on the tiles are checked against the documents these builders return (PR-012).
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
        new(SampleCatalog.Invoice, "A logo, product photos in the rows, and computed totals.", 1, PdfPageSize.A4, "invoice",
            ["invoice-logo.png", "keyboard.jpg", "dock.jpg"], images => InvoiceSample.Build(new InvoiceInput(), images)),
        new(SampleCatalog.Quotation, "Its own first-page header, a last-page footer, and terms kept with the signature.", 1, PdfPageSize.A4, "quotation",
            ["quotation-logo.png"], images => QuotationSample.Build(new QuotationInput(), images)),
        new(SampleCatalog.PurchaseOrder, "An item table with its totals kept together at the end.", 1, PdfPageSize.A4, "purchase-order",
            ["purchase-order-logo.png"], images => PurchaseOrderSample.Build(new PurchaseOrderInput(), images)),
        new(SampleCatalog.DeliveryNote, "Ordered and shipped quantities, with the signature block kept together.", 1, PdfPageSize.A4, "delivery-note",
            ["delivery-note-logo.png"], images => DeliveryNoteSample.Build(new DeliveryNoteInput(), images)),
        new(SampleCatalog.AccountStatement, "A running balance, with the header row on every page.", 3, PdfPageSize.A4, "account-statement",
            ["account-statement-logo.png"], images => AccountStatementSample.Build(new AccountStatementInput(), images)),
        new(SampleCatalog.Payslip, "Earnings, deductions, and net pay, held to one page.", 1, PdfPageSize.A4, "payslip",
            ["payslip-logo.png"], images => PayslipSample.Build(new PayslipInput(), images)),
        new(SampleCatalog.MedicalReport, "Lab results with reference ranges and flags.", 1, PdfPageSize.A4, "medical-report",
            ["medical-report-logo.png"], images => MedicalReportSample.Build(new MedicalReportInput(), images)),
        new(SampleCatalog.Receipt, "Items, tax, and payment on a small page.", 1, PdfPageSize.A6, "receipt",
            ["receipt-logo.png"], images => ReceiptSample.Build(new ReceiptInput(), images)),
        new(SampleCatalog.Certificate, "A seal and a signature placed as images.", 1, PdfPageSize.A4, "certificate",
            ["certificate-seal.png", "certificate-signature.png"], images => CertificateSample.Build(new CertificateInput(), images)),
        new(SampleCatalog.EmployeeDirectory, "A long table with its header and page numbers on every page.", 4, PdfPageSize.A4, "employee-directory",
            [], _ => EmployeeDirectorySample.Build(new EmployeeDirectoryInput()))
    ];

    #endregion
}
