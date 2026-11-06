namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// One content item with its drawn size.
/// </summary>
/// <param name="Item">The content item.</param>
/// <param name="Width">The drawn width in points (approximate for text).</param>
/// <param name="Height">The drawn height in points (the line height for text).</param>
internal sealed record LaidOutItem(PdfCellContent Item, double Width, double Height);
