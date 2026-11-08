namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Bookmarks and page numbers demo's inputs: the files with their bookmark titles, and the footer text.
/// </summary>
public sealed class BookmarksAndNumbersInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the files to add, in order; each file's <see cref="MergeFileInput.BookmarkTitle"/> names its bookmark.
    /// </summary>
    public List<MergeFileInput> Files { get; } = new();

    /// <summary>
    /// Gets or sets the footer text of the built pages; {page} and {pages} count every page of the merged document.
    /// </summary>
    public string FooterText { get; set; } = "Page {page} of {pages}";

    #endregion
}
