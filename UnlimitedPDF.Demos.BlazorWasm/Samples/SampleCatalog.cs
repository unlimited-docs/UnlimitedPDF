namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The single list of playground demos. The gallery, the home page, and the search all read it, so a new capability (for
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
    /// Gets the demo categories in catalog order.
    /// </summary>
    public static IReadOnlyList<SampleCategory> Categories => _categories ??=
    [
        new SampleCategory(
            "Create",
            "Build documents from code: pages, text, tables, images, and headers and footers.",
            [TextAndPages, Tables, Images, HeadersFooters],
            Id: "create",
            Icon: "pen",
            Tone: "blue",
            Snippet: "create"),
        new SampleCategory(
            "Merge",
            "Join existing PDFs, pick their pages, and build complete document packs. Try your own files.",
            [JoinFiles, CoverAndRanges, PickPages, BookmarksAndNumbers, DocumentPacks],
            Id: "merge",
            Icon: "merge",
            Tone: "teal",
            Snippet: "merge",
            Groups:
            [
                new SampleGroup("Operations", [JoinFiles, CoverAndRanges, PickPages, BookmarksAndNumbers]),
                new SampleGroup("Packs", [DocumentPacks])
            ]),
        new SampleCategory(
            "Split and extract",
            "Split a PDF into parts, every few pages or by the page ranges you choose, or extract or remove the pages you choose. Try your own file.",
            [SplitEveryN, SplitRanges, ExtractPages, RemovePages],
            Id: "split",
            Icon: "split",
            Tone: "amber",
            Snippet: "split",
            Groups:
            [
                new SampleGroup("Split", [SplitEveryN, SplitRanges]),
                new SampleGroup("Extract and remove", [ExtractPages, RemovePages])
            ]),
        new SampleCategory(
            "HTML to PDF",
            "Convert HTML with CSS, tables, lists, and images into PDF in your browser. Edit a template, try your own file, or see how known HTML to PDF problems come out.",
            [HtmlToPdf, CommonProblems],
            Id: "html-to-pdf",
            Icon: "code",
            Tone: "purple",
            Snippet: "html"),
        new SampleCategory(
            "Languages",
            "Draw Hindi, Telugu, Tamil, French, and mixed text with your own fonts, shaped by each font's rules. Try your own fonts.",
            [FontsAndLanguages],
            Id: "languages",
            Icon: "language",
            Tone: "green",
            Snippet: "languages"),
        new SampleCategory(
            "Real-life documents",
            "Complete documents you can edit and download.",
            [Invoice, BulkInvoices, AccountStatement, InventoryReport, Payslip, Contract, MedicalReport, KpiReport, ShippingLabels],
            Id: "documents",
            Icon: "invoice",
            Tone: "navy",
            Snippet: "documents")
    ];

    /// <summary>
    /// Gets the document types the Invoice demo also makes (same shape: parties, line items, totals), in the order its
    /// choice lists them. They are not demos of their own in the catalog.
    /// </summary>
    public static IReadOnlyList<SampleDemo> InvoiceTypes => [Quotation, PurchaseOrder, DeliveryNote, Receipt];

    /// <summary>
    /// Gets every demo page in catalog order, each type the Invoice demo makes right after it: the pages the sitemap
    /// and llms.txt list, each with its own address, title, and description.
    /// </summary>
    public static IReadOnlyList<SampleDemo> Pages =>
        Categories.SelectMany(c => c.Demos).Distinct().SelectMany(d => d == Invoice ? [d, .. InvoiceTypes] : new[] { d }).ToList();

    /// <summary>
    /// Gets the Account statement demo.
    /// </summary>
    public static SampleDemo AccountStatement { get; } = new(
        "Account statement",
        "A bank statement with a first-page summary, a running balance, and the header row on every page.",
        "playground/documents/account-statement",
        "AccountStatementSample.cs",
        "account-statement.pdf",
        "table",
        PageTitle: "Create a PDF bank statement in C#")
    {
        Domains = ["Finance"],
    };

    /// <summary>
    /// Gets the Bookmarks and page numbers merge demo.
    /// </summary>
    public static SampleDemo BookmarksAndNumbers { get; } = new(
        "Bookmarks & numbers",
        "Name each file's bookmark; the contents and closing pages show page numbers that count every merged page.",
        "playground/merge/bookmarks-and-numbers",
        "BookmarksAndNumbersSample.cs",
        "bookmarks-and-numbers.pdf",
        "bookmark",
        PageTitle: "Merged PDF bookmarks and page numbers in C#");

    /// <summary>
    /// Gets the Bulk invoices demo: many invoices made in one run and zipped.
    /// </summary>
    public static SampleDemo BulkInvoices { get; } = new(
        "Bulk invoices",
        "Make up to 1,000 invoices in your browser, see the total time and the time per invoice, and download them all as one zip file.",
        "playground/documents/bulk-invoices",
        "BulkInvoicesSample.cs",
        "invoices.zip",
        "copy",
        PageTitle: "Generate invoices in bulk in C#: 1,000 PDFs")
    {
        Domains = ["Finance"],
    };

    /// <summary>
    /// Gets the Common problems demo: known HTML to PDF problems, each as a small editable document.
    /// </summary>
    public static SampleDemo CommonProblems { get; } = new(
        "Common problems",
        "Rows cut at page breaks, headers that don't repeat, long URLs, nested tables and lists, and print styles: open a problem, see the PDF, edit the HTML.",
        "playground/html/common-problems",
        "HtmlToPdfSample.cs",
        "common-html-to-pdf-problems.pdf",
        "warning",
        PageTitle: "Common HTML to PDF problems in C#",
        Heading: "Common HTML to PDF problems");

    /// <summary>
    /// Gets the Contract demo: an agreement converted from HTML.
    /// </summary>
    public static SampleDemo Contract { get; } = new(
        "Contract",
        "An employment agreement written in HTML: numbered clauses, long paragraphs that wrap across pages, and page X of Y.",
        "playground/documents/contract",
        "ContractSample.cs",
        "contract.pdf",
        "pen",
        PageTitle: "Create a PDF contract from HTML in C#")
    {
        Domains = ["Legal", "HR"],
    };

    /// <summary>
    /// Gets the Cover and page ranges merge demo.
    /// </summary>
    public static SampleDemo CoverAndRanges { get; } = new(
        "Cover + ranges",
        "Put a built cover page in front of chosen page ranges from each file.",
        "playground/merge/cover-and-ranges",
        "CoverAndRangesSample.cs",
        "cover-and-ranges.pdf",
        "cover",
        PageTitle: "Merge a cover page and PDF page ranges in C#");

    /// <summary>
    /// Gets the Delivery note demo.
    /// </summary>
    public static SampleDemo DeliveryNote { get; } = new(
        "Delivery note",
        "A delivery note that lists ordered and shipped quantities and keeps the signature block together.",
        "playground/documents/invoice?type=delivery-note",
        "DeliveryNoteSample.cs",
        "delivery-note.pdf",
        "receipt",
        PageTitle: "Create a PDF delivery note in C#")
    {
        Domains = ["Logistics"],
    };

    /// <summary>
    /// Gets the Document packs merge demo: a finance, HR, or healthcare pack, generated and merged behind a cover page.
    /// </summary>
    public static SampleDemo DocumentPacks { get; } = new(
        "Document packs",
        "Generate a month-end, employee, or patient pack from the other demos and merge it behind a cover page, with a bookmark per document.",
        "playground/merge/document-packs",
        "MergePackSample.cs",
        "document-pack.pdf",
        "calendar",
        PageTitle: "Generate and merge PDF document packs in C#")
    {
        Domains = ["Finance", "HR", "Healthcare"],
    };

    /// <summary>
    /// Gets the Extract pages demo: the chosen pages of one PDF, in the order written, in a new PDF.
    /// </summary>
    public static SampleDemo ExtractPages { get; } = new(
        "Extract pages",
        "Copy the pages you choose, in your order, into one new PDF, such as 4, 1-2. A page can appear more than once.",
        "playground/split/extract-pages",
        "ExtractPagesSample.cs",
        "extracted-pages.pdf",
        "extract",
        PageTitle: "Extract pages from a PDF in C#");

    /// <summary>
    /// Gets the Headers and footers demo.
    /// </summary>
    public static SampleDemo HeadersFooters { get; } = new(
        "Headers and footers",
        "A report with a header and footer on every page, page numbers, first and last page settings, and content that flows onto new pages.",
        "playground/create/headers-and-footers",
        "HeadersFootersSample.cs",
        "headers-and-footers.pdf",
        "layout",
        PageTitle: "PDF headers, footers, and page numbers in C#");

    /// <summary>
    /// Gets the Fonts and languages demo: an invoice in a chosen language, drawn with fonts the visitor uploads.
    /// </summary>
    public static SampleDemo FontsAndLanguages { get; } = new(
        "Fonts and languages",
        "An invoice in Hindi, Telugu, Tamil, French, or a mix, drawn with text calls or HTML, with included free fonts or your own, which stay in your browser.",
        "playground/languages/fonts-and-languages",
        "FontsAndLanguagesSample.cs",
        "fonts-and-languages.pdf",
        "language",
        PageTitle: "Multilingual PDFs with your own fonts in C#")
    {
        Domains = ["Finance"],
    };

    /// <summary>
    /// Gets the HTML to PDF demo.
    /// </summary>
    public static SampleDemo HtmlToPdf { get; } = new(
        "HTML to PDF",
        "Pick a template or paste your own HTML, then convert it with the built-in renderer: no engine required, nothing uploaded.",
        "playground/html/html-to-pdf",
        "HtmlToPdfSample.cs",
        "html-to-pdf.pdf",
        "html",
        PageTitle: "Convert HTML to PDF in C#");

    /// <summary>
    /// Gets the Images demo.
    /// </summary>
    public static SampleDemo Images { get; } = new(
        "Images",
        "Place JPEG and PNG images with fit modes, sizes, and inside table cells. Try your own image.",
        "playground/create/images",
        "ImagesSample.cs",
        "images.pdf",
        "image",
        PageTitle: "Add JPEG and PNG images to a PDF in C#");

    /// <summary>
    /// Gets the Inventory report demo: a long table grouped with subtotals.
    /// </summary>
    public static SampleDemo InventoryReport { get; } = new(
        "Inventory report",
        "Stock grouped by warehouse, with a subtotal per warehouse and the header row repeated on every page.",
        "playground/documents/inventory-report",
        "InventoryReportSample.cs",
        "inventory-report.pdf",
        "table",
        PageTitle: "Create a grouped PDF inventory report in C#")
    {
        Domains = ["Retail"],
    };

    /// <summary>
    /// Gets the Invoice demo.
    /// </summary>
    public static SampleDemo Invoice { get; } = new(
        "Invoice",
        "A tax invoice with a logo, product photos in the line items, and computed totals.",
        "playground/documents/invoice",
        "InvoiceSample.cs",
        "invoice.pdf",
        "invoice",
        PageTitle: "Create a PDF invoice in C#")
    {
        Domains = ["Finance", "Retail"],
    };


    /// <summary>
    /// Gets the Join files merge demo.
    /// </summary>
    public static SampleDemo JoinFiles { get; } = new(
        "Join files",
        "Join whole PDFs in one call, in the order you choose.",
        "playground/merge/join-files",
        "JoinFilesSample.cs",
        "joined.pdf",
        "join",
        PageTitle: "Merge PDF files in C#");

    /// <summary>
    /// Gets the KPI report demo: figures and a bar chart on one page.
    /// </summary>
    public static SampleDemo KpiReport { get; } = new(
        "KPI report",
        "Headline figures and a bar chart of monthly revenue, drawn from rectangles on one page.",
        "playground/documents/kpi-report",
        "KpiReportSample.cs",
        "kpi-report.pdf",
        "spark",
        PageTitle: "Create a PDF KPI report with bar charts in C#")
    {
        Domains = ["Management"],
    };

    /// <summary>
    /// Gets the Medical report demo.
    /// </summary>
    public static SampleDemo MedicalReport { get; } = new(
        "Medical report",
        "A laboratory report with patient details, results, reference ranges, and flags.",
        "playground/documents/medical-report",
        "MedicalReportSample.cs",
        "medical-report.pdf",
        "pulse",
        PageTitle: "Create a PDF medical report in C#")
    {
        Domains = ["Healthcare"],
    };

    /// <summary>
    /// Gets the Payslip demo.
    /// </summary>
    public static SampleDemo Payslip { get; } = new(
        "Payslip",
        "A one-page payslip with earnings, deductions, and net pay, held to one page by a page limit.",
        "playground/documents/payslip",
        "PayslipSample.cs",
        "payslip.pdf",
        "users",
        PageTitle: "Create a PDF payslip in C#")
    {
        Domains = ["HR"],
    };

    /// <summary>
    /// Gets the Pick pages merge demo.
    /// </summary>
    public static SampleDemo PickPages { get; } = new(
        "Pick pages",
        "Take the pages you want from a PDF, in the order you want, such as 3, 1-2.",
        "playground/merge/pick-pages",
        "PickPagesSample.cs",
        "picked-pages.pdf",
        "pick",
        PageTitle: "Extract and reorder PDF pages in C#");

    /// <summary>
    /// Gets the Purchase order demo.
    /// </summary>
    public static SampleDemo PurchaseOrder { get; } = new(
        "Purchase order",
        "A purchase order whose item table runs across pages, with the totals kept together at the end.",
        "playground/documents/invoice?type=purchase-order",
        "PurchaseOrderSample.cs",
        "purchase-order.pdf",
        "invoice",
        PageTitle: "Create a PDF purchase order in C#")
    {
        Domains = ["Retail", "Logistics"],
    };

    /// <summary>
    /// Gets the Quotation demo.
    /// </summary>
    public static SampleDemo Quotation { get; } = new(
        "Quotation",
        "A services quotation with its own first-page header, a last-page footer, and terms kept with the signature.",
        "playground/documents/invoice?type=quotation",
        "QuotationSample.cs",
        "quotation.pdf",
        "invoice",
        PageTitle: "Create a PDF quotation in C#")
    {
        Domains = ["Finance", "Retail"],
    };

    /// <summary>
    /// Gets the Receipt demo.
    /// </summary>
    public static SampleDemo Receipt { get; } = new(
        "Receipt",
        "A compact A6 store receipt with items, tax, and payment details.",
        "playground/documents/invoice?type=receipt",
        "ReceiptSample.cs",
        "receipt.pdf",
        "receipt",
        PageTitle: "Create a PDF receipt in C#")
    {
        Domains = ["Retail"],
    };

    /// <summary>
    /// Gets the Remove pages demo: one PDF without the chosen pages.
    /// </summary>
    public static SampleDemo RemovePages { get; } = new(
        "Remove pages",
        "Write the PDF without the pages you choose, such as 1-4. The pages left keep their order.",
        "playground/split/remove-pages",
        "RemovePagesSample.cs",
        "remaining-pages.pdf",
        "remove",
        PageTitle: "Remove pages from a PDF in C#");

    /// <summary>
    /// Gets the Shipping labels demo: address labels on a 3 by 7 sheet.
    /// </summary>
    public static SampleDemo ShippingLabels { get; } = new(
        "Shipping labels",
        "Address labels on A4 sheets of three by seven, outlined for cutting and ready to print.",
        "playground/documents/shipping-labels",
        "ShippingLabelsSample.cs",
        "shipping-labels.pdf",
        "badge",
        PageTitle: "Print PDF shipping labels on A4 sheets in C#")
    {
        Domains = ["Logistics"],
    };

    /// <summary>
    /// Gets the Every N pages split demo.
    /// </summary>
    public static SampleDemo SplitEveryN { get; } = new(
        "Every N pages",
        "Split a PDF into parts of the same number of pages, such as one file per page or every 10 pages.",
        "playground/split/every-n-pages",
        "SplitEveryNSample.cs",
        "every-n-pages.zip",
        "pages",
        PageTitle: "Split a PDF every N pages in C#");

    /// <summary>
    /// Gets the Page ranges split demo.
    /// </summary>
    public static SampleDemo SplitRanges { get; } = new(
        "Page ranges",
        "Make one PDF per page range you choose, such as 1-2, 3, 2-4. Ranges may overlap and leave pages out.",
        "playground/split/page-ranges",
        "SplitRangesSample.cs",
        "page-ranges.zip",
        "ranges",
        PageTitle: "Split a PDF by page ranges in C#");

    /// <summary>
    /// Gets the Tables demo.
    /// </summary>
    public static SampleDemo Tables { get; } = new(
        "Tables",
        "Column widths, colors, borders, padding, alignment, and row height rules.",
        "playground/create/tables",
        "TablesSample.cs",
        "tables.pdf",
        "table",
        PageTitle: "Create PDF tables in C#");

    /// <summary>
    /// Gets the Text and pages demo.
    /// </summary>
    public static SampleDemo TextAndPages { get; } = new(
        "Text and pages",
        "Choose a page size, place text at any position and size, and add pages.",
        "playground/create/text-and-pages",
        "TextBasicsSample.cs",
        "text-and-pages.pdf",
        "text",
        PageTitle: "Add text and pages to a PDF in C#");


    #endregion
}
