namespace UnlimitedPDF;

/// <summary>
/// Where a table ends after it is laid out across pages: the page that holds its last row, the y-coordinate where that
/// row ends, and the continuation pages the table was drawn onto. Returned by <see cref="PdfPageBuilder.GetTableEnd"/>.
/// </summary>
public sealed class PdfTableEnd
{
    #region Constructors and Finalizers

    internal PdfTableEnd(PdfPageBuilder page, double y, IReadOnlyList<PdfPageBuilder> continuationPages)
    {
        Page = page;
        Y = y;
        ContinuationPages = continuationPages;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the continuation pages the table was drawn onto, in page order. Empty when the table fits on its own page.
    /// Content can be added to each, for example a footer or a page number.
    /// </summary>
    public IReadOnlyList<PdfPageBuilder> ContinuationPages { get; }

    /// <summary>
    /// Gets the page that holds the table's last row: the table's own page when it does not continue, otherwise its
    /// last continuation page. Content added to it is drawn on that page.
    /// </summary>
    public PdfPageBuilder Page { get; }

    /// <summary>
    /// Gets the y-coordinate, in points from the page's bottom edge, where the table's last row ends on
    /// <see cref="Page"/>. For a table without rows it is the table's own top edge.
    /// </summary>
    public double Y { get; }

    #endregion
}
