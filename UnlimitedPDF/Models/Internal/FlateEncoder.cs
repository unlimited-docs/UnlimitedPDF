using System.IO.Compression;

namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Compresses the streams the library writes itself as zlib data for <c>/Filter /FlateDecode</c> (WR-042), with the
/// platform's own <see cref="ZLibStream"/> at <see cref="CompressionLevel.Optimal"/>. It keeps no state between calls,
/// so threads may share it, and the same input on the same runtime always gives the same bytes.
/// </summary>
internal static class FlateEncoder
{
    #region Fields and Constants

    /// <summary>
    /// The PDF filter name of the data <see cref="Compress"/> returns.
    /// </summary>
    public const string FilterName = "/FlateDecode";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns <paramref name="data"/> compressed as zlib data. Empty data gives a valid stream that unpacks to nothing.
    /// </summary>
    /// <param name="data">The stream's unpacked bytes.</param>
    /// <returns>The compressed bytes.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    #endregion

    #endregion
}
