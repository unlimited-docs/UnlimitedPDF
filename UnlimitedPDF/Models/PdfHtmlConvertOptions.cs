namespace UnlimitedPDF;

/// <summary>
/// Options for <see cref="PdfOperations.ConvertHtml(PdfHtml, string, PdfHtmlConvertOptions?)"/> (WR-038): the page
/// size, the four page margins, and whether headings become bookmarks.
/// </summary>
public sealed class PdfHtmlConvertOptions
{
    #region Fields and Constants

    // The default margin: half an inch.
    private const double DefaultMargin = 36;

    private double _marginBottom = DefaultMargin;
    private double _marginLeft = DefaultMargin;
    private double _marginRight = DefaultMargin;
    private double _marginTop = DefaultMargin;

    #endregion

    #region Methods

    #region Private Methods

    private static double Margin(double value, string name) =>
        double.IsNaN(value) || double.IsInfinity(value) || value < 0
            ? throw new ArgumentOutOfRangeException(name, value, "The margin must be a finite number of points, zero or greater.")
            : value;

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the fonts that draw characters the HTML's fonts lack, tried in order for each character (WR-039); the
    /// converted document's <see cref="PdfDocument.FontFallback"/>. Null entries are rejected.
    /// </summary>
    public IList<PdfFont> FontFallback { get; } = new FontList();

    /// <summary>
    /// Gets or sets whether h1 to h6 headings become bookmarks at their heading levels. The default is false.
    /// </summary>
    public bool HeadingBookmarks { get; set; }

    /// <summary>
    /// Gets or sets the bottom page margin in points. The default is 36 (half an inch).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double MarginBottom
    {
        get => _marginBottom;
        set => _marginBottom = Margin(value, nameof(MarginBottom));
    }

    /// <summary>
    /// Gets or sets the left page margin in points. The default is 36 (half an inch).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double MarginLeft
    {
        get => _marginLeft;
        set => _marginLeft = Margin(value, nameof(MarginLeft));
    }

    /// <summary>
    /// Gets or sets the right page margin in points. The default is 36 (half an inch).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double MarginRight
    {
        get => _marginRight;
        set => _marginRight = Margin(value, nameof(MarginRight));
    }

    /// <summary>
    /// Gets or sets the top page margin in points. The default is 36 (half an inch).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double MarginTop
    {
        get => _marginTop;
        set => _marginTop = Margin(value, nameof(MarginTop));
    }

    /// <summary>
    /// Gets or sets the page size. The default is A4.
    /// </summary>
    public PdfPageSize PageSize { get; set; } = PdfPageSize.A4;

    #endregion
}
