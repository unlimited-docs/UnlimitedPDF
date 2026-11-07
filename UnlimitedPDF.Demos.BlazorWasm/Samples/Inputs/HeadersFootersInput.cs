namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Headers and footers demo: a report of sections, each a heading and a table, between a header and
/// a footer.
/// </summary>
public sealed class HeadersFootersInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether the first page has its own, taller header (First-page settings).
    /// </summary>
    public bool DifferentFirstPage { get; set; } = true;

    /// <summary>
    /// Gets or sets the most pages the report may have, or 0 for no limit. A report that needs more pages fails to save
    /// and names the content that does not fit.
    /// </summary>
    public int MaxPages { get; set; }

    /// <summary>
    /// Gets or sets how many rows each section's table has (1 to 100).
    /// </summary>
    public int RowsPerSection { get; set; } = 12;

    /// <summary>
    /// Gets or sets how many sections the report has (1 to 30).
    /// </summary>
    public int SectionCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the report title shown in the header.
    /// </summary>
    public string Title { get; set; } = "Regional sales report";

    #endregion
}
