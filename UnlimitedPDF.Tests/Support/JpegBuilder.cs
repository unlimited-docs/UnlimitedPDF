namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Builds synthetic JPEG files with a valid marker structure and placeholder scan data. The library never decodes
/// JPEG data, so these exercise every loading rule; real encoded fixtures are only needed for viewing (FA-14).
/// </summary>
internal sealed class JpegBuilder
{
    #region Fields and Constants

    // Placeholder entropy-coded data with a stuffed zero and a restart marker, which belong to the scan.
    private static readonly byte[] ScanData = { 0x12, 0x34, 0xFF, 0x00, 0x56, 0xFF, 0xD0, 0x78 };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the JPEG file bytes.
    /// </summary>
    public byte[] Build()
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (Jfif is { } jfif)
        {
            var payload = Encoding.ASCII.GetBytes("JFIF\0")
                .Concat(new byte[] { 1, 2, jfif.Units, (byte)(jfif.X >> 8), (byte)jfif.X, (byte)(jfif.Y >> 8), (byte)jfif.Y, 0, 0 })
                .ToArray();
            bytes.AddRange(Segment(0xE0, payload));
        }

        foreach (var segment in ExtraSegments)
            bytes.AddRange(segment);

        if (Adobe)
            bytes.AddRange(Segment(0xEE, Encoding.ASCII.GetBytes("Adobe").Concat(new byte[] { 0, 100, 0, 0, 0, 0, AdobeTransform }).ToArray()));

        // Quantization table placeholder: one 8-bit table of 64 values.
        bytes.AddRange(Segment(0xDB, new byte[65]));

        if (IncludeFrame)
        {
            var frame = new List<byte> { Precision, (byte)(Height >> 8), (byte)Height, (byte)(Width >> 8), (byte)Width, (byte)Components };
            for (int c = 0; c < Components; c++)
                frame.AddRange(new byte[] { (byte)(c + 1), 0x11, 0 });
            bytes.AddRange(Segment(FrameMarker, frame.ToArray()));
        }

        for (int scan = 0; scan < ScanCount; scan++)
        {
            var header = new List<byte> { (byte)Components };
            for (int c = 0; c < Components; c++)
                header.AddRange(new byte[] { (byte)(c + 1), 0 });
            header.AddRange(new byte[] { 0, 63, 0 });
            bytes.AddRange(Segment(0xDA, header.ToArray()));
            bytes.AddRange(ScanData);
        }

        if (IncludeEndOfImage)
            bytes.AddRange(new byte[] { 0xFF, 0xD9 });

        return bytes.ToArray();
    }

    /// <summary>
    /// Returns an APP1 EXIF segment whose first image directory holds one orientation entry.
    /// </summary>
    public static byte[] ExifSegment(ushort orientation, bool bigEndian = false, ushort tagType = 3, uint directoryOffset = 8)
    {
        return Segment(0xE1, Encoding.ASCII.GetBytes("Exif\0\0").Concat(TiffBlock(orientation, bigEndian, tagType, directoryOffset)).ToArray());
    }

    /// <summary>
    /// Returns a segment: marker, big-endian length (payload + 2), payload.
    /// </summary>
    public static byte[] Segment(byte marker, byte[] payload)
    {
        var length = payload.Length + 2;
        return new byte[] { 0xFF, marker, (byte)(length >> 8), (byte)length }.Concat(payload).ToArray();
    }

    /// <summary>
    /// Returns a TIFF-structured block: header, then one directory with an orientation entry and a next-directory
    /// offset that points back at itself (a loop the reader must not follow).
    /// </summary>
    public static byte[] TiffBlock(ushort orientation, bool bigEndian = false, ushort tagType = 3, uint directoryOffset = 8)
    {
        var block = new List<byte>();
        block.AddRange(bigEndian ? new byte[] { (byte)'M', (byte)'M' } : new byte[] { (byte)'I', (byte)'I' });
        block.AddRange(UInt16(42, bigEndian));
        block.AddRange(UInt32(directoryOffset, bigEndian));
        block.AddRange(UInt16(1, bigEndian));                // One entry.
        block.AddRange(UInt16(0x0112, bigEndian));           // Orientation tag.
        block.AddRange(UInt16(tagType, bigEndian));
        block.AddRange(UInt32(1, bigEndian));                // One value.
        block.AddRange(UInt16(orientation, bigEndian));
        block.AddRange(new byte[2]);                         // Value field padding.
        block.AddRange(UInt32(8, bigEndian));                // Next directory: itself.
        return block.ToArray();
    }

    #endregion

    #region Private Methods

    private static byte[] UInt16(ushort value, bool bigEndian) => bigEndian
        ? new[] { (byte)(value >> 8), (byte)value }
        : new[] { (byte)value, (byte)(value >> 8) };

    private static byte[] UInt32(uint value, bool bigEndian) => bigEndian
        ? new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value }
        : new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };

    #endregion

    #endregion

    #region Properties, Indexers

    public bool Adobe { get; init; }

    public byte AdobeTransform { get; init; } = 2;

    public int Components { get; init; } = 3;

    public List<byte[]> ExtraSegments { get; init; } = new();

    public byte FrameMarker { get; init; } = 0xC0;

    public int Height { get; init; } = 3;

    public bool IncludeEndOfImage { get; init; } = true;

    public bool IncludeFrame { get; init; } = true;

    public (byte Units, ushort X, ushort Y)? Jfif { get; init; }

    public byte Precision { get; init; } = 8;

    public int ScanCount { get; init; } = 1;

    public int Width { get; init; } = 4;

    #endregion
}
