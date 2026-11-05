namespace UnlimitedPDF;

/// <summary>
/// The saved page a header or footer callback builds content for (WR-031).
/// </summary>
public sealed class PdfPageInfo
{
    #region Constructors and Finalizers

    internal PdfPageInfo(int pageNumber, int pageCount)
    {
        PageNumber = pageNumber;
        PageCount = pageCount;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets whether the page is the first saved page.
    /// </summary>
    public bool IsFirst => PageNumber == 1;

    /// <summary>
    /// Gets whether the page is the last saved page.
    /// </summary>
    public bool IsLast => PageNumber == PageCount;

    /// <summary>
    /// Gets the number of saved pages, including pages the library added for tables and the body flow.
    /// </summary>
    public int PageCount { get; }

    /// <summary>
    /// Gets the 1-based page number in reading order.
    /// </summary>
    public int PageNumber { get; }

    #endregion
}
