namespace UnlimitedPDF;

/// <summary>
/// A run of pages from <see cref="FirstPage"/> to <see cref="LastPage"/>, 1-based and inclusive, used to choose pages:
/// the parts of a split (WR-037), the pages to extract or remove, and the pages a document adds (WR-036, WR-040).
/// <see cref="Parse(string)"/> reads ranges from text such as <c>2, 5, 7-9</c>.
/// </summary>
public sealed class PdfPageRange
{
    #region Fields and Constants

    /// <summary>
    /// The most pages one <see cref="Parse(string)"/> call may describe, each range counting its span (WR-040).
    /// </summary>
    internal const long MaxParsedPages = 1_000_000;

    // The most characters of a bad item shown in a FormatException message.
    private const int MaxShownItemLength = 40;

    #endregion

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
    /// Reads page ranges from text such as <c>2, 5, 7-9</c>: page numbers and ranges separated by commas, with white
    /// space allowed around numbers, commas, and hyphens. A descending range such as <c>9-7</c> gives its pages in that
    /// order, as one range per page (9, 8, 7). The text may describe at most 1,000,000 pages in all, each range
    /// counting its span. Page numbers past a file's last page are reported by the call that uses the ranges.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The ranges, in the order the text gives them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException"><paramref name="text"/> is empty, has an empty item, a page number below 1
    /// or too large for an <see cref="int"/>, anything other than digits, commas, hyphens, and white space (such as
    /// <c>7-</c>, <c>last</c>, or <c>odd</c>), or describes more than 1,000,000 pages. The message names the item.</exception>
    [AddedVersion("1.1.0")]
    [Description("Reads page ranges from text such as 2, 5, 7-9.")]
    public static IReadOnlyList<PdfPageRange> Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("The text holds no page numbers.");

        // Every item is read and the page total checked before any range is created, so a short text cannot make
        // this call allocate more than the cap allows (A06).
        string[] items = text.Split(',');
        var spans = new List<(int First, int Last)>(items.Length);
        long pages = 0;
        for (int i = 0; i < items.Length; i++)
        {
            var span = ParseItem(items[i], i + 1);
            pages += Math.Abs((long)span.Last - span.First) + 1;
            if (pages > MaxParsedPages)
                throw new FormatException(string.Create(CultureInfo.InvariantCulture,
                    $"The text describes more than {MaxParsedPages:N0} pages; item {i + 1} (\"{Shown(items[i])}\") passes the limit."));
            spans.Add(span);
        }

        var ranges = new List<PdfPageRange>();
        foreach (var (first, last) in spans)
        {
            if (first <= last)
            {
                ranges.Add(new PdfPageRange(first, last));
            }
            else
            {
                for (int page = first; page >= last; page--) ranges.Add(new PdfPageRange(page, page));
            }
        }
        return ranges.AsReadOnly();
    }

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

    #region Private Methods

    // A FormatException naming the 1-based item and its text.
    private static FormatException ItemError(int number, string item, string problem) =>
        new(string.Create(CultureInfo.InvariantCulture, $"Item {number} (\"{Shown(item)}\") {problem}"));

    // One item: a page number, or two page numbers joined by a hyphen, with white space around either.
    private static (int First, int Last) ParseItem(string item, int number)
    {
        string trimmed = item.Trim();
        if (trimmed.Length == 0)
            throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"Item {number} is empty; page numbers are separated by single commas."));

        int hyphen = trimmed.IndexOf('-');
        if (hyphen < 0)
        {
            int page = ParsePage(trimmed, number, item);
            return (page, page);
        }

        string left = trimmed.Substring(0, hyphen).Trim();
        string right = trimmed.Substring(hyphen + 1).Trim();
        if (left.Length == 0 || right.Length == 0 || right.IndexOf('-') >= 0)
            throw ItemError(number, item, "is not a page number or a range such as 7-9.");
        return (ParsePage(left, number, item), ParsePage(right, number, item));
    }

    // A page number: ASCII digits only, from 1 to int.MaxValue.
    private static int ParsePage(string digits, int number, string item)
    {
        long value = 0;
        foreach (char c in digits)
        {
            if (c < '0' || c > '9') throw ItemError(number, item, "is not a page number or a range such as 7-9.");
            value = value * 10 + (c - '0');
            if (value > int.MaxValue) throw ItemError(number, item, "has a page number that is too large.");
        }
        if (value < 1) throw ItemError(number, item, "has page 0; page numbers start at 1.");
        return (int)value;
    }

    // The item as shown in a message: trimmed, and cut short when long.
    private static string Shown(string item)
    {
        string trimmed = item.Trim();
        return trimmed.Length <= MaxShownItemLength ? trimmed : trimmed.Substring(0, MaxShownItemLength) + "...";
    }

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
