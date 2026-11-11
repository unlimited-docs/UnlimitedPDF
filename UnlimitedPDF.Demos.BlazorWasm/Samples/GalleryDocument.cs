namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One document of the home page gallery: what its tile and its quick look show, and how its default PDF is built.
/// </summary>
/// <param name="Demo">The document's playground demo.</param>
/// <param name="Summary">One line on what the document demonstrates, shown on its tile.</param>
/// <param name="PageCount">The number of pages its demo generates with the default input.</param>
/// <param name="PageSize">The page size of that PDF.</param>
/// <param name="Picture">The base name of its pictures in <c>images/gallery/</c>, for example "invoice".</param>
/// <param name="Images">The built-in image files its default input uses.</param>
/// <param name="Build">Builds the document from a new default input with the given images.</param>
public sealed record GalleryDocument(
    SampleDemo Demo,
    string Summary,
    int PageCount,
    PdfPageSize PageSize,
    string Picture,
    IReadOnlyList<string> Images,
    Func<SampleImages, PdfDocument> Build);
