using System.IO.Compression;
using UnlimitedPDF.Models.Internal.Imaging;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Encodes project-authored PNG test images (PR-011) for every color type, bit depth, and interlacing mode. Rows use
/// all five PNG filter types in turn, and the source samples stay available so decoded output can be compared.
/// </summary>
internal sealed class PngBuilder
{
    #region Fields and Constants

    // Adam7 passes: x start, y start, x step, y step.
    private static readonly (int X, int Y, int DX, int DY)[] Passes =
    {
        (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)
    };

    private int[,,]? _samples;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the bit depths the PNG specification allows for <paramref name="colorType"/>.
    /// </summary>
    public static int[] AllowedDepths(int colorType) => colorType switch
    {
        0 => new[] { 1, 2, 4, 8, 16 },
        3 => new[] { 1, 2, 4, 8 },
        _ => new[] { 8, 16 }
    };

    /// <summary>
    /// Returns the PNG file bytes.
    /// </summary>
    public byte[] Build()
    {
        var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        if (IncludeHeader)
        {
            var header = new List<byte>();
            header.AddRange(UInt32(Width));
            header.AddRange(UInt32(Height));
            header.AddRange(new[] { (byte)BitDepth, (byte)ColorType, (byte)0, (byte)0, (byte)(Interlaced ? 1 : 0) });
            WriteChunk(output, "IHDR", header.ToArray());
        }

        foreach (var (type, data) in ChunksBeforeImageData)
            WriteChunk(output, type, data);

        if (ColorType == 3 && IncludePalette)
            WriteChunk(output, "PLTE", Palette);

        if (Transparency is not null)
            WriteChunk(output, "tRNS", Transparency);

        if (PixelsPerMeter is { } ppm)
            WriteChunk(output, "pHYs", UInt32(ppm.X).Concat(UInt32(ppm.Y)).Append(ppm.Unit).ToArray());

        var imageData = CompressedImageData;
        if (IncludeImageData)
        {
            // Split across several IDAT chunks to exercise concatenation.
            int parts = Math.Max(1, Math.Min(ImageDataChunks, imageData.Length));
            int size = (imageData.Length + parts - 1) / parts;
            for (int offset = 0; offset < imageData.Length; offset += size)
                WriteChunk(output, "IDAT", imageData[offset..Math.Min(imageData.Length, offset + size)]);
        }

        if (IncludeEnd)
            WriteChunk(output, "IEND", Array.Empty<byte>());

        return output.ToArray();
    }

    /// <summary>
    /// Returns the source sample of channel <paramref name="channel"/> at (<paramref name="x"/>, <paramref name="y"/>):
    /// a palette index for palette images, otherwise a value from 0 to 2^bitDepth - 1.
    /// </summary>
    public int Sample(int x, int y, int channel) => Samples[y, x, channel];

    #endregion

    #region Private Methods

    private byte[] Compress(byte[] raw)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);
        return buffer.ToArray();
    }

    private int[,,] CreateSamples()
    {
        var samples = new int[Height, Width, Channels];
        int max = ColorType == 3 ? Math.Min(Palette.Length / 3, 1 << BitDepth) : 1 << BitDepth;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        for (int c = 0; c < Channels; c++)
        {
            // A deterministic spread that reaches both ends of the range and differs per channel.
            long hash = (x * 7919L + y * 104729L + c * 1299709L + Seed) * 2654435761L;
            samples[y, x, c] = (int)((hash >> 7) & 0x7FFFFFFF) % max;
        }

        if (SampleOverride is not null)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            for (int c = 0; c < Channels; c++)
                samples[y, x, c] = SampleOverride(x, y, c);
            return samples;
        }

        if (Width > 1 && Height > 0)
        {
            for (int c = 0; c < Channels; c++)
            {
                samples[0, 0, c] = 0;
                samples[0, 1, c] = max - 1;
            }
        }
        return samples;
    }

    private byte[] Filter(byte[] row, byte[] prior, int filterType, int bytesPerPixel)
    {
        var output = new byte[row.Length + 1];
        output[0] = (byte)filterType;
        for (int i = 0; i < row.Length; i++)
        {
            int a = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
            int b = prior[i];
            int c = i >= bytesPerPixel ? prior[i - bytesPerPixel] : 0;
            int predictor = filterType switch
            {
                1 => a,
                2 => b,
                3 => (a + b) / 2,
                4 => Paeth(a, b, c),
                _ => 0
            };
            output[i + 1] = (byte)(row[i] - predictor);
        }
        return output;
    }

    private byte[] PackRow(int y, int xStart, int xStep, int count)
    {
        int bitsPerPixel = Channels * BitDepth;
        var row = new byte[(count * bitsPerPixel + 7) / 8];
        int bit = 0;
        for (int i = 0; i < count; i++)
        {
            int x = xStart + i * xStep;
            for (int c = 0; c < Channels; c++)
            {
                int value = Samples[y, x, c];
                if (BitDepth == 16)
                {
                    row[bit / 8] = (byte)(value >> 8);
                    row[bit / 8 + 1] = (byte)value;
                }
                else if (BitDepth == 8)
                {
                    row[bit / 8] = (byte)value;
                }
                else
                {
                    // Samples below 8 bits are packed from the most significant bit.
                    int shift = 8 - BitDepth - bit % 8;
                    row[bit / 8] |= (byte)(value << shift);
                }
                bit += BitDepth;
            }
        }
        return row;
    }

    private static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }

    private byte[] RawImageData()
    {
        int bytesPerPixel = Math.Max(1, Channels * BitDepth / 8);
        var raw = new MemoryStream();
        var passes = Interlaced ? Passes : new[] { (0, 0, 1, 1) };
        foreach (var (px, py, dx, dy) in passes)
        {
            int passWidth = Width > px ? (Width - px + dx - 1) / dx : 0;
            int passHeight = Height > py ? (Height - py + dy - 1) / dy : 0;
            if (passWidth == 0 || passHeight == 0) continue;

            byte[]? prior = null;
            for (int r = 0; r < passHeight; r++)
            {
                var row = PackRow(py + r * dy, px, dx, passWidth);
                prior ??= new byte[row.Length];
                raw.Write(Filter(row, prior, FilterOverride ?? (r % 5), bytesPerPixel));
                prior = row;
            }
        }
        return raw.ToArray();
    }

    private static byte[] UInt32(int value) =>
        new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        output.Write(UInt32(data.Length));
        output.Write(typeAndData);
        output.Write(UInt32((int)Crc32.Compute(typeAndData, 0, typeAndData.Length)));
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public int BitDepth { get; init; } = 8;

    /// <summary>
    /// Gets the samples per pixel for the color type.
    /// </summary>
    public int Channels => ColorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };

    public List<(string Type, byte[] Data)> ChunksBeforeImageData { get; init; } = new();

    public int ColorType { get; init; } = 2;

    /// <summary>
    /// Gets the zlib data of the IDAT chunks, which pass-through embeds unchanged.
    /// </summary>
    public byte[] CompressedImageData => CompressedOverride ?? Compress(RawTransform is null ? RawImageData() : RawTransform(RawImageData()));

    /// <summary>
    /// Gets or sets IDAT data used as-is instead of the encoded image, for damaged-data tests.
    /// </summary>
    public byte[]? CompressedOverride { get; init; }

    /// <summary>
    /// Gets or sets a filter type used for every row instead of cycling through all five; a value above 4 is invalid.
    /// </summary>
    public int? FilterOverride { get; init; }

    public int Height { get; init; } = 5;

    public int ImageDataChunks { get; init; } = 2;

    public bool IncludeEnd { get; init; } = true;

    public bool IncludeHeader { get; init; } = true;

    public bool IncludeImageData { get; init; } = true;

    public bool IncludePalette { get; init; } = true;

    public bool Interlaced { get; init; }

    /// <summary>
    /// Gets the palette (RGB triples) used for color type 3; 16 entries by default.
    /// </summary>
    public byte[] Palette { get; init; } = Enumerable.Range(0, 48).Select(i => (byte)(i * 5)).ToArray();

    public (int X, int Y, byte Unit)? PixelsPerMeter { get; init; }

    /// <summary>
    /// Gets or sets a change applied to the filtered rows before compression, for truncated or oversized data tests.
    /// </summary>
    public Func<byte[], byte[]>? RawTransform { get; init; }

    /// <summary>
    /// Gets or sets a function (x, y, channel) that replaces the generated samples.
    /// </summary>
    public Func<int, int, int, int>? SampleOverride { get; init; }

    private int[,,] Samples => _samples ??= CreateSamples();

    public int Seed { get; init; }

    public byte[]? Transparency { get; init; }

    public int Width { get; init; } = 7;

    #endregion
}
