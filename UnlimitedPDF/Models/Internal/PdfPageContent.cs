namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// A page's compiled content: the content stream bytes and the distinct images it draws, in the order of their
/// resource names (/Im1, /Im2, ...).
/// </summary>
/// <param name="Data">The content stream bytes.</param>
/// <param name="Images">The distinct images the content draws; image n is named /Im(n+1).</param>
internal sealed record PdfPageContent(byte[] Data, IReadOnlyList<PdfImageData> Images);
