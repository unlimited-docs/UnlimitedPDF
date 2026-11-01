namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// Reads a JPEG file's marker structure without decoding it. The JPEG bytes are embedded unchanged with the
/// DCTDecode filter, so only the frame header, resolution, EXIF orientation, and Adobe marker are needed.
/// </summary>
internal static class JpegReader
{
    #region Fields and Constants

    private const string CmykDecodeEntry = "/Decode [1 0 1 0 1 0 1 0]";
    private const byte EndOfImage = 0xD9;
    private const string ExifIdentifier = "Exif\0\0";
    private const byte FirstRestart = 0xD0;
    private const byte LastRestart = 0xD7;
    private const byte MarkerPrefix = 0xFF;
    private const byte StartOfImage = 0xD8;
    private const byte StartOfScan = 0xDA;
    private const byte Temporary = 0x01;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads <paramref name="data"/>, which starts with the JPEG signature, into PDF-ready image data.
    /// </summary>
    /// <param name="data">The complete JPEG file bytes, owned by the caller's load.</param>
    /// <param name="settings">The settings captured when the load started.</param>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>The image data holding <paramref name="data"/> unchanged.</returns>
    /// <exception cref="PdfImageException">The JPEG is damaged, uses unsupported coding, or is over the pixel limit.</exception>
    public static PdfImageData Read(byte[] data, ImageLoadSettings settings, CancellationToken cancellationToken)
    {
        Frame? frame = null;
        bool adobe = false;
        bool exifSeen = false;
        int orientation = ExifReader.Upright;
        bool scanSeen = false;
        double? dpiX = null;
        double? dpiY = null;

        int position = 2; // After the start-of-image marker.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte marker = ReadMarker(data, ref position);

            if (marker == EndOfImage)
            {
                if (!scanSeen) throw Corrupt("ends before any image data (no scan)");
                break;
            }

            if (marker == Temporary || (marker >= FirstRestart && marker <= LastRestart))
                continue; // Standalone markers carry no segment.

            if (marker == StartOfImage)
                throw Corrupt("contains a second start-of-image marker");

            int length = ReadSegmentLength(data, position, marker);
            int payload = position + 2;
            int end = position + length;

            if (marker == StartOfScan)
            {
                if (frame is null) throw Corrupt("starts a scan before the frame header");
                ValidateScanHeader(data, payload, length);
                position = SkipEntropyCodedData(data, end);
                scanSeen = true;
                continue;
            }

            if (IsSupportedFrame(marker))
            {
                if (frame is not null) throw Corrupt("has more than one frame header");
                frame = ReadFrame(data, payload, length);
                ImageLimits.EnsureWithinLimit(frame.Width, frame.Height, settings.MaxPixels);
            }
            else if (IsUnsupportedFrame(marker))
            {
                throw new PdfImageException(
                    PdfImageErrorReason.UnsupportedFormat,
                    $"The JPEG image uses {DescribeUnsupportedFrame(marker)} coding. Only baseline and progressive JPEG images are supported.");
            }
            else if (marker == 0xE0 && dpiX is null)
            {
                (dpiX, dpiY) = ReadJfifResolution(data, payload, length);
            }
            else if (marker == 0xEE && HasAscii(data, payload, length - 2, "Adobe"))
            {
                adobe = true;
            }
            else if (marker == 0xE1 && !exifSeen && HasAscii(data, payload, length - 2, ExifIdentifier))
            {
                // Only the first EXIF block counts; the TIFF structure follows the identifier.
                exifSeen = true;
                orientation = ExifReader.ReadOrientation(data, payload + ExifIdentifier.Length, length - 2 - ExifIdentifier.Length);
            }

            position = end;
        }

        // A scan always follows a frame header, so the frame is known here.
        var entries = new List<string>
        {
            $"/Width {frame!.Width}",
            $"/Height {frame.Height}",
            $"/ColorSpace {ColorSpace(frame.Components)}",
            "/BitsPerComponent 8",
            "/Filter /DCTDecode"
        };

        if (frame.Components == 4 && IsCmykInverted(settings.CmykInversion, adobe))
            entries.Add(CmykDecodeEntry);

        // The JPEG bytes stay as stored; orientation is applied as a placement transform, and the reported size is
        // the displayed size.
        if (!settings.ApplyExifOrientation) orientation = ExifReader.Upright;
        bool swap = ExifReader.SwapsDimensions(orientation);
        int displayedWidth = swap ? frame.Height : frame.Width;
        int displayedHeight = swap ? frame.Width : frame.Height;

        return new PdfImageData(displayedWidth, displayedHeight, entries, data, dpiX, dpiY, orientation: orientation);
    }

    #endregion

    #region Private Methods

    private static string ColorSpace(int components) => components switch
    {
        1 => "/DeviceGray",
        3 => "/DeviceRGB",
        _ => "/DeviceCMYK"
    };

    private static PdfImageException Corrupt(string detail) =>
        new(PdfImageErrorReason.CorruptData, $"The JPEG image {detail}.");

    private static string DescribeUnsupportedFrame(byte marker) => marker switch
    {
        0xC3 => "lossless",
        0xC5 or 0xC6 or 0xC7 => "hierarchical",
        _ => "arithmetic"
    };

    private static bool HasAscii(byte[] data, int offset, int available, string text)
    {
        if (available < text.Length) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (data[offset + i] != text[i]) return false;
        }
        return true;
    }

    // An Adobe segment marks inverted CMYK values; the option can force either reading (D-051).
    private static bool IsCmykInverted(PdfCmykInversion inversion, bool adobe) => inversion switch
    {
        PdfCmykInversion.Inverted => true,
        PdfCmykInversion.NotInverted => false,
        _ => adobe
    };

    // SOF0 baseline, SOF1 extended sequential, SOF2 progressive (all Huffman-coded).
    private static bool IsSupportedFrame(byte marker) => marker is 0xC0 or 0xC1 or 0xC2;

    // Lossless, hierarchical, and arithmetic-coded frames. 0xC4 (DHT), 0xC8 (reserved), and 0xCC (DAC) are not frames.
    private static bool IsUnsupportedFrame(byte marker) =>
        marker is 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;

    private static Frame ReadFrame(byte[] data, int payload, int length)
    {
        if (length < 8) throw Corrupt("has a frame header that is too short");

        int precision = data[payload];
        int height = ReadUInt16(data, payload + 1);
        int width = ReadUInt16(data, payload + 3);
        int components = data[payload + 5];

        if (length != 8 + 3 * components) throw Corrupt("has a frame header whose length does not match its components");
        if (precision != 8)
            throw new PdfImageException(PdfImageErrorReason.UnsupportedFormat, $"The JPEG image uses {precision}-bit samples. Only 8-bit JPEG images are supported.");
        if (width == 0 || height == 0) throw Corrupt("has a zero width or height");
        if (components is not (1 or 3 or 4))
            throw new PdfImageException(PdfImageErrorReason.UnsupportedFormat, $"The JPEG image has {components} color components. Only grayscale, RGB, and CMYK JPEG images are supported.");

        return new Frame(width, height, components);
    }

    private static (double? X, double? Y) ReadJfifResolution(byte[] data, int payload, int length)
    {
        // JFIF: identifier (5), version (2), units (1), X density (2), Y density (2), thumbnail size (2).
        if (length < 16 || !HasAscii(data, payload, length - 2, "JFIF\0")) return (null, null);

        int units = data[payload + 7];
        int x = ReadUInt16(data, payload + 8);
        int y = ReadUInt16(data, payload + 10);
        if (x == 0 || y == 0) return (null, null);

        return units switch
        {
            1 => (x, y),
            2 => (x * 2.54, y * 2.54), // Dots per centimeter.
            _ => (null, null) // Aspect ratio only.
        };
    }

    private static byte ReadMarker(byte[] data, ref int position)
    {
        if (position >= data.Length) throw Corrupt("is truncated: the end-of-image marker is missing");
        if (data[position] != MarkerPrefix) throw Corrupt($"has unexpected data at byte {position} where a marker was expected");

        // Any number of 0xFF fill bytes may precede a marker code.
        while (position < data.Length && data[position] == MarkerPrefix) position++;
        if (position >= data.Length) throw Corrupt("is truncated: the end-of-image marker is missing");

        byte marker = data[position++];
        if (marker == 0x00) throw Corrupt($"has an invalid marker at byte {position - 1}");
        return marker;
    }

    private static int ReadSegmentLength(byte[] data, int position, byte marker)
    {
        if (position + 2 > data.Length) throw Corrupt("is truncated inside a segment header");

        int length = ReadUInt16(data, position);
        if (length < 2) throw Corrupt($"has an invalid segment length for marker 0x{marker:X2}");
        if (position + length > data.Length) throw Corrupt($"is truncated inside the segment for marker 0x{marker:X2}");
        return length;
    }

    private static int ReadUInt16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

    // Returns the position of the next marker after entropy-coded data. Stuffed zeros, restart markers, and fill
    // bytes belong to the scan.
    private static int SkipEntropyCodedData(byte[] data, int position)
    {
        int i = position;
        while (i < data.Length)
        {
            if (data[i] != MarkerPrefix)
            {
                i++;
                continue;
            }

            if (i + 1 >= data.Length) break;
            byte next = data[i + 1];
            if (next == 0x00 || (next >= FirstRestart && next <= LastRestart))
            {
                i += 2;
            }
            else if (next == MarkerPrefix)
            {
                i++;
            }
            else
            {
                return i;
            }
        }

        throw Corrupt("is truncated: the end-of-image marker is missing");
    }

    private static void ValidateScanHeader(byte[] data, int payload, int length)
    {
        if (length < 6) throw Corrupt("has a scan header that is too short");
        int components = data[payload];
        if (components is < 1 or > 4 || length != 6 + 2 * components)
            throw Corrupt("has a scan header whose length does not match its components");
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed record Frame(int Width, int Height, int Components);

    #endregion
}
