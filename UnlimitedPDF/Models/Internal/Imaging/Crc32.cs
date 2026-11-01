namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// The CRC-32 checksum PNG uses for every chunk (polynomial 0xEDB88320).
/// </summary>
internal static class Crc32
{
    #region Fields and Constants

    private static readonly uint[] Table = CreateTable();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Computes the checksum of <paramref name="count"/> bytes of <paramref name="data"/> from <paramref name="offset"/>.
    /// </summary>
    /// <param name="data">The buffer.</param>
    /// <param name="offset">The first byte to include.</param>
    /// <param name="count">The number of bytes to include.</param>
    /// <returns>The CRC-32 value.</returns>
    public static uint Compute(byte[] data, int offset, int count)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + count; i++)
        {
            crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }

    #endregion

    #region Private Methods

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    #endregion

    #endregion
}
