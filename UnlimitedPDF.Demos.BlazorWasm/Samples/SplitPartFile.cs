using UnlimitedPDF;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One part written by a Split demo: the part's number, pages, and file name, and its PDF bytes.
/// </summary>
/// <param name="Part">The part as <see cref="PdfOperations"/> reports it.</param>
/// <param name="Bytes">The part's PDF.</param>
public sealed record SplitPartFile(PdfSplitPart Part, byte[] Bytes)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Wraps a file a demo made from scratch (one of many invoices, for example), so the parts page lists, previews,
    /// downloads, and zips it like a split part.
    /// </summary>
    /// <param name="number">The file's 1-based number in the run.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="bytes">The PDF.</param>
    /// <returns>The file, its pages counted from the PDF.</returns>
    public static SplitPartFile Generated(int number, string fileName, byte[] bytes) =>
        new(new PdfSplitPart(number, 1, PdfFile.FromBytes(bytes).PageCount, fileName), bytes);

    /// <summary>
    /// Packs the parts into one zip file, each under its part file name.
    /// </summary>
    /// <param name="parts">The parts to pack.</param>
    /// <returns>The zip file's bytes.</returns>
    public static byte[] Zip(IEnumerable<SplitPartFile> parts) =>
        ZipBuilder.Zip(parts.Select(part => (part.Part.FileName, part.Bytes)));

    #endregion

    #endregion
}
