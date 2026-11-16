using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Draws the pages of a loaded PDF as SVG for an in-page preview. A page is drawn completely or not at all: a page
/// with content this renderer does not understand, or one that reaches a work bound, comes back with the reason
/// instead of a drawing.
/// </summary>
internal static class PdfPageRenderer
{
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
    public static PageRenderResult Render(PdfFile file, int pageIndex, string idPrefix, PageRenderLimits? limits = null)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (pageIndex < 0 || pageIndex >= file.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex));

        var page = file.Pages[pageIndex];
        try
        {
            return new PageInterpreter(file.Source.Objects, limits ?? PageRenderLimits.Default, idPrefix ?? string.Empty)
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
    }

    #endregion

    #region Private Methods

    private static PageRenderResult NotDrawn(int pageNumber, string reason) =>
        new(pageNumber, 0, 0, null, reason, Array.Empty<string>(), PageRenderCounts.None);

    #endregion

    #endregion
}
