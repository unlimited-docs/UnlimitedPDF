namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// Reads a PNG file's chunks. A PNG that needs no decoding (not interlaced, at most 8 bits per sample, no alpha
/// channel, no palette transparency) is embedded from its original compressed data, which PDF reads directly with
/// the Flate filter and PNG predictors. Other PNGs are decoded once at load.
/// </summary>
internal static class PngReader
{
    #region Fields and Constants

    private const int ChunkOverhead = 12; // Length (4), type (4), CRC (4).
    private const int SignatureLength = 8;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads <paramref name="data"/>, which starts with the PNG signature, into PDF-ready image data.
    /// </summary>
    /// <param name="data">The complete PNG file bytes, owned by the caller's load.</param>
    /// <param name="settings">The settings captured when the load started.</param>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>The image data.</returns>
    /// <exception cref="PdfImageException">The PNG is damaged, uses an unknown critical chunk, or is over the pixel limit.</exception>
    public static PdfImageData Read(byte[] data, ImageLoadSettings settings, CancellationToken cancellationToken)
    {
        PngHeader? header = null;
        byte[]? palette = null;
        byte[]? transparency = null;
        double? dpiX = null;
        double? dpiY = null;
        using var compressed = new MemoryStream();
        bool imageDataSeen = false;
        bool endSeen = false;

        int position = SignatureLength;
        while (!endSeen)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (position + ChunkOverhead > data.Length) throw Corrupt("is truncated: the IEND chunk is missing");

            long length = ReadUInt32(data, position);
            string type = ReadChunkType(data, position + 4);
            int chunkData = position + 8;
            if (length > data.Length - chunkData - 4) throw Corrupt($"is truncated inside the {type} chunk");

            int dataLength = (int)length;
            if (Crc32.Compute(data, position + 4, dataLength + 4) != ReadUInt32(data, chunkData + dataLength))
                throw Corrupt($"has a damaged {type} chunk (checksum mismatch)");

            if (header is null && type != "IHDR") throw Corrupt("does not start with an IHDR chunk");

            switch (type)
            {
                case "IHDR":
                    if (header is not null) throw Corrupt("has more than one IHDR chunk");
                    header = ReadHeader(data, chunkData, dataLength);
                    ImageLimits.EnsureWithinLimit(header.Width, header.Height, settings.MaxPixels);
                    break;
                case "PLTE":
                    palette = ReadPalette(data, chunkData, dataLength, header);
                    break;
                case "tRNS":
                    transparency = data[chunkData..(chunkData + dataLength)];
                    break;
                case "pHYs":
                    (dpiX, dpiY) = ReadResolution(data, chunkData, dataLength);
                    break;
                case "IDAT":
                    compressed.Write(data, chunkData, dataLength);
                    imageDataSeen = true;
                    break;
                case "IEND":
                    endSeen = true;
                    break;
                default:
                    // Ancillary chunks (lowercase first letter), including compressed ones such as iCCP, zTXt, and
                    // iTXt, are skipped without being inflated. Unknown critical chunks change how the image must be
                    // read, so they cannot be ignored.
                    if (char.IsUpper(type[0]))
                        throw new PdfImageException(PdfImageErrorReason.UnsupportedFormat, $"The PNG image uses the unknown critical chunk '{type}'.");
                    break;
            }

            position = chunkData + dataLength + 4;
        }

        if (!imageDataSeen) throw Corrupt("has no image data (IDAT chunk)");

        // IHDR is always read before the loop can end.
        var image = header!;
        if (image.ColorType == PngColorType.Palette && palette is null) throw Corrupt("is a palette image without a PLTE chunk");
        ValidateTransparency(image, palette, transparency);

        if (CanPassThrough(image, transparency))
        {
            PngDecoder.EnsureImageDataFits(image, compressed.Length);
            return PassThrough(image, palette, transparency, compressed.ToArray(), dpiX, dpiY);
        }

        return PngDecoder.Decode(image, palette, transparency, compressed.ToArray(), dpiX, dpiY, cancellationToken);
    }

    #endregion

    #region Private Methods

    // Pass-through needs data PDF can read as-is: no interlacing, at most 8 bits, no alpha channel, and no palette
    // transparency. Gray and RGB color keys become a /Mask array instead of a mask image.
    private static bool CanPassThrough(PngHeader header, byte[]? transparency)
    {
        if (header.Interlaced || header.BitDepth > 8) return false;
        if (header.ColorType is PngColorType.GrayAlpha or PngColorType.RgbAlpha) return false;
        if (header.ColorType == PngColorType.Palette && transparency is not null && transparency.Any(alpha => alpha != 255)) return false;
        return true;
    }

    private static string ColorKeyMask(PngHeader header, byte[] transparency)
    {
        // Each key sample is stored as 16 bits; only the low bits for the image's depth are used.
        int sampleMask = (1 << header.BitDepth) - 1;
        var values = new List<string>();
        for (int i = 0; i + 1 < transparency.Length; i += 2)
        {
            int sample = ReadUInt16(transparency, i) & sampleMask;
            values.Add($"{sample} {sample}");
        }
        return $"/Mask [{string.Join(" ", values)}]";
    }

    private static PdfImageException Corrupt(string detail) =>
        new(PdfImageErrorReason.CorruptData, $"The PNG image {detail}.");

    private static bool IsValidDepth(PngColorType colorType, int bitDepth) => colorType switch
    {
        PngColorType.Gray => bitDepth is 1 or 2 or 4 or 8 or 16,
        PngColorType.Palette => bitDepth is 1 or 2 or 4 or 8,
        PngColorType.Rgb or PngColorType.GrayAlpha or PngColorType.RgbAlpha => bitDepth is 8 or 16,
        _ => false
    };

    private static PdfImageData PassThrough(PngHeader header, byte[]? palette, byte[]? transparency, byte[] compressed, double? dpiX, double? dpiY)
    {
        int colors = header.ColorType == PngColorType.Rgb ? 3 : 1;
        string colorSpace = header.ColorType switch
        {
            PngColorType.Gray => "/DeviceGray",
            PngColorType.Rgb => "/DeviceRGB",
            _ => $"[/Indexed /DeviceRGB {palette!.Length / 3 - 1} <{Convert.ToHexString(palette)}>]"
        };

        var entries = new List<string>
        {
            $"/Width {header.Width}",
            $"/Height {header.Height}",
            $"/ColorSpace {colorSpace}",
            $"/BitsPerComponent {header.BitDepth}",
            "/Filter /FlateDecode",
            $"/DecodeParms << /Predictor 15 /Colors {colors} /BitsPerComponent {header.BitDepth} /Columns {header.Width} >>"
        };

        if (transparency is not null && header.ColorType is PngColorType.Gray or PngColorType.Rgb)
            entries.Add(ColorKeyMask(header, transparency));

        return new PdfImageData(header.Width, header.Height, entries, compressed, dpiX, dpiY);
    }

    private static string ReadChunkType(byte[] data, int offset)
    {
        for (int i = 0; i < 4; i++)
        {
            byte c = data[offset + i];
            if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))) throw Corrupt($"has an invalid chunk type at byte {offset}");
        }
        return Encoding.ASCII.GetString(data, offset, 4);
    }

    private static PngHeader ReadHeader(byte[] data, int offset, int length)
    {
        if (length != 13) throw Corrupt("has an IHDR chunk of the wrong size");

        long width = ReadUInt32(data, offset);
        long height = ReadUInt32(data, offset + 4);
        int bitDepth = data[offset + 8];
        int colorType = data[offset + 9];
        int compression = data[offset + 10];
        int filter = data[offset + 11];
        int interlace = data[offset + 12];

        if (width == 0 || height == 0) throw Corrupt("has a zero width or height");
        if (width > int.MaxValue || height > int.MaxValue) throw Corrupt("has a width or height above the PNG maximum");
        if (!IsValidDepth((PngColorType)colorType, bitDepth))
            throw Corrupt($"has an invalid combination of color type {colorType} and bit depth {bitDepth}");
        if (compression != 0 || filter != 0) throw Corrupt("uses an unknown compression or filter method");
        if (interlace > 1) throw Corrupt("uses an unknown interlace method");

        return new PngHeader((int)width, (int)height, bitDepth, (PngColorType)colorType, interlace == 1);
    }

    private static byte[] ReadPalette(byte[] data, int offset, int length, PngHeader? header)
    {
        if (header!.ColorType is PngColorType.Gray or PngColorType.GrayAlpha) throw Corrupt("has a PLTE chunk in a grayscale image");
        if (length == 0 || length % 3 != 0 || length / 3 > 256) throw Corrupt("has a PLTE chunk of invalid size");
        return data[offset..(offset + length)];
    }

    private static (double? X, double? Y) ReadResolution(byte[] data, int offset, int length)
    {
        // pHYs: pixels per unit in X (4) and Y (4), then the unit (1 = meter, 0 = aspect ratio only).
        if (length != 9 || data[offset + 8] != 1) return (null, null);

        long x = ReadUInt32(data, offset);
        long y = ReadUInt32(data, offset + 4);
        if (x == 0 || y == 0) return (null, null);
        return (x * 0.0254, y * 0.0254);
    }

    private static int ReadUInt16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

    private static long ReadUInt32(byte[] data, int offset) =>
        ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) | ((long)data[offset + 2] << 8) | data[offset + 3];

    private static void ValidateTransparency(PngHeader header, byte[]? palette, byte[]? transparency)
    {
        if (transparency is null) return;

        bool valid = header.ColorType switch
        {
            PngColorType.Gray => transparency.Length == 2,
            PngColorType.Rgb => transparency.Length == 6,
            PngColorType.Palette => transparency.Length <= palette!.Length / 3,
            _ => false // Images with an alpha channel must not carry tRNS.
        };

        if (!valid) throw Corrupt("has a tRNS chunk that does not match its color type");
    }

    #endregion

    #endregion
}
