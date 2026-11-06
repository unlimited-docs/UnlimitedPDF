using System.IO.Compression;
using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// PNG chunk parsing, pass-through embedding of PNGs that need no decoding, color-key transparency, resolution, ignored
/// color metadata, and damage detection (AC-01, AC-02, AC-07, AC-24, AC-25; D-038, D-039; A06).
/// </summary>
public class PdfImagePngTests
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Every PNG variant the specification allows: each color type, each allowed bit depth, interlaced or not.
    /// </summary>
    public static TheoryData<int, int, bool> AllVariants()
    {
        var data = new TheoryData<int, int, bool>();
        foreach (var colorType in new[] { 0, 2, 3, 4, 6 })
        foreach (var depth in PngBuilder.AllowedDepths(colorType))
        {
            data.Add(colorType, depth, false);
            data.Add(colorType, depth, true);
        }
        return data;
    }

    [Theory]
    [InlineData("gAMA", new byte[] { 0, 0, 0xB1, 0x8F })]
    [InlineData("cHRM", new byte[] { 0, 0, 0x7A, 0x26, 0, 0, 0x80, 0x84, 0, 0, 0xFA, 0, 0, 0, 0x80, 0xE8, 0, 0, 0x75, 0x30, 0, 0, 0xEA, 0x60, 0, 0, 0x3A, 0x98, 0, 0, 0x17, 0x70 })]
    [InlineData("sRGB", new byte[] { 0 })]
    [InlineData("iCCP", new byte[] { (byte)'p', 0, 0, 0xDE, 0xAD, 0xBE, 0xEF })] // Invalid zlib data: inflating it would fail.
    [InlineData("zTXt", new byte[] { (byte)'k', 0, 0, 0xFF, 0xFF, 0xFF })]
    [InlineData("iTXt", new byte[] { (byte)'k', 0, 1, 0, 0, 0, 0xFF, 0xFF })]
    [InlineData("tEXt", new byte[] { (byte)'k', 0, (byte)'v' })]
    public void Color_and_text_chunks_are_ignored(string type, byte[] data)
    {
        var plain = PdfImage.FromBytes(new PngBuilder().Build());
        var withChunk = PdfImage.FromBytes(new PngBuilder { ChunksBeforeImageData = { (type, data) } }.Build());

        Assert.Equal(plain.Data.DictionaryEntries, withChunk.Data.DictionaryEntries);
        Assert.Equal(plain.Data.EncodedData, withChunk.Data.EncodedData);
        Assert.Equal(plain.Data.ContentKey, withChunk.Data.ContentKey);
    }

    [Fact]
    public void Damaged_chunk_checksum_is_corrupt()
    {
        var png = new PngBuilder().Build();
        png[^20] ^= 0xFF; // Inside the last IDAT chunk's data.

        AssertCorrupt(png, "checksum");
    }

    [Theory]
    [InlineData(0, 2, new byte[] { 0x00, 0x03 }, "/Mask [3 3]")]
    [InlineData(0, 8, new byte[] { 0x00, 0x7F }, "/Mask [127 127]")]
    [InlineData(2, 8, new byte[] { 0x00, 0x0A, 0x00, 0x14, 0x00, 0x1E }, "/Mask [10 10 20 20 30 30]")]
    public void Gray_and_rgb_color_keys_become_a_mask_array(int colorType, int depth, byte[] key, string expected)
    {
        var builder = new PngBuilder { ColorType = colorType, BitDepth = depth, Transparency = key };

        var image = PdfImage.FromBytes(builder.Build());

        Assert.Contains(expected, image.Data.DictionaryEntries);
        Assert.Equal(builder.CompressedImageData, image.Data.EncodedData);
        Assert.Null(image.Data.SoftMask);
    }

    [Fact]
    public void Invalid_chunk_type_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { ChunksBeforeImageData = { ("ab1d", new byte[1]) } }.Build(), "invalid chunk type");
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(3, 16)]
    [InlineData(4, 4)]
    [InlineData(0, 3)]
    public void Invalid_color_type_and_depth_combination_is_corrupt(int colorType, int depth)
    {
        AssertCorrupt(new PngBuilder { ColorType = colorType, BitDepth = depth }.Build(), "invalid combination");
    }

    [Fact]
    public void Missing_end_chunk_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { IncludeEnd = false }.Build(), "IEND");
    }

    [Fact]
    public void Missing_header_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { IncludeHeader = false }.Build(), "does not start with an IHDR");
    }

    [Fact]
    public void Missing_image_data_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { IncludeImageData = false }.Build(), "no image data");
    }

    [Fact]
    public void Opaque_palette_transparency_still_passes_through()
    {
        var builder = new PngBuilder { ColorType = 3, BitDepth = 4, Transparency = Enumerable.Repeat((byte)255, 16).ToArray() };

        var image = PdfImage.FromBytes(builder.Build());

        Assert.Equal(builder.CompressedImageData, image.Data.EncodedData);
        Assert.DoesNotContain(image.Data.DictionaryEntries, e => e.StartsWith("/Mask", StringComparison.Ordinal));
    }

    [Fact]
    public void Palette_image_without_palette_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { ColorType = 3, IncludePalette = false }.Build(), "without a PLTE");
    }

    [Fact]
    public void Palette_in_grayscale_image_is_corrupt()
    {
        AssertCorrupt(new PngBuilder { ColorType = 0, ChunksBeforeImageData = { ("PLTE", new byte[6]) } }.Build(), "PLTE chunk in a grayscale");
    }

    [Theory]
    [InlineData(0, 10_000, 10_000)]
    [InlineData(2, 100_000_000, 1)]
    public void Pass_through_header_larger_than_its_image_data_is_corrupt(int colorType, int width, int height)
    {
        // The image data of a 1 x 1 image under a header that declares 100,000,000 pixels. Pass-through never
        // inflates the data, so the size check is what makes this fail at load.
        var png = new PngBuilder { ColorType = colorType, Width = 1, Height = 1, HeaderSize = (width, height) }.Build();

        AssertCorrupt(png, "less image data than its size requires");
    }

    [Fact]
    public void Pass_through_image_near_the_compression_bound_still_loads()
    {
        // 10,000,100 bytes of zero rows compress more than 1,000 times, close to the deflate limit the check uses.
        const int width = 100_000;
        const int height = 100;
        var raw = new byte[(width + 1) * height];
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw);
        var compressed = buffer.ToArray();

        var image = PdfImage.FromBytes(new PngBuilder { ColorType = 0, Width = width, Height = height, CompressedOverride = compressed }.Build());

        Assert.True(raw.Length > compressed.Length * 1_000L, $"Compression ratio is only {raw.Length / compressed.Length}.");
        Assert.Equal(compressed, image.Data.EncodedData);
    }

    public static TheoryData<int, int> PassThroughVariants() => new()
    {
        { 0, 1 }, { 0, 2 }, { 0, 4 }, { 0, 8 }, { 2, 8 }, { 3, 1 }, { 3, 2 }, { 3, 4 }, { 3, 8 }
    };

    [Theory]
    [MemberData(nameof(PassThroughVariants))]
    public void Pass_through_variants_embed_the_original_compressed_data(int colorType, int depth)
    {
        var builder = new PngBuilder { ColorType = colorType, BitDepth = depth, Width = 13, Height = 6 };

        var image = PdfImage.FromBytes(builder.Build());

        var colors = colorType == 2 ? 3 : 1;
        var colorSpace = colorType switch
        {
            0 => "/DeviceGray",
            2 => "/DeviceRGB",
            _ => $"[/Indexed /DeviceRGB 15 <{Convert.ToHexString(builder.Palette)}>]"
        };
        Assert.Equal(new[]
        {
            "/Width 13",
            "/Height 6",
            $"/ColorSpace {colorSpace}",
            $"/BitsPerComponent {depth}",
            "/Filter /FlateDecode",
            $"/DecodeParms << /Predictor 15 /Colors {colors} /BitsPerComponent {depth} /Columns 13 >>"
        }, image.Data.DictionaryEntries);
        Assert.Equal(builder.CompressedImageData, image.Data.EncodedData);
        Assert.Equal(13, image.PixelWidth);
        Assert.Equal(6, image.PixelHeight);
    }

    [Fact]
    public void Pixel_limit_is_checked_right_after_the_header()
    {
        // The file also lacks image data; the limit is reported first because it is checked before reading further.
        var png = new PngBuilder { Width = 20, Height = 20, IncludeImageData = false }.Build();

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png, new PdfImageLoadOptions { MaxPixels = 399 }));

        Assert.Equal(PdfImageErrorReason.TooLarge, exception.Reason);
    }

    [Theory]
    [InlineData(11811, 1, 299.9994)]
    [InlineData(3937, 1, 99.9998)]
    [InlineData(11811, 0, null)]
    public void Reports_phys_resolution(int pixelsPerMeter, byte unit, double? expectedDpi)
    {
        var image = PdfImage.FromBytes(new PngBuilder { PixelsPerMeter = (pixelsPerMeter, pixelsPerMeter, unit) }.Build());

        if (expectedDpi is null)
        {
            Assert.Null(image.DpiX);
        }
        else
        {
            Assert.Equal(expectedDpi.Value, image.DpiX!.Value, 4);
            Assert.Equal(expectedDpi.Value, image.DpiY!.Value, 4);
        }
    }

    [Theory]
    [InlineData(0, new byte[] { 0, 1, 2 })]
    [InlineData(2, new byte[] { 0, 1 })]
    [InlineData(6, new byte[] { 0, 1 })]
    public void Transparency_chunk_not_matching_color_type_is_corrupt(int colorType, byte[] transparency)
    {
        AssertCorrupt(new PngBuilder { ColorType = colorType, Transparency = transparency }.Build(), "tRNS");
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(60)]
    public void Truncated_file_is_corrupt(int keepBytes)
    {
        var png = new PngBuilder().Build();

        Assert.Equal(PdfImageErrorReason.CorruptData, Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png[..keepBytes])).Reason);
    }

    [Fact]
    public void Unknown_critical_chunk_is_unsupported()
    {
        var png = new PngBuilder { ChunksBeforeImageData = { ("ABCD", new byte[2]) } }.Build();

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png));

        Assert.Equal(PdfImageErrorReason.UnsupportedFormat, exception.Reason);
        Assert.Contains("'ABCD'", exception.Message);
    }

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void Variant_matrix_builds_valid_png_files(int colorType, int depth, bool interlaced)
    {
        // Every AC-02 variant is generated; pass-through variants load now, and the rest are decoded in T-07.
        var builder = new PngBuilder { ColorType = colorType, BitDepth = depth, Interlaced = interlaced, Width = 11, Height = 9 };
        var png = builder.Build();

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        bool passThrough = !interlaced && depth <= 8 && colorType is 0 or 2 or 3;
        if (passThrough)
            Assert.Equal(builder.CompressedImageData, PdfImage.FromBytes(png).Data.EncodedData);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void Zero_dimension_is_corrupt(int width, int height)
    {
        AssertCorrupt(new PngBuilder { Width = width, Height = height }.Build(), "zero width or height");
    }

    #endregion

    #region Private Methods

    private static void AssertCorrupt(byte[] png, string expectedText)
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(png));
        Assert.Equal(PdfImageErrorReason.CorruptData, exception.Reason);
        Assert.StartsWith("The PNG image", exception.Message);
        Assert.Contains(expectedText, exception.Message);
    }

    #endregion

    #endregion
}
