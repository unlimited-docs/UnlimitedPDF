namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Split demos' inputs: one file, and either the pages per part (Every N pages) or the page ranges (Page ranges).
/// </summary>
public sealed class SplitInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the file to split.
    /// </summary>
    public MergeFileInput? File { get; set; }

    /// <summary>
    /// Gets or sets the number of pages in each part for the Every N pages demo.
    /// </summary>
    public int PagesPerPart { get; set; } = 2;

    /// <summary>
    /// Gets or sets the parts for the Page ranges demo: pages and ranges separated by commas, one part each, such as
    /// "1-2, 3, 2-4".
    /// </summary>
    public string Ranges { get; set; } = "1-2, 3, 2-4";

    #endregion
}
