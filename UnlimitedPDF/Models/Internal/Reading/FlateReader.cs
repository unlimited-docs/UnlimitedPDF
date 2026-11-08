using System.IO.Compression;
using System.Runtime.InteropServices;

namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Decompresses <c>FlateDecode</c> data with a ceiling on the output, so a small hostile stream cannot inflate into
/// gigabytes (A06). Decompression stops as soon as the ceiling is passed.
/// </summary>
internal static class FlateReader
{
    #region Fields and Constants

    private const int BufferSize = 81920;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Decompresses <paramref name="data"/>, which may have a zlib header or be raw deflate data.
    /// </summary>
    /// <param name="data">The compressed bytes.</param>
    /// <param name="maxOutput">The most bytes the result may have.</param>
    /// <returns>The decompressed bytes. Data cut short after some output keeps what decoded.</returns>
    /// <exception cref="PdfReadException">The output passes <paramref name="maxOutput"/>, or nothing decodes.</exception>
    public static byte[] Decode(ReadOnlyMemory<byte> data, long maxOutput)
    {
        var segment = MemoryMarshal.TryGetArray(data, out var array) ? array : new ArraySegment<byte>(data.ToArray());
        bool zlibHeader = segment.Count >= 2 && (segment.Array![segment.Offset] & 0x0F) == 8
            && ((segment.Array[segment.Offset] << 8) | segment.Array[segment.Offset + 1]) % 31 == 0;

        var result = Inflate(segment, zlibHeader, maxOutput, out bool failed);
        if (failed && result.Length == 0 && zlibHeader)
            result = Inflate(new ArraySegment<byte>(segment.Array!, segment.Offset + 2, segment.Count - 2), false, maxOutput, out failed);
        if (failed && result.Length == 0)
            throw PdfReadException.Damaged("A compressed stream cannot be decompressed.");
        return result;
    }

    #endregion

    #region Private Methods

    private static byte[] Inflate(ArraySegment<byte> input, bool zlib, long maxOutput, out bool failed)
    {
        failed = false;
        using var source = new MemoryStream(input.Array!, input.Offset, input.Count, writable: false);
        using Stream inflater = zlib
            ? new ZLibStream(source, CompressionMode.Decompress)
            : new DeflateStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[BufferSize];
        try
        {
            int read;
            while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > maxOutput)
                    throw new PdfReadException(PdfReadError.TooLarge, $"A stream decompresses to more than {maxOutput.ToString("N0", CultureInfo.InvariantCulture)} bytes.");
                output.Write(buffer, 0, read);
            }
        }
        catch (InvalidDataException)
        {
            failed = true;
        }
        return output.ToArray();
    }

    #endregion

    #endregion
}
