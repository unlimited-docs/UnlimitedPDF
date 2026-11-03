using System.IO.Compression;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Internal.Imaging;

namespace UnlimitedPDF.Tests;

/// <summary>
/// PNGs that PDF cannot read directly are decoded once at load: every variant's decoded samples equal the source
/// pixels after 8-bit reduction, transparency becomes a soft mask equal to the source alpha, and damaged or oversized
/// data fails safely (AC-02, AC-03, AC-25, AC-26; D-019, D-039; FA-03; A06).
/// </summary>
public class PdfImagePngDecodingTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Cancellation_is_checked_while_decoding()
    {
        var builder = new PngBuilder { ColorType = 6, Width = 8, Height = 8 };
        var header = new PngHeader(8, 8, 8, PngColorType.RgbAlpha, false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            PngDecoder.Decode(header, null, null, builder.CompressedImageData, null, null, cancellation.Token));
    }

    [Theory]
    [InlineData(0, 16, false)]
    [InlineData(2, 16, false)]
    [InlineData(0, 8, true)]
    [InlineData(2, 8, true)]
    [InlineData(0, 2, true)]
    public void Color_keys_on_decoded_images_become_a_soft_mask(int colorType, int depth, bool interlaced)
    {
        // The builder sets pixel (0, 0) to zero in every channel, so a zero key makes at least that pixel transparent.
        var key = new byte[colorType == 0 ? 2 : 6];
        var builder = new PngBuilder { ColorType = colorType, BitDepth = depth, Interlaced = interlaced, Transparency = key, Width = 9, Height = 7 };

        var image = PdfImage.FromBytes(builder.Build());

        var expected = new byte[builder.Width * builder.Height];
        for (int y = 0; y < builder.Height; y++)
        for (int x = 0; x < builder.Width; x++)
        {
            bool isKey = Enumerable.Range(0, builder.Channels).All(c => builder.Sample(x, y, c) == 0);
            expected[y * builder.Width + x] = isKey ? (byte)0 : (byte)255;
        }

        Assert.NotNull(image.Data.SoftMask);
        Assert.Equal(expected, Inflate(image.Data.SoftMask!.EncodedData));
        Assert.Contains((byte)0, expected);
        Assert.DoesNotContain(image.Data.DictionaryEntries, e => e.StartsWith("/Mask", StringComparison.Ordinal));
    }

    public static TheoryData<int, int, bool> DecodedVariants()
    {
        var data = new TheoryData<int, int, bool>();
        foreach (var colorType in new[] { 0, 2, 3, 4, 6 })
        foreach (var depth in PngBuilder.AllowedDepths(colorType))
        foreach (var interlaced in new[] { false, true })
        {
            bool passThrough = !interlaced && depth <= 8 && colorType is 0 or 2 or 3;
            if (!passThrough) data.Add(colorType, depth, interlaced);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(DecodedVariants))]
    public void Decoded_variants_match_source_samples(int colorType, int depth, bool interlaced)
    {
        var builder = new PngBuilder { ColorType = colorType, BitDepth = depth, Interlaced = interlaced, Width = 11, Height = 9, Seed = colorType * 100 + depth };

        var image = PdfImage.FromBytes(builder.Build());

        AssertDecodedImage(builder, image);
    }

    [Fact]
    public void Extra_image_data_is_corrupt_and_not_inflated_further()
    {
        // 50 MB of zeros compress to a few kilobytes; decoding must stop at the first byte past the expected rows.
        var builder = new PngBuilder { ColorType = 6, RawTransform = raw => raw.Concat(new byte[50_000_000]).ToArray() };

        AssertCorrupt(builder.Build(), "more image data than its size allows");
    }

    [Theory]
    [InlineData(100_000_000, 1, false)]
    [InlineData(10_000, 10_000, true)]
    public void Header_promising_more_rows_than_the_image_data_can_hold_is_corrupt_before_allocating(int width, int height, bool interlaced)
    {
        // Within the default pixel limit, but 800 MB of 16-bit RGBA rows from a few bytes of image data.
        var png = Resized(new PngBuilder { ColorType = 6, BitDepth = 16, Interlaced = interlaced, Width = 1, Height = 1 }, width, height);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png));

        Assert.Equal(PdfImageErrorReason.CorruptData, exception.Reason);
        Assert.Contains("less image data than its size requires", exception.Message);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore < 16_000_000, "The load allocated buffers before checking the image data.");
    }

    [Fact]
    public void Interlaced_image_too_large_for_memory_is_rejected_before_allocating()
    {
        // A 50000 x 50000 header needs a 20 GB buffer.
        var png = Resized(new PngBuilder { ColorType = 6, BitDepth = 16, Interlaced = true, Width = 1, Height = 1 }, 50_000, 50_000);

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png, new PdfImageLoadOptions { MaxPixels = 3_000_000_000 }));

        Assert.Equal(PdfImageErrorReason.TooLarge, exception.Reason);
        Assert.Contains("too large to decode in memory", exception.Message);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(9, 1)]
    [InlineData(1, 9)]
    [InlineData(8, 8)]
    [InlineData(17, 13)]
    public void Interlaced_images_of_every_shape_decode(int width, int height)
    {
        var builder = new PngBuilder { ColorType = 6, Interlaced = true, Width = width, Height = height };

        AssertDecodedImage(builder, PdfImage.FromBytes(builder.Build()));
    }

    [Fact]
    public void Invalid_compressed_data_is_corrupt()
    {
        var builder = new PngBuilder { ColorType = 6, CompressedOverride = new byte[] { 0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x01 } };

        AssertCorrupt(builder.Build(), "damaged compressed image data");
    }

    [Fact]
    public void Invalid_filter_type_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { ColorType = 6, FilterOverride = 5 }.Build(), "invalid row filter type 5");
    }

    [Fact]
    public void Missing_image_data_rows_are_corrupt()
    {
        var builder = new PngBuilder { ColorType = 6, RawTransform = raw => raw[..^5] };

        AssertCorrupt(builder.Build(), "less image data than its size requires");
    }

    [Fact]
    public void Palette_index_outside_the_palette_is_corrupt()
    {
        var builder = new PngBuilder
        {
            ColorType = 3,
            Palette = new byte[] { 0, 0, 0, 255, 255, 255 },
            Transparency = new byte[] { 0 },
            SampleOverride = (x, y, c) => x == 3 ? 5 : 1
        };

        AssertCorrupt(builder.Build(), "palette index 5");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(8, true)]
    public void Palette_transparency_becomes_a_soft_mask(int depth, bool interlaced)
    {
        var alphas = new byte[] { 0, 128, 255, 64, 32 };
        var builder = new PngBuilder { ColorType = 3, BitDepth = depth, Interlaced = interlaced, Transparency = alphas[..Math.Min(alphas.Length, 1 << depth)] };

        var image = PdfImage.FromBytes(builder.Build());

        AssertDecodedImage(builder, image);
    }

    [Theory]
    [InlineData(6, 8, 600_000_000)]
    [InlineData(3, 1, int.MaxValue)]
    public void Rows_too_large_for_memory_are_too_large_when_the_limit_is_raised(int colorType, int depth, int width)
    {
        // A 2.4 GB filtered RGBA row, and a 1-bit palette row (with palette transparency, so it is decoded) whose
        // 8-bit converted row would exceed the largest array.
        var builder = new PngBuilder
        {
            ColorType = colorType,
            BitDepth = depth,
            Width = 1,
            Height = 1,
            Transparency = colorType == 3 ? new byte[] { 0 } : null
        };
        var png = Resized(builder, width, 1);

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png, new PdfImageLoadOptions { MaxPixels = 3_000_000_000 }));

        Assert.Equal(PdfImageErrorReason.TooLarge, exception.Reason);
        Assert.Contains("one row is too large to decode in memory", exception.Message);
    }

    #endregion

    #region Private Methods

    private static void AssertCorrupt(byte[] png, string expectedText)
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png));
        Assert.Equal(PdfImageErrorReason.CorruptData, exception.Reason);
        Assert.Contains(expectedText, exception.Message);
    }

    private static void AssertDecodedImage(PngBuilder builder, PdfImage image)
    {
        int colorType = builder.ColorType;
        int colorChannels = colorType is 2 or 6 ? 3 : 1;
        int width = builder.Width;
        int height = builder.Height;

        var expectedColor = new byte[width * height * colorChannels];
        var expectedAlpha = new byte[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            for (int c = 0; c < colorChannels; c++)
            {
                int sample = builder.Sample(x, y, c);
                expectedColor[(y * width + x) * colorChannels + c] = colorType == 3 ? (byte)sample : Scale(sample, builder.BitDepth);
            }

            expectedAlpha[y * width + x] = colorType switch
            {
                4 => Scale(builder.Sample(x, y, 1), builder.BitDepth),
                6 => Scale(builder.Sample(x, y, 3), builder.BitDepth),
                3 when builder.Transparency is { } alphas => builder.Sample(x, y, 0) < alphas.Length ? alphas[builder.Sample(x, y, 0)] : (byte)255,
                _ => 255
            };
        }

        string colorSpace = colorType switch
        {
            0 or 4 => "/DeviceGray",
            3 => $"[/Indexed /DeviceRGB {builder.Palette.Length / 3 - 1} <{Convert.ToHexString(builder.Palette)}>]",
            _ => "/DeviceRGB"
        };
        Assert.Equal(new[] { $"/Width {width}", $"/Height {height}", $"/ColorSpace {colorSpace}", "/BitsPerComponent 8", "/Filter /FlateDecode" },
            image.Data.DictionaryEntries);
        Assert.Equal(expectedColor, Inflate(image.Data.EncodedData));

        bool expectMask = colorType is 4 or 6 || (colorType == 3 && builder.Transparency is not null);
        if (expectMask)
        {
            Assert.NotNull(image.Data.SoftMask);
            Assert.Equal(new[] { $"/Width {width}", $"/Height {height}", "/ColorSpace /DeviceGray", "/BitsPerComponent 8", "/Filter /FlateDecode" },
                image.Data.SoftMask!.DictionaryEntries);
            Assert.Equal(expectedAlpha, Inflate(image.Data.SoftMask.EncodedData));
        }
        else
        {
            Assert.Null(image.Data.SoftMask);
        }
    }

    private static byte[] Inflate(byte[] zlib)
    {
        using var input = new ZLibStream(new MemoryStream(zlib), CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    // The 8-bit reduction the decoder applies: low depths stretch to 0..255, 16-bit samples keep their high byte.
    private static byte Scale(int sample, int depth) => depth switch
    {
        16 => (byte)(sample >> 8),
        8 => (byte)sample,
        _ => (byte)(sample * 255 / ((1 << depth) - 1))
    };

    // Builds the PNG, then patches its IHDR size and checksum so a tiny file declares a huge image.
    private static byte[] Resized(PngBuilder builder, int width, int height)
    {
        var png = builder.Build();
        WriteUInt32(png, 16, width);
        WriteUInt32(png, 20, height);
        WriteUInt32(png, 29, (int)Crc32.Compute(png, 12, 17));
        return png;
    }

    private static void WriteUInt32(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }

    #endregion

    #endregion
}
