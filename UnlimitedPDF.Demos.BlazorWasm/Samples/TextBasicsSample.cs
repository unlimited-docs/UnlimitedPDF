using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Text and pages: a page size, text at a chosen position and size, and several pages.
/// </summary>
public static class TextBasicsSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The demo options.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(TextBasicsInput input)
    {
        // Coordinates are in points (1/72 inch), measured from the bottom-left corner of the page.
        var document = new PdfDocument(input.PageSize);
        var pageCount = Math.Max(1, input.PageCount);

        for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var page = document.AddPage();

            // Text is written as typed: the library escapes \ ( and ) itself.
            page.AddText(input.Heading, input.X, input.Y, input.FontSize);
            page.AddText(input.Body, input.X, input.Y - input.FontSize - 14, 12);
            page.AddText($"Page {pageNumber} of {pageCount}", input.X, 40, 9);
        }

        return document;
    }

    #endregion

    #endregion
}
