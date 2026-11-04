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

            // AddText writes its text into the PDF as given, so escape \ ( and ) first.
            page.AddText(PdfText.Escape(input.Heading), input.X, input.Y, input.FontSize);
            page.AddText(PdfText.Escape(input.Body), input.X, input.Y - input.FontSize - 14, 12);
            page.AddText($"Page {pageNumber} of {pageCount}", input.X, 40, 9);
        }

        return document;
    }

    #endregion

    #endregion
}
