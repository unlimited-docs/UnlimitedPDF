using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// Images the renderer must refuse rather than draw wrong, and the bounds that keep hostile images from exhausting
/// memory (26111701 AC-12, A06): JPEG data must match its dictionary, masks must be ones it draws exactly, and every
/// decode and encode is sized by the image before it runs.
/// </summary>
public class PageRendererImageBoundsTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("gray-baseline.jpg", "/DeviceCMYK", 0, "doesn't match")]
    [InlineData("gray-baseline.jpg", "/DeviceGray", 1, "doesn't match")]
    [InlineData("rgb-baseline.jpg", "/DeviceRGB", 2, "doesn't match")]
    [InlineData("rgb-baseline.jpg", "[/Separation /Spot /DeviceGray 9 0 R]", 0, "doesn't match")]
    public void Jpeg_data_that_does_not_match_its_dictionary_is_refused(string fixture, string space, int variant, string reason)
    {
        byte[] jpeg = File.ReadAllBytes(Fixtures.Jpeg(fixture));
        var image = PdfImage.FromBytes(jpeg);
        string size = variant == 2 ? "/Width 1 /Height 1" : $"/Width {image.PixelWidth} /Height {image.PixelHeight}";
        string decode = variant == 1 ? " /Decode [1 0]" : string.Empty;

        var page = DrawRawImage($"{size} /ColorSpace {space} /BitsPerComponent 8 /Filter /DCTDecode{decode}", jpeg);

        Assert.False(page.IsDrawn);
        Assert.Contains(reason, page.Reason);
    }

    [Fact]
    public void A_cmyk_jpeg_whose_inversion_the_dictionary_contradicts_is_refused()
    {
        byte[] jpeg = File.ReadAllBytes(Fixtures.Jpeg("cmyk-baseline.jpg"));
        var image = PdfImage.FromBytes(jpeg, new PdfImageLoadOptions { CmykInversion = PdfCmykInversion.NotInverted });
        var document = new PdfDocument();
        document.AddPage().AddImage(image, 10, 10, 50, 50);

        var page = RenderTestFiles.RenderFirst(Save(document));

        // The data carries an Adobe marker, so a browser would invert it; the PDF says not to. Not drawn, never wrong.
        Assert.False(page.IsDrawn);
        Assert.Contains("doesn't match", page.Reason);
    }

    [Theory]
    [InlineData("exif-orientation-6.jpg")]
    [InlineData("exif-orientation-3.jpg")]
    public void Exif_orientation_is_removed_so_the_browser_does_not_turn_the_image_again(string fixture)
    {
        byte[] jpeg = File.ReadAllBytes(Fixtures.Jpeg(fixture));
        var document = new PdfDocument();
        document.AddPage().AddImage(PdfImage.FromBytes(jpeg), 10, 10, 50, 50);

        var page = RenderTestFiles.RenderFirst(Save(document));
        byte[] drawn = Convert.FromBase64String(Regex.Match(page.Svg!, "data:image/jpeg;base64,([^\"]+)").Groups[1].Value);

        Assert.Contains("Exif", Encoding.Latin1.GetString(jpeg));
        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(drawn));
        Assert.Equal(PdfImage.FromBytes(jpeg, new PdfImageLoadOptions { ApplyExifOrientation = false }).PixelWidth,
            PdfImage.FromBytes(drawn).PixelWidth);
    }

    [Fact]
    public void A_masked_jpeg_is_refused()
    {
        byte[] jpeg = File.ReadAllBytes(Fixtures.Jpeg("rgb-baseline.jpg"));
        var image = PdfImage.FromBytes(jpeg);

        var page = DrawRawImage($"/Width {image.PixelWidth} /Height {image.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Mask [0 0 0 0 0 0]", jpeg);

        Assert.Contains("masked JPEG", page.Reason);
    }

    [Fact]
    public void A_small_stream_cannot_inflate_past_the_size_its_image_needs()
    {
        var watch = Stopwatch.StartNew();
        var page = DrawRawImage("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", Deflate(new byte[10_000_000]));

        Assert.False(page.IsDrawn);
        Assert.Contains("can't be read", page.Reason);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"Took {watch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void An_image_too_large_for_the_drawing_is_refused_before_it_is_encoded()
    {
        var random = new Random(7);
        var noise = new byte[600 * 600 * 3];
        random.NextBytes(noise);
        var limits = PageRenderLimits.Default with { MaxOutputCharacters = 100_000 };

        var watch = Stopwatch.StartNew();
        var page = DrawRawImage("/Width 600 /Height 600 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode", Deflate(noise), limits);

        Assert.Contains("drawing is too large", page.Reason);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"Took {watch.ElapsedMilliseconds} ms.");
    }

    [Theory]
    [InlineData("/Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", "another size")]
    [InlineData("/Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8", "soft mask in a color space")]
    [InlineData("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Matte [0 0 0]", "entries this preview")]
    [InlineData("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Decode [1 0]", "entries this preview")]
    public void Soft_masks_it_does_not_draw_exactly_are_refused(string mask, string reason)
    {
        var page = DrawRawImage("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /SMask 6 0 R", new byte[] { 9 },
            extra: (6, $"/Type /XObject /Subtype /Image {mask}", new byte[] { 255, 255, 255 }));

        Assert.False(page.IsDrawn);
        Assert.Contains(reason, page.Reason);
    }

    [Fact]
    public void Soft_mask_pixels_count_toward_the_pixel_bound()
    {
        var limits = PageRenderLimits.Default with { MaxImagePixels = 5 };
        var page = DrawRawImage("/Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /SMask 6 0 R", new byte[4],
            limits, (6, "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8", new byte[4]));

        Assert.Contains("too many image pixels", page.Reason);
    }

    [Fact]
    public void A_stencil_mask_stream_is_refused()
    {
        var page = DrawRawImage("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Mask 6 0 R", new byte[] { 9 },
            extra: (6, "/Type /XObject /Subtype /Image /ImageMask true /Width 1 /Height 1", new byte[] { 0 }));

        Assert.Contains("stencil-masked", page.Reason);
    }

    [Fact]
    public void Indexed_images_key_their_index_values_and_clamp_bad_indexes_and_short_tables()
    {
        // Indexes 0, 1, 2, 7: the key hides index 1; index 7 is past hival 2 and clamps to entry 2, which the
        // four-byte table only half holds, so its missing bytes read as 0.
        var page = DrawRawImage("/Width 4 /Height 1 /ColorSpace [/Indexed /DeviceRGB 2 <FF000000FF0000>] /BitsPerComponent 8 /Mask [1 1]",
            new byte[] { 0, 1, 2, 7 });

        var pixels = PngPixels(page.Svg!);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 255, 0, 0, 0, 0, 0, 255, 0, 0, 0, 255 }, pixels);
    }

    [Fact]
    public void Sixteen_bit_samples_keep_their_high_byte()
    {
        var page = DrawRawImage("/Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 16", new byte[] { 0x12, 0xFF, 0xAB, 0x01 });

        Assert.Equal(new byte[] { 0x12, 0xAB }, PngPixels(page.Svg!));
    }

    [Fact]
    public void A_jpeg_behind_another_filter_is_refused()
    {
        var page = DrawRawImage("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter [/FlateDecode /DCTDecode]", Deflate(new byte[] { 1 }));

        Assert.Contains("behind another filter", page.Reason);
    }

    #endregion

    #region Private Methods

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(data);
        return output.ToArray();
    }

    private static PageRenderResult DrawRawImage(string entries, byte[] data, PageRenderLimits? limits = null, (int Number, string Dictionary, byte[] Data)? extra = null)
    {
        var objects = new List<(int, string, byte[]?)> { (5, $"/Type /XObject /Subtype /Image {entries}", data) };
        if (extra is { } e) objects.Add((e.Number, e.Dictionary, e.Data));
        var pdf = RenderTestFiles.OnePage("q 10 0 0 10 0 0 cm /X1 Do Q", "<< /XObject << /X1 5 0 R >> >>", objects);
        return RenderTestFiles.RenderFirst(pdf, limits);
    }

    // The pixels of the drawing's PNG, after the per-row filter byte.
    private static byte[] PngPixels(string svg)
    {
        byte[] png = Convert.FromBase64String(Regex.Match(svg, "data:image/png;base64,([^\"]+)").Groups[1].Value);
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int channels = png[25] switch { 0 => 1, 2 => 3, _ => 4 };
        using var idat = new MemoryStream();
        for (int p = 8; p < png.Length;)
        {
            int length = (png[p] << 24) | (png[p + 1] << 16) | (png[p + 2] << 8) | png[p + 3];
            if (Encoding.ASCII.GetString(png, p + 4, 4) == "IDAT") idat.Write(png, p + 8, length);
            p += 12 + length;
        }
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(idat.ToArray()), CompressionMode.Decompress)) zlib.CopyTo(inflated);
        byte[] rows = inflated.ToArray();
        int stride = width * channels;
        return Enumerable.Range(0, rows.Length / (stride + 1)).SelectMany(r => rows.Skip(r * (stride + 1) + 1).Take(stride)).ToArray();
    }

    private static byte[] Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    #endregion

    #endregion
}
