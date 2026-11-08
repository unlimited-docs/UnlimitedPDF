using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Cover + ranges demo's inputs: an optional cover page and the files to join, each with an optional page range.
/// </summary>
public sealed class CoverAndRangesInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether a cover page is built before the files.
    /// </summary>
    public bool AddCover { get; set; } = true;

    /// <summary>
    /// Gets or sets the cover page title.
    /// </summary>
    public string CoverTitle { get; set; } = "Merged documents";

    /// <summary>
    /// Gets the files to join, in order.
    /// </summary>
    public List<MergeFileInput> Files { get; } = new();

    #endregion
}
