namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Pick pages demo's inputs: one file and the pages to take from it.
/// </summary>
public sealed class PickPagesInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the file to pick pages from.
    /// </summary>
    public MergeFileInput? File { get; set; }

    /// <summary>
    /// Gets or sets the pages to take, in order: page numbers and ranges separated by commas, such as "3, 1-2".
    /// </summary>
    public string Pages { get; set; } = "3, 1-2";

    #endregion
}
