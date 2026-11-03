namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// Reads the orientation tag from an EXIF block. EXIF is optional metadata, so a damaged block is ignored (the image
/// keeps its stored orientation) rather than failing the load. Only the first image directory is read, so offsets
/// cannot loop.
/// </summary>
internal static class ExifReader
{
    #region Fields and Constants

    private const int EntrySize = 12;
    private const int OrientationTag = 0x0112;
    private const int ShortType = 3;
    private const int TiffHeaderSize = 8;

    /// <summary>
    /// The orientation that needs no transform.
    /// </summary>
    public const int Upright = 1;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the EXIF orientation (1 to 8) stored in a TIFF-structured EXIF block, or <see cref="Upright"/> when
    /// the block has no valid orientation.
    /// </summary>
    /// <param name="data">The buffer holding the block.</param>
    /// <param name="offset">The start of the TIFF header (after the "Exif\0\0" identifier).</param>
    /// <param name="length">The number of bytes available from <paramref name="offset"/>.</param>
    /// <returns>The orientation value.</returns>
    public static int ReadOrientation(byte[] data, int offset, int length)
    {
        if (length < TiffHeaderSize || offset < 0 || offset + length > data.Length) return Upright;

        bool bigEndian;
        if (data[offset] == 'M' && data[offset + 1] == 'M') bigEndian = true;
        else if (data[offset] == 'I' && data[offset + 1] == 'I') bigEndian = false;
        else return Upright;

        if (ReadUInt16(data, offset + 2, bigEndian) != 42) return Upright;

        // Offsets are relative to the TIFF header; every read below stays inside the block.
        long directory = ReadUInt32(data, offset + 4, bigEndian);
        if (directory < TiffHeaderSize || directory + 2 > length) return Upright;

        int entryCount = ReadUInt16(data, offset + (int)directory, bigEndian);
        long entriesEnd = directory + 2 + (long)entryCount * EntrySize;
        if (entriesEnd > length) return Upright;

        for (int i = 0; i < entryCount; i++)
        {
            int entry = offset + (int)directory + 2 + i * EntrySize;
            if (ReadUInt16(data, entry, bigEndian) != OrientationTag) continue;

            if (ReadUInt16(data, entry + 2, bigEndian) != ShortType || ReadUInt32(data, entry + 4, bigEndian) != 1)
                return Upright;

            // A single SHORT value sits in the first two bytes of the value field.
            int value = ReadUInt16(data, entry + 8, bigEndian);
            return value is >= 1 and <= 8 ? value : Upright;
        }

        return Upright;
    }

    /// <summary>
    /// Returns whether <paramref name="orientation"/> turns the image by 90 degrees, which swaps its displayed width
    /// and height (values 5 to 8).
    /// </summary>
    /// <param name="orientation">An EXIF orientation value.</param>
    /// <returns>True for values 5 to 8.</returns>
    public static bool SwapsDimensions(int orientation) => orientation is >= 5 and <= 8;

    #endregion

    #region Private Methods

    private static int ReadUInt16(byte[] data, int offset, bool bigEndian) => bigEndian
        ? (data[offset] << 8) | data[offset + 1]
        : data[offset] | (data[offset + 1] << 8);

    private static long ReadUInt32(byte[] data, int offset, bool bigEndian) => bigEndian
        ? ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) | ((long)data[offset + 2] << 8) | data[offset + 3]
        : data[offset] | ((long)data[offset + 1] << 8) | ((long)data[offset + 2] << 16) | ((long)data[offset + 3] << 24);

    #endregion

    #endregion
}
