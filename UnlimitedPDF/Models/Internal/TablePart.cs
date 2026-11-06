namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// The rows of a table drawn on one page: rows <paramref name="FirstRow"/> up to, not including,
/// <paramref name="EndRow"/>, starting at <paramref name="Top"/>, below the repeated header rows when
/// <paramref name="RepeatsHeader"/> is set. A part with no rows draws nothing.
/// </summary>
/// <param name="PageIndex">The index of the page the part is drawn on, among the saved pages in reading order.</param>
/// <param name="FirstRow">The index of the first row.</param>
/// <param name="EndRow">The index after the last row.</param>
/// <param name="Top">The y-coordinate of the part's top edge, in points.</param>
/// <param name="Bottom">The y-coordinate where the part's last row ends, in points (the top when it draws nothing).</param>
/// <param name="X">The x-coordinate of the part's left edge, in points.</param>
/// <param name="RepeatsHeader">Whether the header rows are drawn again above the rows.</param>
internal sealed record TablePart(int PageIndex, int FirstRow, int EndRow, double Top, double Bottom, double X, bool RepeatsHeader)
{
    /// <summary>
    /// Gets whether the part draws any row.
    /// </summary>
    public bool HasRows => EndRow > FirstRow;
}
