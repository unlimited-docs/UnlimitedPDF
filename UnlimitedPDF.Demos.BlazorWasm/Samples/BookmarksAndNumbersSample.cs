using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Bookmarks and page numbers demo: a built contents page and a built closing page around the files, each
/// file under its own bookmark. Page numbers count every page, but only built pages show the footer.
/// </summary>
public static class BookmarksAndNumbersSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The files, their bookmark titles, and the footer text.</param>
    /// <returns>The document, ready to save.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    public static PdfDocument Build(BookmarksAndNumbersInput input)
    {
        if (input.Files.Count == 0) throw new InvalidOperationException("Add at least one PDF.");

        var document = new PdfDocument();
        document.Footer.Height = 30;
        document.Footer.AddText(input.FooterText, 0, 10, 10, PdfTextAlignment.Center);

        var contents = document.AddPage();
        contents.AddText("Contents", 72, 720, 24);
        int line = 680;
        foreach (var file in input.Files)
        {
            contents.AddText(Title(file), 72, line, 12);
            line -= 20;
        }

        // Each file gets one top-level bookmark with the title given; its own bookmarks are nested under it.
        foreach (var file in input.Files)
            document.AddPages(file.File, bookmarkTitle: Title(file));

        // Pages added after the imported ones are numbered after them.
        document.AddPage().AddText("End of document", 72, 720, 18);
        return document;
    }

    #endregion

    #region Private Methods

    private static string Title(MergeFileInput file) => string.IsNullOrWhiteSpace(file.BookmarkTitle) ? file.Name : file.BookmarkTitle;

    #endregion

    #endregion
}
