using System.Globalization;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// The pages picked in a page grid, kept in step with the page list the visitor can type: clicking a page rewrites the
/// list in page order, and typing a list picks its pages, read by <see cref="PdfPageRange.Parse(string)"/>, which the
/// tool then uses as typed. The typed list is the keyboard way to pick pages (WCAG 2.1.1).
/// </summary>
public sealed class PageSelection
{
    #region Fields and Constants

    private readonly int _pageCount;
    private IReadOnlyList<int> _pages = [];

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PageSelection"/> class with no page picked.
    /// </summary>
    /// <param name="pageCount">The PDF's page count.</param>
    public PageSelection(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);
        _pageCount = pageCount;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Writes pages as a short list: runs of three or more become ranges, for example "1, 3-5, 8".
    /// </summary>
    /// <param name="pages">The pages, in the order to write them.</param>
    /// <returns>The list.</returns>
    public static string Compact(IEnumerable<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var list = pages.ToList();
        var parts = new List<string>();
        int i = 0;
        while (i < list.Count)
        {
            int j = i;
            while (j + 1 < list.Count && list[j + 1] == list[j] + 1) j++;
            parts.Add(j - i >= 2
                ? string.Create(CultureInfo.InvariantCulture, $"{list[i]}-{list[j]}")
                : string.Join(", ", list.Skip(i).Take(j - i + 1).Select(p => p.ToString(CultureInfo.InvariantCulture))));
            i = j + 1;
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Picks no page.
    /// </summary>
    public void Clear() => SetPages([]);

    /// <summary>
    /// Returns whether a page is picked.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <returns>True when the page is picked.</returns>
    public bool Contains(int page) => _pages.Contains(page);

    /// <summary>
    /// Picks the even pages.
    /// </summary>
    public void SelectEven() => SetPages(Enumerable.Range(1, _pageCount).Where(p => p % 2 == 0));

    /// <summary>
    /// Picks every page.
    /// </summary>
    public void SelectAll() => SetPages(Enumerable.Range(1, _pageCount));

    /// <summary>
    /// Picks the odd pages.
    /// </summary>
    public void SelectOdd() => SetPages(Enumerable.Range(1, _pageCount).Where(p => p % 2 == 1));

    /// <summary>
    /// Picks the pages a typed list names; a list that can't be read keeps the earlier pages and sets
    /// <see cref="Error"/>.
    /// </summary>
    /// <param name="text">The list, for example "1, 3-5".</param>
    public void SetText(string? text)
    {
        Text = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Text))
        {
            _pages = [];
            Error = null;
            return;
        }

        try
        {
            var ranges = PdfPageRange.Parse(Text);
            var past = ranges.Select(r => Math.Max(r.FirstPage, r.LastPage)).FirstOrDefault(p => p > _pageCount);
            if (past > 0)
            {
                Error = string.Create(CultureInfo.InvariantCulture, $"Page {past} is past the last page ({_pageCount}).");
                return;
            }

            _pages = ranges.SelectMany(Expand).ToList();
            Error = null;
        }
        catch (FormatException exception)
        {
            Error = exception.Message;
        }
    }

    /// <summary>
    /// Picks a page, or unpicks it when it is picked; the list is rewritten in page order.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    public void Toggle(int page)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, _pageCount);
        var set = new SortedSet<int>(_pages);
        if (!set.Remove(page)) set.Add(page);
        SetPages(set);
    }

    #endregion

    #region Private Methods

    private static IEnumerable<int> Expand(PdfPageRange range) =>
        range.FirstPage <= range.LastPage
            ? Enumerable.Range(range.FirstPage, range.LastPage - range.FirstPage + 1)
            : Enumerable.Range(range.LastPage, range.FirstPage - range.LastPage + 1).Reverse();

    private void SetPages(IEnumerable<int> pages)
    {
        _pages = pages.Distinct().Order().ToList();
        Text = Compact(_pages);
        Error = null;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets how many pages are picked, a repeated page counted each time.
    /// </summary>
    public int Count => _pages.Count;

    /// <summary>
    /// Gets why the typed list can't be used, or null.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets whether the list names at least one page and can be used.
    /// </summary>
    public bool IsUsable => Error is null && _pages.Count > 0;

    /// <summary>
    /// Gets the page count.
    /// </summary>
    public int PageCount => _pageCount;

    /// <summary>
    /// Gets the pages picked, in the list's order.
    /// </summary>
    public IReadOnlyList<int> Pages => _pages;

    /// <summary>
    /// Gets the page list, as typed or as written for the pages clicked.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    #endregion
}
