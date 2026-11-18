namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Extract pages and Remove pages demos' inputs: one file and the pages to extract or remove.
/// </summary>
public sealed class PageSelectionInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the file to take pages from.
    /// </summary>
    public MergeFileInput? File { get; set; }

    /// <summary>
    /// Gets or sets the pages: page numbers and ranges separated by commas, such as "4, 1-2", read with
    /// <see cref="PdfPageRange.Parse(string)"/>.
    /// </summary>
    public string Pages { get; set; } = string.Empty;

    #endregion
}
