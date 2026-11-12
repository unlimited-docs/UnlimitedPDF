namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One playground demo: what the sidebar, the home page, and the demo page show about it.
/// </summary>
/// <param name="Title">The demo name, for example "Invoice".</param>
/// <param name="Description">One sentence saying what the demo shows. It is also the page's meta description.</param>
/// <param name="Route">The demo's URL path without a leading slash, for example "playground/documents/invoice".</param>
/// <param name="SourceFile">The sample builder file shown in the code tab, for example "InvoiceSample.cs".</param>
/// <param name="FileName">The file name a downloaded PDF gets, for example "invoice.pdf".</param>
/// <param name="Icon">The icon name drawn by the Icon component.</param>
/// <param name="PageTitle">
/// The browser tab and search result title before the site name: the task the demo shows, with "PDF" and "C#", for
/// example "Create a PDF invoice in C#". It is unique and keeps <see cref="DocumentTitle"/> to 60 characters or fewer.
/// </param>
/// <param name="Heading">The demo page's heading when it says more than the short sidebar title, or null to use the title.</param>
public sealed record SampleDemo(
    string Title,
    string Description,
    string Route,
    string SourceFile,
    string FileName,
    string Icon,
    string PageTitle,
    string? Heading = null)
{
    #region Fields and Constants

    /// <summary>
    /// The site name that ends every page title.
    /// </summary>
    public const string SiteName = "UnlimitedPDF";

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the full page title: <see cref="PageTitle"/> followed by the site name.
    /// </summary>
    public string DocumentTitle => $"{PageTitle} · {SiteName}";

    #endregion
}
