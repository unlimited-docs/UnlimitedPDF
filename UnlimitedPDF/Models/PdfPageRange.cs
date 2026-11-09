namespace UnlimitedPDF;

/// <summary>
/// A run of pages from <see cref="FirstPage"/> to <see cref="LastPage"/>, 1-based and inclusive, used to choose the
/// pages of one split part (WR-037).
/// </summary>
public sealed class PdfPageRange
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageRange"/> class.
    /// </summary>
    /// <param name="firstPage">The 1-based number of the first page.</param>
    /// <param name="lastPage">The 1-based number of the last page; equal to <paramref name="firstPage"/> for one
    /// page.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstPage"/> is less than 1, or
    /// <paramref name="lastPage"/> comes before <paramref name="firstPage"/>.</exception>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfPageRange class.")]
    public PdfPageRange(int firstPage, int lastPage)
    {
        if (firstPage < 1)
            throw new ArgumentOutOfRangeException(nameof(firstPage), firstPage, "The first page must be 1 or more.");
        if (lastPage < firstPage)
            throw new ArgumentOutOfRangeException(nameof(lastPage), lastPage, "The last page must not come before the first page.");

        FirstPage = firstPage;
        LastPage = lastPage;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the range as text, for example <c>1-3</c>, or <c>5</c> for one page.
    /// </summary>
    /// <returns>The range as text.</returns>
    [AddedVersion("1.0.0")]
    [Description("Returns the range as text, for example 1-3, or 5 for one page.")]
    public override string ToString() =>
        FirstPage == LastPage
            ? FirstPage.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{FirstPage}-{LastPage}");

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the 1-based number of the first page.
    /// </summary>
    public int FirstPage { get; }

    /// <summary>
    /// Gets the 1-based number of the last page.
    /// </summary>
    public int LastPage { get; }

    #endregion
}
