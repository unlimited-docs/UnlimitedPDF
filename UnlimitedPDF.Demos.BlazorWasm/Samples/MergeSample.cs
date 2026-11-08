using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Merge PDFs demo: an optional cover page, then the chosen pages of each file. Each file adds one bookmark.
/// To join whole files without a cover, <c>PdfOperations.Merge(output, files)</c> does it in one call.
/// </summary>
public static class MergeSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the merged document.
    /// </summary>
    /// <param name="input">The cover settings and the files to join.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(MergeInput input)
    {
        var document = new PdfDocument();

        if (input.AddCover)
        {
            // The cover gets the page count of the whole merged document; imported pages keep their own look.
            document.Footer.Height = 30;
            document.Footer.SetPageContent((footer, info) =>
                footer.AddText($"{info.PageCount} pages", 0, 10, 10, PdfTextAlignment.Center));
            var cover = document.AddPage();
            cover.AddText(input.CoverTitle, 72, 720, 26);
            int line = 680;
            foreach (var file in input.Files)
            {
                cover.AddText($"{file.Name}: {Range(file)}", 72, line, 12);
                line -= 20;
            }
        }

        foreach (var file in input.Files)
        {
            // Files are loaded once with PdfFile.FromStreamAsync; AddPages copies the chosen pages.
            document.AddPages(file.File, file.FirstPage, file.LastPage, bookmarkTitle: file.Name);
        }
        return document;
    }

    #endregion

    #region Private Methods

    private static string Range(MergeFileInput file)
    {
        int first = file.FirstPage ?? 1;
        int last = file.LastPage ?? file.File.PageCount;
        return first == 1 && last == file.File.PageCount ? $"all {last} pages" : $"pages {first} to {last}";
    }

    #endregion

    #endregion
}
