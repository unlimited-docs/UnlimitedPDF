using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Join files demo: whole PDFs joined in one call, in the order given. Each file gets a top-level bookmark.
/// </summary>
public static class JoinFilesSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the joined document for the preview. Its bytes equal <see cref="Merge"/>'s, because
    /// <c>PdfOperations.Merge</c> adds each file to a new document the same way.
    /// </summary>
    /// <param name="input">The files to join.</param>
    /// <returns>The document, ready to save.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    public static PdfDocument Build(JoinFilesInput input)
    {
        var document = new PdfDocument();
        foreach (var file in Files(input)) document.AddPages(file);
        return document;
    }

    /// <summary>
    /// Joins the files in one call.
    /// </summary>
    /// <param name="input">The files to join.</param>
    /// <returns>The joined PDF.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    public static byte[] Merge(JoinFilesInput input)
    {
        using var output = new MemoryStream();
        PdfOperations.Merge(output, Files(input));
        return output.ToArray();
    }

    #endregion

    #region Private Methods

    private static PdfFile[] Files(JoinFilesInput input) => input.Files.Count > 0
        ? input.Files.Select(file => file.File).ToArray()
        : throw new InvalidOperationException("Add at least one PDF.");

    #endregion

    #endregion
}
