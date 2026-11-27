using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// The page renderer draws every kind of image the library writes, with its placement, pixels, and transparency, and
/// bounds the image pixels on a page (26111701 AC-12, A06).
/// </summary>
public class PageRendererImageTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [MemberData(nameof(PngKinds))]
    public void Png_images_come_out_with_their_pixels(int colorType, int depth, bool keyed)
    {
        byte[]? key = keyed ? KeyFor(colorType, depth) : null;
        var builder = new PngBuilder { ColorType = colorType, BitDepth = depth, Width = 9, Height = 7, Seed = colorType * 31 + depth, Transparency = key };
        var page = RenderImage(PdfImage.FromBytes(builder.Build()));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(1, page.Counts.Images);
        var (width, height, channels, pixels) = DecodePng(DataUri(page.Svg!, "image/png"));
        Assert.Equal((9, 7), (width, height));

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var expected = ExpectedPixel(builder, x, y, key);
            var actual = Enumerable.Range(0, channels).Select(c => (int)pixels[(y * width + x) * channels + c]).ToArray();
            if (channels == 4 && expected.Length == 4 && expected[3] == 0)
                Assert.Equal(0, actual[3]); // fully transparent: the color doesn't matter
            else
                Assert.Equal(Widen(expected, channels), actual);
        }
    }

    [Fact]
    public void Jpeg_images_are_passed_through_unchanged()
    {
        byte[] jpeg = File.ReadAllBytes(Fixtures.Jpeg("cmyk-baseline.jpg"));
        var page = RenderImage(PdfImage.FromBytes(jpeg));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(jpeg, DataUri(page.Svg!, "image/jpeg"));
    }

    [Fact]
    public void An_image_is_placed_in_its_unit_square_with_the_first_row_on_top()
    {
        var page = RenderImage(PdfImage.FromBytes(new PngBuilder { Width = 4, Height = 2 }.Build()), x: 100, y: 200, width: 40, height: 20);

        // The library places the image's bottom-left at (100, 200) on an A4 page 841.89 points high, so its top edge
        // is 841.89 - 220 = 621.89 from the top.
        Assert.Equal("<use href=\"#t-i0\" transform=\"matrix(40 0 0 20 100 621.89)\"/>", Regex.Match(page.Svg!, "<use [^>]+>").Value);
    }

    [Theory]
    [InlineData("exif-orientation-6.jpg")]
    [InlineData("exif-orientation-3.jpg")]
    public void Turned_jpeg_images_keep_the_matrix_the_library_wrote(string fixture)
    {
        byte[] pdf = OnePageWith(PdfImage.FromBytes(File.ReadAllBytes(Fixtures.Jpeg(fixture))), 50, 50, 100, 100);
        var page = RenderTestFiles.RenderFirst(pdf);
        string cm = Regex.Match(PdfText.Of(pdf), @"q\s+([-\d. ]+) cm\s+/Im\d+ Do").Groups[1].Value;
        var m = cm.Split(' ').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();

        // The drawing is the unit square, flipped to put row one on top, then the PDF's matrix, then the page flip.
        var expected = new RenderMatrix(1, 0, 0, -1, 0, 1).Then(new RenderMatrix(m[0], m[1], m[2], m[3], m[4], m[5])).Then(new RenderMatrix(1, 0, 0, -1, 0, page.Height));
        Assert.Equal($"transform=\"{Matrix(expected)}\"", Regex.Match(page.Svg!, "<use [^>]*(transform=\"[^\"]+\")").Groups[1].Value);
    }

    [Fact]
    public void A_repeated_image_is_stored_once()
    {
        var image = PdfImage.FromBytes(new PngBuilder().Build());
        var document = new PdfDocument();
        var pdfPage = document.AddPage();
        pdfPage.AddImage(image, 10, 10, 20, 20);
        pdfPage.AddImage(image, 50, 50, 20, 20);

        var page = RenderTestFiles.RenderFirst(Save(document));

        Assert.Equal(2, page.Counts.Images);
        Assert.Single(Regex.Matches(page.Svg!, "<image "));
        Assert.Equal(2, Regex.Matches(page.Svg!, "<use href=\"#t-i0\"").Count);
    }

    [Fact]
    public void Images_past_the_pixel_bound_leave_the_page_undrawn()
    {
        var page = RenderImage(PdfImage.FromBytes(new PngBuilder { Width = 30, Height = 30 }.Build()),
            limits: PageRenderLimits.Default with { MaxImagePixels = 899 });

        Assert.False(page.IsDrawn);
        Assert.Contains("too many image pixels", page.Reason);
    }

    [Theory]
    [InlineData("/ImageMask true /Width 1 /Height 1", "stencil mask")]
    [InlineData("/Width 1 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceCMYK", "color space")]
    [InlineData("/Width 1 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray /Decode [1 0]", "decode array")]
    [InlineData("/Width 4 /Height 4 /BitsPerComponent 8 /ColorSpace /DeviceGray", "too little data")]
    [InlineData("/Width 1 /Height 1 /BitsPerComponent 3 /ColorSpace /DeviceGray", "sample size")]
    [InlineData("/Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray", "/Width")]
    public void Images_it_does_not_draw_leave_the_page_undrawn(string entries, string reason)
    {
        var pdf = RenderTestFiles.OnePage("q 10 0 0 10 0 0 cm /X1 Do Q", "<< /XObject << /X1 5 0 R >> >>",
            new[] { (5, $"/Type /XObject /Subtype /Image {entries}", (byte[]?)new byte[] { 7 }) });

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.False(page.IsDrawn);
        Assert.Contains(reason, page.Reason);
    }

    [Fact]
    public void A_missing_or_postscript_xobject_leaves_the_page_undrawn()
    {
        Assert.Contains("doesn't have", RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("/X9 Do")).Reason);
        var pdf = RenderTestFiles.OnePage("/X1 Do", "<< /XObject << /X1 5 0 R >> >>", new[] { (5, "/Type /XObject /Subtype /PS", (byte[]?)new byte[] { 1 }) });
        Assert.Contains("type PS", RenderTestFiles.RenderFirst(pdf).Reason);
    }

    #endregion

    #region Private Methods

    private static byte[] DataUri(string svg, string mediaType)
    {
        var match = Regex.Match(svg, $"href=\"data:{Regex.Escape(mediaType)};base64,([^\"]+)\"");
        Assert.True(match.Success, $"No {mediaType} data URI in the drawing.");
        return Convert.FromBase64String(match.Groups[1].Value);
    }

    private static (int Width, int Height, int Channels, byte[] Pixels) DecodePng(byte[] png)
    {
        int width = 0, height = 0, channels = 0;
        using var idat = new MemoryStream();
        for (int p = 8; p < png.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p));
            string type = Encoding.ASCII.GetString(png, p + 4, 4);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p + 8));
                height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p + 12));
                Assert.Equal(8, png[p + 16]);
                channels = png[p + 17] switch { 0 => 1, 2 => 3, 6 => 4, _ => throw new InvalidDataException() };
            }
            if (type == "IDAT") idat.Write(png, p + 8, length);
            p += 12 + length;
        }
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(idat.ToArray()), CompressionMode.Decompress)) zlib.CopyTo(inflated);
        byte[] rows = inflated.ToArray();
        var pixels = new byte[width * height * channels];
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(0, rows[y * (width * channels + 1)]);
            Array.Copy(rows, y * (width * channels + 1) + 1, pixels, y * width * channels, width * channels);
        }
        return (width, height, channels, pixels);
    }

    // The expected 8-bit pixel: gray or RGB, plus alpha when the PNG has transparency.
    private static int[] ExpectedPixel(PngBuilder builder, int x, int y, byte[]? key)
    {
        int max = (1 << builder.BitDepth) - 1;
        int To8(int v) => builder.BitDepth == 16 ? v >> 8 : builder.BitDepth == 8 ? v : v * 255 / max;
        int[] color;
        bool transparentByKey = false;
        switch (builder.ColorType)
        {
            case 3:
                int index = builder.Sample(x, y, 0);
                color = new int[] { builder.Palette[index * 3], builder.Palette[index * 3 + 1], builder.Palette[index * 3 + 2] };
                if (key is not null) return color.Append(index < key.Length ? key[index] : 255).ToArray();
                return color;
            case 0 or 4:
                color = new[] { To8(builder.Sample(x, y, 0)) };
                if (key is not null) transparentByKey = builder.Sample(x, y, 0) == BinaryPrimitives.ReadUInt16BigEndian(key);
                break;
            default:
                color = Enumerable.Range(0, 3).Select(c => To8(builder.Sample(x, y, c))).ToArray();
                if (key is not null) transparentByKey = Enumerable.Range(0, 3).All(c => builder.Sample(x, y, c) == BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(c * 2)));
                break;
        }
        if (builder.ColorType is 4 or 6) return color.Append(To8(builder.Sample(x, y, builder.Channels - 1))).ToArray();
        if (key is not null) return color.Append(transparentByKey ? 0 : 255).ToArray();
        return color;
    }

    // A tRNS key that matches the first pixel, so at least one pixel is keyed out.
    private static byte[] KeyFor(int colorType, int depth)
    {
        var probe = new PngBuilder { ColorType = colorType, BitDepth = depth, Width = 9, Height = 7, Seed = colorType * 31 + depth };
        return colorType switch
        {
            0 => new[] { (byte)(probe.Sample(0, 0, 0) >> 8), (byte)probe.Sample(0, 0, 0) },
            2 => Enumerable.Range(0, 3).SelectMany(c => new[] { (byte)(probe.Sample(0, 0, c) >> 8), (byte)probe.Sample(0, 0, c) }).ToArray(),
            _ => new byte[] { 0, 128, 255 },
        };
    }

    private static string Matrix(RenderMatrix m) =>
        $"matrix({SvgText.Number(m.A)} {SvgText.Number(m.B)} {SvgText.Number(m.C)} {SvgText.Number(m.D)} {SvgText.Number(m.E)} {SvgText.Number(m.F)})";

    private static byte[] OnePageWith(PdfImage image, double x, double y, double? width, double? height)
    {
        var document = new PdfDocument();
        document.AddPage().AddImage(image, x, y, width, height);
        return Save(document);
    }

    private static PageRenderResult RenderImage(PdfImage image, double x = 10, double y = 10, double? width = 90, double? height = 70, PageRenderLimits? limits = null) =>
        RenderTestFiles.RenderFirst(OnePageWith(image, x, y, width, height), limits);

    private static byte[] Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    // Gray expected values compared with an RGBA drawing repeat the gray in each color channel.
    private static int[] Widen(int[] expected, int channels) =>
        channels == 4 && expected.Length == 2 ? new[] { expected[0], expected[0], expected[0], expected[1] } : expected;

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<int, int, bool> PngKinds
    {
        get
        {
            var data = new TheoryData<int, int, bool>();
            foreach (int colorType in new[] { 0, 2, 3, 4, 6 })
            foreach (int depth in PngBuilder.AllowedDepths(colorType))
            {
                data.Add(colorType, depth, false);
                if (colorType is 0 or 2 or 3) data.Add(colorType, depth, true);
            }
            return data;
        }
    }

    #endregion
}
