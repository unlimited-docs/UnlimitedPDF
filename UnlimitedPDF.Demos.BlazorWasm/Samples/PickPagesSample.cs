using System.Globalization;
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
    /// <exception cref="InvalidOperationException">No file is chosen or the page list is not valid.</exception>
    public static PdfDocument Build(PickPagesInput input)
    {
        var file = input.File?.File ?? throw new InvalidOperationException("Choose a PDF.");
        var ranges = ParsePages(input.Pages, file.PageCount);

        // Each range is one AddPages call: its pages are added in order, under one bookmark.
        var document = new PdfDocument();
        foreach (var (first, last) in ranges)
        {
            string title = first == last ? $"Page {first}" : $"Pages {first}-{last}";
            document.AddPages(file, first, last, bookmarkTitle: title);
        }
        return document;
    }

    /// <summary>
    /// Reads a page list such as "3, 1-2" into 1-based, inclusive ranges, in the order written. Spaces are ignored.
    /// </summary>
    /// <param name="text">The page list.</param>
    /// <param name="pageCount">The number of pages in the file.</param>
    /// <returns>The ranges.</returns>
    /// <exception cref="InvalidOperationException">The list is empty, a part is not a page or range, a range runs
    /// backwards, or a page is past the last page.</exception>
    public static List<(int First, int Last)> ParsePages(string text, int pageCount)
    {
        var parts = (text ?? string.Empty).Replace(" ", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new InvalidOperationException("Enter the pages to pick, for example 3, 1-2.");

        var ranges = new List<(int First, int Last)>();
        foreach (var part in parts)
        {
            var ends = part.Split('-');
            if (ends.Length > 2 || !TryPage(ends[0], out int first) || !TryPage(ends[^1], out int last))
                throw new InvalidOperationException($"'{part}' is not a page or a range such as 1-2.");
            if (last < first) throw new InvalidOperationException($"'{part}' runs backwards; write {last}-{first}.");
            if (last > pageCount) throw new InvalidOperationException($"Page {last} is past the last page ({pageCount}).");
            ranges.Add((first, last));
        }
        return ranges;
    }

    #endregion

    #region Private Methods

    private static bool TryPage(string text, out int page) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1;

    #endregion

    #endregion
}
