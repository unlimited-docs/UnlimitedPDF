namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The single list of playground demos. The sidebar and the home page both read it, so a new capability (for
/// example merge or split) is added here as a new category plus its pages.
/// </summary>
public static class SampleCatalog
{
    #region Fields and Constants

    // Built on first use: static initializers run in source order, so a list built eagerly here would see the demo
    // properties below before they are initialized.
    private static IReadOnlyList<SampleCategory>? _categories;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the demo categories in sidebar order.
    /// </summary>
    public static IReadOnlyList<SampleCategory> Categories => _categories ??=
    [
        new SampleCategory(
            "Create",
            "Build documents from code: pages, text, tables, images, and headers and footers.",
            [TextAndPages, Tables, Images, HeadersFooters]),
        new SampleCategory(
            "Merge",
            "Join existing PDFs, pick their pages, and build complete document packs. Try your own files.",
            [JoinFiles, CoverAndRanges, PickPages, BookmarksAndNumbers, MonthEndPack, EmployeePack, PatientPack]),
        new SampleCategory(
            "Split",
            "Split a PDF into parts: every few pages, or the page ranges you choose. Try your own file.",
            [SplitEveryN, SplitRanges]),
        new SampleCategory(
            "Real-life documents",
            "Complete documents you can edit and download.",
            [Invoice, Quotation, PurchaseOrder, DeliveryNote, AccountStatement, Payslip, MedicalReport, Receipt, Certificate, EmployeeDirectory])
    ];

    /// <summary>
    /// Gets the Account statement demo.
    /// </summary>
    public static SampleDemo AccountStatement { get; } = new(
        "Account statement",
        "A bank statement with a first-page summary, a running balance, and the header row on every page.",
        "playground/documents/account-statement",
        "AccountStatementSample.cs",
        "account-statement.pdf",
        "table");

    /// <summary>
    /// Gets the Bookmarks and page numbers merge demo.
    /// </summary>
    public static SampleDemo BookmarksAndNumbers { get; } = new(
        "Bookmarks & numbers",
        "Name each file's bookmark and number every page of the merged document from the cover's footer.",
        "playground/merge/bookmarks-and-numbers",
        "BookmarksAndNumbersSample.cs",
        "bookmarks-and-numbers.pdf",
        "menu");

    /// <summary>
    /// Gets the Certificate demo.
    /// </summary>
    public static SampleDemo Certificate { get; } = new(
        "Certificate",
        "A certificate of achievement with a seal and a signature image.",
        "playground/documents/certificate",
        "CertificateSample.cs",
        "certificate.pdf",
        "award");

    /// <summary>
    /// Gets the Cover and page ranges merge demo.
    /// </summary>
    public static SampleDemo CoverAndRanges { get; } = new(
        "Cover + ranges",
        "Put a built cover page in front of chosen page ranges from each file.",
        "playground/merge/cover-and-ranges",
        "CoverAndRangesSample.cs",
        "cover-and-ranges.pdf",
        "copy");

    /// <summary>
    /// Gets the Delivery note demo.
    /// </summary>
    public static SampleDemo DeliveryNote { get; } = new(
        "Delivery note",
        "A delivery note that lists ordered and shipped quantities and keeps the signature block together.",
        "playground/documents/delivery-note",
        "DeliveryNoteSample.cs",
        "delivery-note.pdf",
        "receipt");

    /// <summary>
    /// Gets the Employee directory demo.
    /// </summary>
    public static SampleDemo EmployeeDirectory { get; } = new(
        "Employee directory",
        "A long table that continues across pages, repeats its header row, and numbers every page.",
        "playground/documents/employee-directory",
        "EmployeeDirectorySample.cs",
        "employee-directory.pdf",
        "users");

    /// <summary>
    /// Gets the Employee pack merge demo.
    /// </summary>
    public static SampleDemo EmployeePack { get; } = new(
        "Employee pack",
        "A payslip and a certificate, generated and merged behind a cover page.",
        "playground/merge/employee-pack",
        "MergePackSample.cs",
        "employee-pack.pdf",
        "users");

    /// <summary>
    /// Gets the Headers and footers demo.
    /// </summary>
    public static SampleDemo HeadersFooters { get; } = new(
        "Headers and footers",
        "A report with a header and footer on every page, page numbers, first and last page settings, and content that flows onto new pages.",
        "playground/create/headers-and-footers",
        "HeadersFootersSample.cs",
        "headers-and-footers.pdf",
        "spark");

    /// <summary>
    /// Gets the Images demo.
    /// </summary>
    public static SampleDemo Images { get; } = new(
        "Images",
        "Place JPEG and PNG images with fit modes, sizes, and inside table cells. Try your own image.",
        "playground/create/images",
        "ImagesSample.cs",
        "images.pdf",
        "image");

    /// <summary>
    /// Gets the Invoice demo.
    /// </summary>
    public static SampleDemo Invoice { get; } = new(
        "Invoice",
        "A tax invoice with a logo, product photos in the line items, and computed totals.",
        "playground/documents/invoice",
        "InvoiceSample.cs",
        "invoice.pdf",
        "invoice");


    /// <summary>
    /// Gets the Join files merge demo.
    /// </summary>
    public static SampleDemo JoinFiles { get; } = new(
        "Join files",
        "Join whole PDFs in one call, in the order you choose.",
        "playground/merge/join-files",
        "JoinFilesSample.cs",
        "joined.pdf",
        "copy");

    /// <summary>
    /// Gets the Medical report demo.
    /// </summary>
    public static SampleDemo MedicalReport { get; } = new(
        "Medical report",
        "A laboratory report with patient details, results, reference ranges, and flags.",
        "playground/documents/medical-report",
        "MedicalReportSample.cs",
        "medical-report.pdf",
        "pulse");

    /// <summary>
    /// Gets the Month-end pack merge demo.
    /// </summary>
    public static SampleDemo MonthEndPack { get; } = new(
        "Month-end pack",
        "An invoice, a receipt, and an account statement, generated and merged behind a cover page.",
        "playground/merge/month-end-pack",
        "MergePackSample.cs",
        "month-end-pack.pdf",
        "invoice");

    /// <summary>
    /// Gets the Patient pack merge demo.
    /// </summary>
    public static SampleDemo PatientPack { get; } = new(
        "Patient pack",
        "A medical report and a receipt, generated and merged behind a cover page.",
        "playground/merge/patient-pack",
        "MergePackSample.cs",
        "patient-pack.pdf",
        "pulse");

    /// <summary>
    /// Gets the Payslip demo.
    /// </summary>
    public static SampleDemo Payslip { get; } = new(
        "Payslip",
        "A one-page payslip with earnings, deductions, and net pay, held to one page by a page limit.",
        "playground/documents/payslip",
        "PayslipSample.cs",
        "payslip.pdf",
        "users");

    /// <summary>
    /// Gets the Pick pages merge demo.
    /// </summary>
    public static SampleDemo PickPages { get; } = new(
        "Pick pages",
        "Take the pages you want from a PDF, in the order you want, such as 3, 1-2.",
        "playground/merge/pick-pages",
        "PickPagesSample.cs",
        "picked-pages.pdf",
        "text");

    /// <summary>
    /// Gets the Purchase order demo.
    /// </summary>
    public static SampleDemo PurchaseOrder { get; } = new(
        "Purchase order",
        "A purchase order whose item table runs across pages, with the totals kept together at the end.",
        "playground/documents/purchase-order",
        "PurchaseOrderSample.cs",
        "purchase-order.pdf",
        "invoice");

    /// <summary>
    /// Gets the Quotation demo.
    /// </summary>
    public static SampleDemo Quotation { get; } = new(
        "Quotation",
        "A services quotation with its own first-page header, a last-page footer, and terms kept with the signature.",
        "playground/documents/quotation",
        "QuotationSample.cs",
        "quotation.pdf",
        "invoice");

    /// <summary>
    /// Gets the Receipt demo.
    /// </summary>
    public static SampleDemo Receipt { get; } = new(
        "Receipt",
        "A compact A6 store receipt with items, tax, and payment details.",
        "playground/documents/receipt",
        "ReceiptSample.cs",
        "receipt.pdf",
        "receipt");

    /// <summary>
    /// Gets the Every N pages split demo.
    /// </summary>
    public static SampleDemo SplitEveryN { get; } = new(
        "Every N pages",
        "Split a PDF into parts of the same number of pages, such as one file per page or every 10 pages.",
        "playground/split/every-n-pages",
        "SplitEveryNSample.cs",
        "every-n-pages.zip",
        "copy");

    /// <summary>
    /// Gets the Page ranges split demo.
    /// </summary>
    public static SampleDemo SplitRanges { get; } = new(
        "Page ranges",
        "Make one PDF per page range you choose, such as 1-2, 3, 2-4. Ranges may overlap and leave pages out.",
        "playground/split/page-ranges",
        "SplitRangesSample.cs",
        "page-ranges.zip",
        "copy");

    /// <summary>
    /// Gets the Tables demo.
    /// </summary>
    public static SampleDemo Tables { get; } = new(
        "Tables",
        "Column widths, colors, borders, padding, alignment, and row height rules.",
        "playground/create/tables",
        "TablesSample.cs",
        "tables.pdf",
        "table");

    /// <summary>
    /// Gets the Text and pages demo.
    /// </summary>
    public static SampleDemo TextAndPages { get; } = new(
        "Text and pages",
        "Choose a page size, place text at any position and size, and add pages.",
        "playground/create/text-and-pages",
        "TextBasicsSample.cs",
        "text-and-pages.pdf",
        "text");

    #endregion
}
