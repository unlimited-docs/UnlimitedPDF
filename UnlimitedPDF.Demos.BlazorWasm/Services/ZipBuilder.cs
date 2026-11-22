using System.IO.Compression;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Packs files into one zip, as the Split scenarios and the Split tool offer "Download all".
/// </summary>
public static class ZipBuilder
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Packs the files into one zip, each under its file name, in the order given.
    /// </summary>
    /// <param name="files">The files: a name and the bytes for each.</param>
    /// <returns>The zip file's bytes.</returns>
    public static byte[] Zip(IEnumerable<(string Name, byte[] Bytes)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in files)
            {
                using var entry = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
                entry.Write(bytes);
            }
        }

        return output.ToArray();
    }

    #endregion

    #endregion
}
