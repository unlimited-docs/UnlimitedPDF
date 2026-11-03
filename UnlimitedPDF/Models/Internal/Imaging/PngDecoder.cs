using System.IO.Compression;

namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// Decodes the PNGs that PDF cannot read directly (interlaced, 16-bit, with an alpha channel, or with transparency
/// that needs a mask) once at load. Samples are reduced to 8 bits, transparency becomes an 8-bit soft mask, and both
/// are compressed again. Non-interlaced images are processed one row at a time; interlaced images need one buffer.
/// </summary>
internal static class PngDecoder
{
    #region Fields and Constants

    // The largest buffer the decoder allocates (a row, or an interlaced image); above it the image cannot be decoded
    // in memory.
    private const long MaxBufferBytes = int.MaxValue - 64;

    // Deflate cannot expand data by more than about 1032 times (a 258-byte match for every two bits).
    private const long MaxDeflateRatio = 1032;

    // Adam7 passes: x start, y start, x step, y step.
    private static readonly (int X, int Y, int DX, int DY)[] Adam7Passes =
    {
        (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Decodes the image data of a PNG whose chunks <see cref="PngReader"/> has already validated.
    /// </summary>
    /// <param name="header">The image header.</param>
    /// <param name="palette">The palette for palette images; otherwise ignored.</param>
    /// <param name="transparency">The tRNS chunk data, if any.</param>
    /// <param name="compressed">The concatenated IDAT data.</param>
    /// <param name="dpiX">The file's horizontal resolution, if any.</param>
    /// <param name="dpiY">The file's vertical resolution, if any.</param>
    /// <param name="cancellationToken">A token to cancel the load; checked between rows.</param>
    /// <returns>The image data, with a soft mask when the image has transparency.</returns>
    /// <exception cref="PdfImageException">The image data is damaged or too large to decode in memory.</exception>
    public static PdfImageData Decode(
        PngHeader header,
        byte[]? palette,
        byte[]? transparency,
        byte[] compressed,
        double? dpiX,
        double? dpiY,
        CancellationToken cancellationToken)
    {
        var layout = new PixelLayout(header);
        EnsureDecodable(header, layout);
        EnsureImageDataFits(header, compressed.Length);
        var converter = new RowConverter(header, palette, transparency);

        using var colorOutput = new MemoryStream();
        using var alphaOutput = new MemoryStream();
        using (var inflater = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress))
        using (var colorStream = new ZLibStream(colorOutput, CompressionLevel.Optimal, leaveOpen: true))
        using (var alphaStream = new ZLibStream(alphaOutput, CompressionLevel.Optimal, leaveOpen: true))
        {
            try
            {
                if (header.Interlaced)
                    DecodeInterlaced(inflater, header, layout, converter, colorStream, alphaStream, cancellationToken);
                else
                    DecodeRows(inflater, header, layout, converter, colorStream, alphaStream, cancellationToken);

                // Any data after the expected rows means the stream does not match the header, so it is not
                // inflated further (a small file must not expand without bound).
                if (inflater.ReadByte() >= 0) throw Corrupt("contains more image data than its size allows");
            }
            catch (InvalidDataException exception)
            {
                throw new PdfImageException(PdfImageErrorReason.CorruptData, "The PNG image has damaged compressed image data.", exception);
            }
        }

        var colorEntries = new List<string>
        {
            $"/Width {header.Width}",
            $"/Height {header.Height}",
            $"/ColorSpace {converter.ColorSpace}",
            "/BitsPerComponent 8",
            "/Filter /FlateDecode"
        };

        PdfImageData? softMask = null;
        if (converter.HasAlpha)
        {
            var maskEntries = new List<string>
            {
                $"/Width {header.Width}",
                $"/Height {header.Height}",
                "/ColorSpace /DeviceGray",
                "/BitsPerComponent 8",
                "/Filter /FlateDecode"
            };
            softMask = new PdfImageData(header.Width, header.Height, maskEntries, alphaOutput.ToArray(), null, null);
        }

        return new PdfImageData(header.Width, header.Height, colorEntries, colorOutput.ToArray(), dpiX, dpiY, softMask);
    }

    /// <summary>
    /// Throws when the compressed image data is too short to inflate to the rows <paramref name="header"/> declares.
    /// Every PNG is checked, decoded or passed through, so damaged data fails at load without inflating it (AC-07).
    /// </summary>
    /// <param name="header">The image header.</param>
    /// <param name="compressedLength">The total length of the IDAT data.</param>
    /// <exception cref="PdfImageException">The image data cannot hold the image.</exception>
    public static void EnsureImageDataFits(PngHeader header, long compressedLength)
    {
        var layout = new PixelLayout(header);
        long filteredBytes = (layout.RowBytes(header.Width) + 1) * header.Height;
        if (header.Interlaced)
        {
            filteredBytes = 0;
            foreach (var pass in Adam7Passes)
            {
                var (passWidth, passHeight) = PassSize(header, pass);
                if (passWidth > 0) filteredBytes += (layout.RowBytes(passWidth) + 1) * passHeight;
            }
        }

        if (filteredBytes > compressedLength * MaxDeflateRatio) throw Corrupt("has less image data than its size requires");
    }

    #endregion

    #region Private Methods

    private static int ColorChannels(PngColorType colorType) =>
        colorType is PngColorType.Rgb or PngColorType.RgbAlpha ? 3 : 1;

    // Copies one pixel of a packed row into a packed row of the full image.
    private static void CopyPixel(byte[] source, int sourceIndex, byte[] target, long targetRowOffset, int targetIndex, int bitsPerPixel)
    {
        if (bitsPerPixel >= 8)
        {
            int bytes = bitsPerPixel / 8;
            Buffer.BlockCopy(source, sourceIndex * bytes, target, (int)(targetRowOffset + (long)targetIndex * bytes), bytes);
            return;
        }

        long sourceBit = (long)sourceIndex * bitsPerPixel;
        int value = (source[sourceBit / 8] >> (8 - bitsPerPixel - (int)(sourceBit % 8))) & ((1 << bitsPerPixel) - 1);
        long targetBit = (long)targetIndex * bitsPerPixel;
        target[targetRowOffset + targetBit / 8] |= (byte)(value << (8 - bitsPerPixel - (int)(targetBit % 8)));
    }

    private static PdfImageException Corrupt(string detail) =>
        new(PdfImageErrorReason.CorruptData, $"The PNG image {detail}.");

    private static void DecodeInterlaced(
        Stream inflater,
        PngHeader header,
        PixelLayout layout,
        RowConverter converter,
        Stream colorStream,
        Stream alphaStream,
        CancellationToken cancellationToken)
    {
        long fullRowBytes = layout.RowBytes(header.Width);
        var image = new byte[fullRowBytes * header.Height];
        foreach (var (px, py, dx, dy) in Adam7Passes)
        {
            var (passWidth, passHeight) = PassSize(header, (px, py, dx, dy));
            if (passWidth == 0 || passHeight == 0) continue;

            int rowBytes = (int)layout.RowBytes(passWidth);
            var filtered = new byte[rowBytes + 1];
            var prior = new byte[rowBytes];
            var current = new byte[rowBytes];
            for (int r = 0; r < passHeight; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadRow(inflater, filtered, current, prior, layout.BytesPerPixel);

                long targetRow = (long)(py + r * dy) * fullRowBytes;
                for (int i = 0; i < passWidth; i++)
                    CopyPixel(current, i, image, targetRow, px + i * dx, layout.BitsPerPixel);

                (prior, current) = (current, prior);
            }
        }

        var row = new byte[fullRowBytes];
        for (int y = 0; y < header.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Buffer.BlockCopy(image, (int)(y * fullRowBytes), row, 0, (int)fullRowBytes);
            converter.Write(row, colorStream, alphaStream);
        }
    }

    private static void DecodeRows(
        Stream inflater,
        PngHeader header,
        PixelLayout layout,
        RowConverter converter,
        Stream colorStream,
        Stream alphaStream,
        CancellationToken cancellationToken)
    {
        int rowBytes = (int)layout.RowBytes(header.Width);
        var filtered = new byte[rowBytes + 1];
        var prior = new byte[rowBytes];
        var current = new byte[rowBytes];
        for (int y = 0; y < header.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadRow(inflater, filtered, current, prior, layout.BytesPerPixel);
            converter.Write(current, colorStream, alphaStream);
            (prior, current) = (current, prior);
        }
    }

    // Checks every buffer size the header implies before anything is allocated (A06): a row or an interlaced image
    // that cannot fit in one array is too large.
    private static void EnsureDecodable(PngHeader header, PixelLayout layout)
    {
        long rowBytes = layout.RowBytes(header.Width);
        if (rowBytes + 1 > MaxBufferBytes || (long)header.Width * ColorChannels(header.ColorType) > MaxBufferBytes)
        {
            throw new PdfImageException(
                PdfImageErrorReason.TooLarge,
                $"The PNG image is {header.Width} x {header.Height} pixels, so one row is too large to decode in memory.");
        }

        if (header.Interlaced && rowBytes * header.Height > MaxBufferBytes)
        {
            throw new PdfImageException(
                PdfImageErrorReason.TooLarge,
                $"The PNG image is {header.Width} x {header.Height} pixels and interlaced, which is too large to decode in memory.");
        }
    }

    private static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }

    // The width and height of one Adam7 pass; zero when the pass has no pixels.
    private static (int Width, int Height) PassSize(PngHeader header, (int X, int Y, int DX, int DY) pass) =>
    (
        header.Width > pass.X ? (int)(((long)header.Width - pass.X + pass.DX - 1) / pass.DX) : 0,
        header.Height > pass.Y ? (int)(((long)header.Height - pass.Y + pass.DY - 1) / pass.DY) : 0
    );

    // Reads one filtered row and reverses its filter into current, using the previous row of the same pass.
    private static void ReadRow(Stream inflater, byte[] filtered, byte[] current, byte[] prior, int bytesPerPixel)
    {
        int read = 0;
        while (read < filtered.Length)
        {
            int count = inflater.Read(filtered, read, filtered.Length - read);
            if (count == 0) throw Corrupt("has less image data than its size requires");
            read += count;
        }

        int filterType = filtered[0];
        if (filterType > 4) throw Corrupt($"uses invalid row filter type {filterType}");

        for (int i = 0; i < current.Length; i++)
        {
            int raw = filtered[i + 1];
            int a = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
            int b = prior[i];
            int c = i >= bytesPerPixel ? prior[i - bytesPerPixel] : 0;
            current[i] = filterType switch
            {
                0 => (byte)raw,
                1 => (byte)(raw + a),
                2 => (byte)(raw + b),
                3 => (byte)(raw + (a + b) / 2),
                _ => (byte)(raw + Paeth(a, b, c))
            };
        }
    }

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// The packed size of a PNG pixel.
    /// </summary>
    private sealed class PixelLayout
    {
        #region Constructors and Finalizers

        public PixelLayout(PngHeader header)
        {
            int channels = header.ColorType switch
            {
                PngColorType.Rgb => 3,
                PngColorType.GrayAlpha => 2,
                PngColorType.RgbAlpha => 4,
                _ => 1
            };
            BitsPerPixel = channels * header.BitDepth;
            BytesPerPixel = Math.Max(1, BitsPerPixel / 8);
        }

        #endregion

        #region Methods

        #region Public Methods

        public long RowBytes(int width) => ((long)width * BitsPerPixel + 7) / 8;

        #endregion

        #endregion

        #region Properties, Indexers

        public int BitsPerPixel { get; }

        public int BytesPerPixel { get; }

        #endregion
    }

    /// <summary>
    /// Converts unfiltered PNG rows to 8-bit color (or palette index) rows and 8-bit alpha rows.
    /// </summary>
    private sealed class RowConverter
    {
        #region Fields and Constants

        private readonly byte[] _alphaRow;
        private readonly int _bitDepth;
        private readonly byte[] _colorRow;
        private readonly PngColorType _colorType;
        private readonly int[]? _key;
        private readonly byte[]? _paletteAlpha;
        private readonly int _paletteEntries;
        private readonly int _width;

        #endregion

        #region Constructors and Finalizers

        public RowConverter(PngHeader header, byte[]? palette, byte[]? transparency)
        {
            _width = header.Width;
            _bitDepth = header.BitDepth;
            _colorType = header.ColorType;
            _paletteEntries = palette is null ? 0 : palette.Length / 3;

            _colorRow = new byte[_width * ColorChannels(header.ColorType)];
            _alphaRow = new byte[_width];

            switch (header.ColorType)
            {
                case PngColorType.Palette:
                    ColorSpace = $"[/Indexed /DeviceRGB {_paletteEntries - 1} <{Convert.ToHexString(palette!)}>]";
                    if (transparency is not null && transparency.Any(alpha => alpha != 255))
                    {
                        _paletteAlpha = transparency;
                        HasAlpha = true;
                    }
                    break;
                case PngColorType.Gray:
                case PngColorType.GrayAlpha:
                    ColorSpace = "/DeviceGray";
                    break;
                default:
                    ColorSpace = "/DeviceRGB";
                    break;
            }

            if (header.ColorType is PngColorType.GrayAlpha or PngColorType.RgbAlpha)
            {
                HasAlpha = true;
            }
            else if (transparency is not null && header.ColorType is PngColorType.Gray or PngColorType.Rgb)
            {
                // A color key is compared at full precision, so 16-bit keys stay exact after the 8-bit reduction.
                int sampleMask = header.BitDepth == 16 ? 0xFFFF : (1 << header.BitDepth) - 1;
                _key = new int[transparency.Length / 2];
                for (int i = 0; i < _key.Length; i++)
                    _key[i] = ((transparency[2 * i] << 8) | transparency[2 * i + 1]) & sampleMask;
                HasAlpha = true;
            }
        }

        #endregion

        #region Methods

        #region Public Methods

        public void Write(byte[] row, Stream colorStream, Stream alphaStream)
        {
            switch (_colorType)
            {
                case PngColorType.Gray:
                    for (int x = 0; x < _width; x++)
                    {
                        int value = Sample(row, x, 0, 1);
                        _colorRow[x] = ToByte(value);
                        _alphaRow[x] = _key is not null && value == _key[0] ? (byte)0 : (byte)255;
                    }
                    break;
                case PngColorType.Rgb:
                    for (int x = 0; x < _width; x++)
                    {
                        int r = Sample(row, x, 0, 3), g = Sample(row, x, 1, 3), b = Sample(row, x, 2, 3);
                        _colorRow[3 * x] = ToByte(r);
                        _colorRow[3 * x + 1] = ToByte(g);
                        _colorRow[3 * x + 2] = ToByte(b);
                        _alphaRow[x] = _key is not null && r == _key[0] && g == _key[1] && b == _key[2] ? (byte)0 : (byte)255;
                    }
                    break;
                case PngColorType.Palette:
                    for (int x = 0; x < _width; x++)
                    {
                        int index = Sample(row, x, 0, 1);
                        if (index >= _paletteEntries) throw Corrupt($"uses palette index {index}, outside its {_paletteEntries}-entry palette");
                        _colorRow[x] = (byte)index;
                        _alphaRow[x] = _paletteAlpha is not null && index < _paletteAlpha.Length ? _paletteAlpha[index] : (byte)255;
                    }
                    break;
                case PngColorType.GrayAlpha:
                    for (int x = 0; x < _width; x++)
                    {
                        _colorRow[x] = ToByte(Sample(row, x, 0, 2));
                        _alphaRow[x] = ToByte(Sample(row, x, 1, 2));
                    }
                    break;
                default:
                    for (int x = 0; x < _width; x++)
                    {
                        _colorRow[3 * x] = ToByte(Sample(row, x, 0, 4));
                        _colorRow[3 * x + 1] = ToByte(Sample(row, x, 1, 4));
                        _colorRow[3 * x + 2] = ToByte(Sample(row, x, 2, 4));
                        _alphaRow[x] = ToByte(Sample(row, x, 3, 4));
                    }
                    break;
            }

            colorStream.Write(_colorRow, 0, _colorRow.Length);
            if (HasAlpha) alphaStream.Write(_alphaRow, 0, _alphaRow.Length);
        }

        #endregion

        #region Private Methods

        // Reads a sample at the image's bit depth; samples below 8 bits are packed from the most significant bit.
        private int Sample(byte[] row, int x, int channel, int channels)
        {
            int index = x * channels + channel;
            switch (_bitDepth)
            {
                case 16:
                    return (row[2 * index] << 8) | row[2 * index + 1];
                case 8:
                    return row[index];
                default:
                    long bit = (long)index * _bitDepth;
                    return (row[bit / 8] >> (8 - _bitDepth - (int)(bit % 8))) & ((1 << _bitDepth) - 1);
            }
        }

        // Scales a sample to 8 bits: low depths are stretched to the full range, 16-bit samples keep their high byte.
        // Palette indexes are never scaled.
        private byte ToByte(int value) => _bitDepth switch
        {
            16 => (byte)(value >> 8),
            8 => (byte)value,
            _ => (byte)(value * 255 / ((1 << _bitDepth) - 1))
        };

        #endregion

        #endregion

        #region Properties, Indexers

        public string ColorSpace { get; }

        public bool HasAlpha { get; }

        #endregion
    }

    #endregion
}
