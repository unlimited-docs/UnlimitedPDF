namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Checks a caller's page ranges against a loaded file before anything is built or written, with the same exceptions
/// for split, extract, remove, and adding a selection to a document (WR-037, WR-040).
/// </summary>
internal static class PageSelection
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Checks <paramref name="ranges"/> against <paramref name="file"/> and returns them as a list, in the order given.
    /// </summary>
    /// <param name="file">The file the ranges choose pages of.</param>
    /// <param name="ranges">The caller's ranges.</param>
    /// <param name="parameter">The caller's parameter name for <paramref name="ranges"/>.</param>
    /// <returns>The ranges, in the order given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ranges"/> or a range is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ranges"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range goes past the file's last page.</exception>
    public static List<PdfPageRange> Check(PdfFile file, IEnumerable<PdfPageRange>? ranges, string parameter)
    {
        if (ranges is null) throw new ArgumentNullException(parameter);

        var list = ranges.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one page range is needed.", parameter);
        for (int i = 0; i < list.Count; i++)
        {
            var range = list[i];
            if (range is null)
                throw new ArgumentNullException(parameter, string.Create(CultureInfo.InvariantCulture, $"Page range {i + 1} is null."));
            if (range.LastPage > file.PageCount)
                throw new ArgumentOutOfRangeException(parameter, range.ToString(),
                    string.Create(CultureInfo.InvariantCulture, $"Page range {i + 1} ({range}) goes past the file's last page, {file.PageCount}."));
        }
        return list;
    }

    /// <summary>
    /// Checks <paramref name="ranges"/> against <paramref name="file"/> and returns the 0-based page indexes they
    /// choose, in the order given, repeats kept.
    /// </summary>
    /// <param name="file">The file the ranges choose pages of.</param>
    /// <param name="ranges">The caller's ranges.</param>
    /// <param name="parameter">The caller's parameter name for <paramref name="ranges"/>.</param>
    /// <returns>The 0-based page indexes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ranges"/> or a range is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ranges"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range goes past the file's last page.</exception>
    public static List<int> Expand(PdfFile file, IEnumerable<PdfPageRange>? ranges, string parameter)
    {
        var indexes = new List<int>();
        foreach (var range in Check(file, ranges, parameter))
        {
            for (int page = range.FirstPage; page <= range.LastPage; page++) indexes.Add(page - 1);
        }
        return indexes;
    }

    #endregion

    #endregion
}
