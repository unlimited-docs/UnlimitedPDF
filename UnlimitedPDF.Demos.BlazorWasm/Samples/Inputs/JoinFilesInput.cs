namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Join files demo's inputs: the files to join, in order.
/// </summary>
public sealed class JoinFilesInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the files to join, in order.
    /// </summary>
    public List<MergeFileInput> Files { get; } = new();

    #endregion
}
