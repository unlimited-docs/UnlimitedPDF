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
            "Build documents from code: pages, text, tables, and images.",
            [TextAndPages, Tables, Images]),
        new SampleCategory(
            "Real-life documents",
            "Complete documents you can edit and download.",
            [Invoice, MedicalReport, Receipt, Certificate, EmployeeDirectory])
    ];

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
    /// Gets the Employee directory demo.
    /// </summary>
    public static SampleDemo EmployeeDirectory { get; } = new(
        "Employee directory",
        "A long table that continues across pages and repeats its header row on every page.",
        "playground/documents/employee-directory",
        "EmployeeDirectorySample.cs",
        "employee-directory.pdf",
        "users");

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
