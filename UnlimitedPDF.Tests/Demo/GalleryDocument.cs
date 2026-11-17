namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// A document demo whose card picture is tracked against its sources: when the sample or its images change, the
/// picture is captured again (see <c>UnlimitedPDF.Tests/Fixtures/document-gallery.json</c>).
/// </summary>
/// <param name="Demo">The catalogue entry.</param>
/// <param name="Picture">The base name of its picture in <c>images/gallery/</c>, for example "invoice".</param>
/// <param name="Images">The images its sample draws, by their file names in <c>wwwroot/images</c>.</param>
public sealed record GalleryDocument(SampleDemo Demo, string Picture, IReadOnlyList<string> Images);
