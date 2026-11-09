using System.IO.Compression;
using UnlimitedPDF;

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
    /// Packs the parts into one zip file, each under its part file name.
    /// </summary>
    /// <param name="parts">The parts to pack.</param>
    /// <returns>The zip file's bytes.</returns>
    public static byte[] Zip(IEnumerable<SplitPartFile> parts)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var part in parts)
            {
                using var entry = zip.CreateEntry(part.Part.FileName, CompressionLevel.Fastest).Open();
                entry.Write(part.Bytes);
            }
        }
        return output.ToArray();
    }

    #endregion

    #endregion
}
