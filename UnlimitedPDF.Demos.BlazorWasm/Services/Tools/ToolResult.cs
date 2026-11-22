namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// One PDF a tool made.
/// </summary>
/// <param name="FileName">The file name the download gets.</param>
/// <param name="Bytes">The PDF, exactly as saved; the download hands over these bytes.</param>
/// <param name="PageCount">The PDF's page count.</param>
/// <param name="FirstPage">For a split part, the first page of the original it holds; otherwise 1.</param>
/// <param name="LastPage">For a split part, the last page of the original it holds; otherwise the page count.</param>
public sealed record ToolPdf(string FileName, byte[] Bytes, int PageCount, int FirstPage, int LastPage);

/// <summary>
/// What a tool run made, or why it failed.
/// </summary>
/// <param name="Files">The PDFs made: one, or one per part for Split. Empty when the run failed.</param>
/// <param name="Elapsed">How long the run took in the browser.</param>
/// <param name="Error">The message to show when the run failed, or null.</param>
/// <param name="LeftOut">For HTML to PDF, the pictures the HTML names that were not added, or web addresses not fetched.</param>
public sealed record ToolResult(IReadOnlyList<ToolPdf> Files, TimeSpan Elapsed, string? Error = null, IReadOnlyList<string>? LeftOut = null)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The message to show.</param>
    /// <returns>The result.</returns>
    public static ToolResult Failed(string error) => new([], TimeSpan.Zero, error);

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the first PDF made, the only one for every tool but Split.
    /// </summary>
    public ToolPdf? File => Files.Count > 0 ? Files[0] : null;

    /// <summary>
    /// Gets whether the run made its PDFs.
    /// </summary>
    public bool Succeeded => Error is null && Files.Count > 0;

    #endregion
}
