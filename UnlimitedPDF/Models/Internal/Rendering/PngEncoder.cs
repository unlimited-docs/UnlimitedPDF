using System.IO.Compression;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Writes 8-bit gray, RGB, or RGBA pixels as a PNG file, so a PDF image can be shown in a browser.
/// </summary>
internal static class PngEncoder
{
    #region Fields and Constants

    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly uint[] CrcTable = BuildCrcTable();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Encodes rows of 8-bit samples.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="channels">1 for gray, 3 for RGB, 4 for RGBA.</param>
    /// <param name="pixels">The samples, row by row, top row first, with no padding.</param>
    /// <returns>The PNG file.</returns>
    public static byte[] Encode(int width, int height, int channels, byte[] pixels)
    {
        byte colorType = channels switch { 1 => 0, 3 => 2, 4 => 6, _ => throw new ArgumentOutOfRangeException(nameof(channels)) };
        int stride = width * channels;

        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // filter type None
                zlib.Write(pixels, y * stride, stride);
            }
        }

        using var png = new MemoryStream();
        png.Write(Signature, 0, Signature.Length);
        var header = new byte[13];
        WriteUInt32(header, 0, (uint)width);
        WriteUInt32(header, 4, (uint)height);
        header[8] = 8; // bit depth
        header[9] = colorType;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", raw.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    #endregion

    #region Private Methods

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteUInt32(length, 0, (uint)data.Length);
        output.Write(length, 0, 4);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes, 0, 4);
        output.Write(data, 0, data.Length);
        var crc = new byte[4];
        WriteUInt32(crc, 0, Crc(typeBytes, data));
        output.Write(crc, 0, 4);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    #endregion

    #endregion
}
