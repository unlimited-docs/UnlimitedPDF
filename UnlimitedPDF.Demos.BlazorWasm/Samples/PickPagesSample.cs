using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Pick pages demo: the chosen pages of one PDF, in the order written, such as "3, 1-2".
/// </summary>
public static class PickPagesSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document of picked pages.
    /// </summary>
    /// <param name="input">The file and the page list.</param>
    /// <returns>The document, ready to save.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    /// <exception cref="FormatException">The page list is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page is past the file's last page.</exception>
    public static PdfDocument Build(PickPagesInput input)
    {
        var file = input.File?.File ?? throw new InvalidOperationException("Choose a PDF.");
        var ranges = PdfPageRange.Parse(input.Pages);

        // Each range is one AddPages call: its pages are added in order, under one bookmark.
        var document = new PdfDocument();
        foreach (var range in ranges)
        {
            string title = range.FirstPage == range.LastPage ? $"Page {range.FirstPage}" : $"Pages {range}";
            document.AddPages(file, range.FirstPage, range.LastPage, bookmarkTitle: title);
        }
        return document;
    }

    #endregion

    #endregion
}
