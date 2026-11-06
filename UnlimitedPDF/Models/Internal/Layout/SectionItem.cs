using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// One item of header or footer content, placed by offsets from the band's top-left corner with y increasing downward
/// (WR-031).
/// </summary>
internal abstract record SectionItem;

/// <summary>
/// A line of text whose top edge is <paramref name="Y"/> below the band's top.
/// </summary>
/// <param name="Text">The text; may contain <c>{page}</c> and <c>{pages}</c>.</param>
/// <param name="X">The offset used by the alignment.</param>
/// <param name="Y">The offset of the line's top edge.</param>
/// <param name="FontSize">The font size.</param>
/// <param name="Alignment">The alignment within the band width.</param>
internal sealed record SectionText(string Text, double X, double Y, double FontSize, PdfTextAlignment Alignment) : SectionItem;

/// <summary>
/// An image whose top-left corner is at (<paramref name="X"/>, <paramref name="Y"/>).
/// </summary>
/// <param name="Image">The image.</param>
/// <param name="X">The offset from the band's left edge.</param>
/// <param name="Y">The offset of the image's top edge.</param>
/// <param name="Width">The width, or null.</param>
/// <param name="Height">The height, or null.</param>
/// <param name="Options">Placement options, or null.</param>
internal sealed record SectionImage(PdfImage Image, double X, double Y, double? Width, double? Height, PdfImagePlacementOptions? Options) : SectionItem;

/// <summary>
/// A table whose <see cref="PdfTable.X"/> and <see cref="PdfTable.Y"/> are offsets from the band's top-left corner.
/// It never paginates.
/// </summary>
/// <param name="Table">The table.</param>
internal sealed record SectionTable(PdfTable Table) : SectionItem;

/// <summary>
/// A filled rectangle whose top-left corner is at (<paramref name="X"/>, <paramref name="Y"/>).
/// </summary>
/// <param name="X">The offset from the band's left edge.</param>
/// <param name="Y">The offset of the rectangle's top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Color">The fill color.</param>
internal sealed record SectionRectangle(double X, double Y, double Width, double Height, PdfColor Color) : SectionItem;
