namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// The rows of a table drawn on one page: rows <paramref name="FirstRow"/> up to, not including,
/// <paramref name="EndRow"/>, starting at <paramref name="Top"/>, below the repeated header rows when
/// <paramref name="RepeatsHeader"/> is set. A part with no rows draws nothing.
/// </summary>
/// <param name="Page">The page the part is drawn on.</param>
/// <param name="FirstRow">The index of the first row.</param>
/// <param name="EndRow">The index after the last row.</param>
/// <param name="Top">The y-coordinate of the part's top edge, in points.</param>
/// <param name="RepeatsHeader">Whether the header rows are drawn again above the rows.</param>
internal sealed record TablePart(PdfPageBuilder Page, int FirstRow, int EndRow, double Top, bool RepeatsHeader);
