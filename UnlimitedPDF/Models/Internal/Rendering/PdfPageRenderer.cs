using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Draws the pages of a loaded PDF as SVG for an in-page preview. A page is drawn completely or not at all: a page
/// with content this renderer does not understand, or one that reaches a work bound, comes back with the reason
/// instead of a drawing.
/// </summary>
internal static class PdfPageRenderer
{
    #region Fields and Constants

    private const int MaxReasonLength = 200;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Draws one page.
    /// </summary>
    /// <param name="file">The loaded file.</param>
    /// <param name="pageIndex">The 0-based page index.</param>
    /// <param name="idPrefix">A prefix for the drawing's element ids, unique among the drawings shown together.</param>
    /// <param name="limits">The work bounds; <see cref="PageRenderLimits.Default"/> when null.</param>
    /// <returns>The drawn page, or the reason it is not drawn.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page of the file.</exception>
    /// <exception cref="ArgumentException"><paramref name="idPrefix"/> holds a character other than ASCII letters,
    /// digits, hyphens, and underscores.</exception>
    public static PageRenderResult Render(PdfFile file, int pageIndex, string idPrefix, PageRenderLimits? limits = null)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (pageIndex < 0 || pageIndex >= file.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (idPrefix is null || !idPrefix.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_'))
            throw new ArgumentException("The id prefix may hold only ASCII letters, digits, hyphens, and underscores.", nameof(idPrefix));

        var page = file.Pages[pageIndex];
        try
        {
            return new PageInterpreter(file.Source, file.Pages.Select(p => p.ObjectNumber), limits ?? PageRenderLimits.Default, idPrefix)
                .Draw(page.Dictionary, page.Inherited, pageIndex + 1);
        }
        catch (PageRenderException exception)
        {
            return NotDrawn(pageIndex + 1, exception.Message);
        }
        catch (PdfReadException exception)
        {
            return NotDrawn(pageIndex + 1, $"The page can't be read: {exception.Message}");
        }
        catch (PdfFontException exception)
        {
            return NotDrawn(pageIndex + 1, $"A font on the page can't be read: {exception.Message}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Any other failure on hostile input still ends in a page that is not drawn, never a crash of the
            // preview (A10).
            return NotDrawn(pageIndex + 1, "The page can't be drawn.");
        }
    }

    /// <summary>
    /// Returns the size a page is shown at, its rotation applied, so a viewer can hold its place before drawing it.
    /// </summary>
    /// <param name="file">The loaded PDF.</param>
    /// <param name="pageIndex">The 0-based page index.</param>
    /// <returns>The width and height in points, or null when the page's boxes can't be read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page of the file.</exception>
    public static (double Width, double Height)? Size(PdfFile file, int pageIndex)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (pageIndex < 0 || pageIndex >= file.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex));

        try
        {
            // The size needs no page list: that is only for link destinations.
            return new PageInterpreter(file.Source, Array.Empty<int>(), PageRenderLimits.Default, "s")
                .ShownSize(file.Pages[pageIndex].Inherited);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    #endregion

    #region Private Methods

    // Reasons can quote the PDF (names, tokens), so they are kept short.
    private static PageRenderResult NotDrawn(int pageNumber, string reason) =>
        new(pageNumber, 0, 0, null, reason.Length <= MaxReasonLength ? reason : reason.Substring(0, MaxReasonLength) + "...",
            Array.Empty<string>(), PageRenderCounts.None);

    #endregion

    #endregion
}
