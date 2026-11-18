using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Remove pages demo: one PDF without the chosen pages, the rest in their original order, such as
/// "1-4". The order of the list and repeats in it do not matter.
/// </summary>
public static class RemovePagesSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Writes the file without the chosen pages to a new PDF.
    /// </summary>
    /// <param name="input">The file and the pages to remove.</param>
    /// <returns>The new PDF's bytes.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    /// <exception cref="FormatException">The page list is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page is past the file's last page.</exception>
    /// <exception cref="ArgumentException">The list removes every page.</exception>
    public static byte[] Build(PageSelectionInput input)
    {
        var file = input.File?.File ?? throw new InvalidOperationException("Choose a PDF.");

        // The loaded file is not changed: the pages left are written to a new PDF, with their bookmarks and the
        // file's title and author.
        using var output = new MemoryStream();
        PdfOperations.Remove(file, PdfPageRange.Parse(input.Pages), output);
        return output.ToArray();
    }

    #endregion

    #endregion
}
