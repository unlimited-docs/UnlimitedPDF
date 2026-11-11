namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The home page's "How do I...?" questions, each answered by one demo (PR-012: each is true of the demo it opens).
/// </summary>
public static class HomeQuestions
{
    #region Fields and Constants

    // Built on first use, after the catalogue's demo properties exist.
    private static IReadOnlyList<HomeQuestion>? _questions;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the questions in the order the page shows them.
    /// </summary>
    public static IReadOnlyList<HomeQuestion> Questions => _questions ??=
    [
        new("Repeat a table header on every page", SampleCatalog.EmployeeDirectory),
        new("Keep totals together at the end of a table", SampleCatalog.PurchaseOrder),
        new("Number every page of a merged document", SampleCatalog.BookmarksAndNumbers),
        new("Hold a document to one page", SampleCatalog.Payslip),
        new("Stop a table row being cut at a page break", SampleCatalog.CommonProblems, "playground/html/common-problems/row-cut-in-half"),
        new("Put a logo and photos in a table", SampleCatalog.Invoice),
        new("Use a different header on the first page", SampleCatalog.Quotation),
        new("Take chosen pages from an existing PDF", SampleCatalog.PickPages)
    ];

    #endregion
}
