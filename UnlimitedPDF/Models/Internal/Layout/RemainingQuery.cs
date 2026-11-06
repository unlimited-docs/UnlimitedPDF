namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// One <c>Body.RemainingHeight</c> read, kept so the save can check that the value is still exact (WR-033): how much flow and
/// how many caller pages existed, the page size a first page would have had, the answer, and the layout settings then.
/// </summary>
/// <param name="FlowCount">The number of flow items when the value was read.</param>
/// <param name="CallerPages">The number of pages added with <c>AddPage</c> when the value was read.</param>
/// <param name="PageSize">The document page size when the value was read.</param>
/// <param name="PageNumber">The page the next item would have landed on.</param>
/// <param name="Value">The height returned, in points.</param>
/// <param name="Settings">The layout settings fingerprint when the value was read.</param>
internal sealed record RemainingQuery(int FlowCount, int CallerPages, PdfPageSize PageSize, int PageNumber, double Value, string Settings);
