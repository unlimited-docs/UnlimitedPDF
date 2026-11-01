namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// A cell's drawn items and their total height.
/// </summary>
/// <param name="Items">The items to draw, top to bottom; empty text is left out.</param>
/// <param name="BlockHeight">The total height of the items in points.</param>
internal sealed record CellLayout(IReadOnlyList<LaidOutItem> Items, double BlockHeight);
