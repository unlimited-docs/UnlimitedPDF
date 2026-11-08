using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Merge PDFs demo's inputs: an optional cover page and the files to join, each with an optional page range.
/// </summary>
public sealed class MergeInput
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

/// <summary>
/// One file to join: its loaded PDF, the name its bookmark shows, and an optional page range.
/// </summary>
public sealed class MergeFileInput
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the input.
    /// </summary>
    /// <param name="name">The name shown in the list and as the file's bookmark.</param>
    /// <param name="file">The loaded PDF.</param>
    public MergeFileInput(string name, PdfFile file)
    {
        Name = name;
        File = file;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the loaded PDF.
    /// </summary>
    public PdfFile File { get; }

    /// <summary>
    /// Gets or sets the first page to add, or null for the first page.
    /// </summary>
    public int? FirstPage { get; set; }

    /// <summary>
    /// Gets or sets the last page to add, or null for the last page.
    /// </summary>
    public int? LastPage { get; set; }

    /// <summary>
    /// Gets the name shown in the list and as the file's bookmark.
    /// </summary>
    public string Name { get; }

    #endregion
}
