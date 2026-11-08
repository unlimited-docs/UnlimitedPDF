using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// One file in a merge demo: its loaded PDF, its name, an optional page range, and an optional bookmark title.
/// </summary>
public sealed class MergeFileInput
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the input.
    /// </summary>
    /// <param name="name">The name shown in the list and, unless a bookmark title is set, as the file's bookmark.</param>
    /// <param name="file">The loaded PDF.</param>
    public MergeFileInput(string name, PdfFile file)
    {
        Name = name;
        File = file;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the file's bookmark title, or null to use <see cref="Name"/>.
    /// </summary>
    public string? BookmarkTitle { get; set; }

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
    /// Gets the name shown in the list and, unless a bookmark title is set, as the file's bookmark.
    /// </summary>
    public string Name { get; }

    #endregion
}
