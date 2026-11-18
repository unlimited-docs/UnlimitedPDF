using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Extract pages demo: the chosen pages of one PDF, in the order written, in one new PDF, such as
/// "4, 1-2". A page can be chosen more than once, and "9-7" gives pages 9, 8, 7.
/// </summary>
public static class ExtractPagesSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Writes the chosen pages of the file to a new PDF.
    /// </summary>
    /// <param name="input">The file and the pages to extract.</param>
    /// <returns>The new PDF's bytes.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    /// <exception cref="FormatException">The page list is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page is past the file's last page.</exception>
    public static byte[] Build(PageSelectionInput input)
    {
        var file = input.File?.File ?? throw new InvalidOperationException("Choose a PDF.");

        // The new PDF keeps the pages' links between them, their bookmarks, and the file's title and author,
        // and carries only the fonts and images those pages use.
        using var output = new MemoryStream();
        PdfOperations.Extract(file, PdfPageRange.Parse(input.Pages), output);
        return output.ToArray();
    }

    #endregion

    #endregion
}
