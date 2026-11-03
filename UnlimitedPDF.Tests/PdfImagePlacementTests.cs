using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Placing images on pages: position and size rules, fit modes, EXIF orientation transforms, drawing order, storing
/// each image once per document, argument validation, and invariant numbers (AC-04 to AC-06, AC-09, AC-15, AC-23,
/// AC-25, AC-27; D-008, D-010, D-025, D-027, D-034, D-036; FA-12, FA-13).
/// </summary>
public class PdfImagePlacementTests
{
    #region Fields and Constants

    private static readonly Regex DrawPattern = new(@"q (\S+) (\S+) (\S+) (\S+) (\S+) (\S+) cm (/Im\d+) Do Q", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void AddImage_carries_version_metadata()
    {
        var method = typeof(PdfPageBuilder).GetMethod(nameof(PdfPageBuilder.AddImage))!;

        Assert.Equal("0.1.0", method.GetCustomAttribute<AddedVersionAttribute>()?.Version);
        Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
    }

    [Fact]
    public void Contain_centers_horizontally_for_tall_images()
    {
        var draw = SingleDraw(page => page.AddImage(Jpeg(80, 200), 0, 0, 100, 100, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain }));

        Assert.Equal(new[] { 40d, 0, 0, 100, 30, 0 }, draw.Matrix);
    }

    [Fact]
    public void Contain_fits_inside_the_box_and_centers()
    {
        // AC-04: a 200 x 80 px image in a 100 x 100 box at (50, 700) draws 100 x 40 with its bottom edge at 730.
        var draw = SingleDraw(page => page.AddImage(Jpeg(200, 80), 50, 700, 100, 100, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain }));

        Assert.Equal(new[] { 100d, 0, 0, 40, 50, 730 }, draw.Matrix);
    }

    [Fact]
    public void Content_is_drawn_in_the_order_it_was_added()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("first", 10, 10);
        page.AddImage(Jpeg(10, 10), 20, 20);
        var table = page.AddTable(10, 300, 100);
        table.GetOrCreateCell(0, 0).Text = "third";
        page.AddImage(Jpeg(10, 10), 40, 40);

        var content = Encoding.ASCII.GetString(page.BuildContent().Data);

        int text = content.IndexOf("(first) Tj", StringComparison.Ordinal);
        int firstImage = content.IndexOf(" Do Q", StringComparison.Ordinal);
        int tableText = content.IndexOf("(third) Tj", StringComparison.Ordinal);
        int secondImage = content.LastIndexOf(" Do Q", StringComparison.Ordinal);
        Assert.True(text < firstImage && firstImage < tableText && tableText < secondImage, content);
    }

    [Fact]
    public void Default_size_is_96_pixels_per_inch()
    {
        // AC-04: a 300 x 100 px logo renders 225 x 75 pt; file DPI is ignored.
        var logo = PdfImage.FromBytes(new JpegBuilder { Width = 300, Height = 100, Jfif = (1, 300, 300) }.Build());

        var draw = SingleDraw(page => page.AddImage(logo, 50, 700));

        Assert.Equal(new[] { 225d, 0, 0, 75, 50, 700 }, draw.Matrix);
    }

    [Fact]
    public void Documents_built_in_parallel_share_one_image_safely()
    {
        var logo = PdfImage.FromBytes(new PngBuilder { ColorType = 6, Width = 16, Height = 8 }.Build());
        var outputs = new byte[16][];

        Parallel.For(0, outputs.Length, i =>
        {
            var document = new PdfDocument();
            for (int p = 0; p < 3; p++)
                document.AddPage().AddImage(logo, 10, 10);
            outputs[i] = Save(document);
        });

        Assert.All(outputs, output => Assert.Equal(outputs[0], output));
        PdfStructureChecker.AssertValid(outputs[0]);
        Assert.Equal(2, CountImageObjects(outputs[0])); // The image and its transparency mask.
    }

    [Fact]
    public void Identical_images_loaded_separately_are_stored_once()
    {
        var bytes = new JpegBuilder { Width = 40, Height = 30 }.Build();
        var document = new PdfDocument();
        document.AddPage().AddImage(PdfImage.FromBytes(bytes), 0, 0);
        document.AddPage().AddImage(PdfImage.FromBytes(bytes.ToArray()), 0, 0);
        document.AddPage().AddImage(PdfImage.FromBytes(new JpegBuilder { Width = 41, Height = 30 }.Build()), 0, 0);

        var pdf = Save(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(2, CountImageObjects(pdf));
    }

    [Fact]
    public void Image_pages_reference_images_and_other_pages_keep_empty_resources()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("no images", 10, 10);
        var page = document.AddPage();
        page.AddImage(Jpeg(10, 10), 0, 0);
        page.AddImage(Jpeg(20, 10), 0, 0);

        var text = Encoding.Latin1.GetString(Save(document));

        Assert.Contains($"/Resources <<{Environment.NewLine}>>", text);
        Assert.Matches(@"/XObject << /Im1 \d+ 0 R /Im2 \d+ 0 R >>", text);
    }

    [Fact]
    public void Images_added_after_a_save_and_pages_added_later_keep_the_file_valid()
    {
        var logo = Jpeg(10, 10);
        var document = new PdfDocument();
        document.AddPage().AddImage(logo, 0, 0);
        var first = Save(document);

        document.AddPage().AddImage(Jpeg(12, 12), 5, 5);
        document.AddPage().AddImage(logo, 5, 5);
        var second = Save(document);

        PdfStructureChecker.AssertValid(first);
        PdfStructureChecker.AssertValid(second);
        Assert.Equal(1, CountImageObjects(first));
        Assert.Equal(2, CountImageObjects(second));
    }

    [Fact]
    public void Images_with_identical_masks_share_one_mask_object()
    {
        // Same transparency, different colors: two images, one stored mask.
        Func<int, int, int, int> Rgba(int red) => (x, y, c) => c switch { 0 => red, 3 => (x + y) % 2 == 0 ? 255 : 0, _ => 0 };
        var first = PdfImage.FromBytes(new PngBuilder { ColorType = 6, SampleOverride = Rgba(200) }.Build());
        var second = PdfImage.FromBytes(new PngBuilder { ColorType = 6, SampleOverride = Rgba(50) }.Build());
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddImage(first, 0, 0);
        page.AddImage(second, 50, 0);

        var pdf = Save(document);
        var masks = Regex.Matches(Encoding.Latin1.GetString(pdf), @"/SMask (\d+) 0 R").Select(m => m.Groups[1].Value).ToList();

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(3, CountImageObjects(pdf));
        Assert.Equal(2, masks.Count);
        Assert.Single(masks.Distinct());
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_sizes_throw_with_parameter_name(double size)
    {
        var page = new PdfDocument().AddPage();
        var image = Jpeg(10, 10);

        Assert.Equal("width", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddImage(image, 0, 0, size, 10)).ParamName);
        Assert.Equal("height", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddImage(image, 0, 0, 10, size)).ParamName);
        Assert.Equal("width", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddImage(image, 0, 0, width: size)).ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_coordinates_throw_with_parameter_name(double coordinate)
    {
        var page = new PdfDocument().AddPage();
        var image = Jpeg(10, 10);

        Assert.Equal("x", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddImage(image, coordinate, 0)).ParamName);
        Assert.Equal("y", Assert.Throws<ArgumentOutOfRangeException>(() => page.AddImage(image, 0, coordinate)).ParamName);
    }

    [Fact]
    public void Null_image_throws()
    {
        Assert.Equal("image", Assert.Throws<ArgumentNullException>(() => new PdfDocument().AddPage().AddImage(null!, 0, 0)).ParamName);
    }

    [Fact]
    public void Numbers_are_invariant_under_comma_decimal_cultures()
    {
        using var culture = new CultureScope("de-DE");
        var document = new PdfDocument();
        document.AddPage().AddImage(Jpeg(3, 7), 10.5, 20.25, 33.3);

        var pdf = Save(document);
        var draw = DrawPattern.Match(Encoding.Latin1.GetString(pdf));

        Assert.True(draw.Success);
        Assert.DoesNotContain(",", draw.Value);
        Assert.Equal("q 33.3 0 0 77.7 10.5 20.25 cm /Im1 Do Q", draw.Value);
        PdfStructureChecker.AssertValid(pdf);
    }

    [Fact]
    public void Off_page_coordinates_are_accepted()
    {
        var document = new PdfDocument();
        document.AddPage().AddImage(Jpeg(10, 10), -500, 2000);

        PdfStructureChecker.AssertValid(Save(document));
    }

    [Fact]
    public void One_dimension_keeps_the_aspect_ratio()
    {
        var image = Jpeg(200, 80);

        Assert.Equal(new[] { 100d, 0, 0, 40, 0, 0 }, SingleDraw(page => page.AddImage(image, 0, 0, width: 100)).Matrix);
        Assert.Equal(new[] { 100d, 0, 0, 40, 0, 0 }, SingleDraw(page => page.AddImage(image, 0, 0, height: 40)).Matrix);
    }

    [Theory]
    [InlineData(1, "TL", "TR")]
    [InlineData(2, "TR", "TL")]
    [InlineData(3, "BR", "BL")]
    [InlineData(4, "BL", "BR")]
    [InlineData(5, "TL", "BL")]
    [InlineData(6, "TR", "BR")]
    [InlineData(7, "BR", "TR")]
    [InlineData(8, "BL", "TL")]
    public void Orientation_places_stored_corners_where_exif_defines(ushort orientation, string storedTopLeft, string storedTopRight)
    {
        // The stored image's top-left and top-right pixels must land on these displayed corners for each EXIF value.
        var photo = PdfImage.FromBytes(new JpegBuilder { Width = 400, Height = 300, ExtraSegments = { JpegBuilder.ExifSegment(orientation) } }.Build());
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddImage(photo, 10, 20);

        var draw = ParseDraws(page.BuildContent().Data).Single();
        bool swapped = orientation >= 5;
        double w = swapped ? 225 : 300, h = swapped ? 300 : 225;

        Assert.Equal(Corner(storedTopLeft, 10, 20, w, h), Apply(draw.Matrix, 0, 1));
        Assert.Equal(Corner(storedTopRight, 10, 20, w, h), Apply(draw.Matrix, 1, 1));

        var pdf = Save(document);
        PdfStructureChecker.AssertValid(pdf);
        Assert.True(IndexOf(pdf, photo.Data.EncodedData) > 0, "The JPEG bytes must be embedded unchanged.");
    }

    [Fact]
    public void Repeated_saves_reuse_stored_images_and_produce_identical_output()
    {
        var document = new PdfDocument();
        document.AddPage().AddImage(PdfImage.FromBytes(new PngBuilder { ColorType = 6 }.Build()), 0, 0);

        var first = Save(document);
        var second = Save(document);

        Assert.Equal(first, second);
        Assert.Equal(2, CountImageObjects(second));
    }

    [Fact]
    public void Same_image_placed_on_many_pages_is_stored_once()
    {
        // AC-05: one logo placed twice on each of three pages is stored once and referenced by every placement.
        var logo = PdfImage.FromBytes(new PngBuilder { ColorType = 6, Width = 20, Height = 10 }.Build());
        var document = new PdfDocument();
        for (int p = 0; p < 3; p++)
        {
            var page = document.AddPage();
            page.AddImage(logo, 10, 10);
            page.AddImage(logo, 100, 100, 50, 25);
        }

        var pdf = Save(document);
        var text = Encoding.Latin1.GetString(pdf);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(2, CountImageObjects(pdf));
        Assert.Equal(6, DrawPattern.Matches(text).Count);
        Assert.Equal(3, Regex.Matches(text, @"/XObject << /Im1 (\d+) 0 R >>").Select(m => m.Groups[1].Value).Count());
        Assert.Single(Regex.Matches(text, @"/XObject << /Im1 (\d+) 0 R >>").Select(m => m.Groups[1].Value).Distinct());
        Assert.Matches(@"/SMask \d+ 0 R", text);
    }

    [Fact]
    public void Stretch_fills_the_box_by_default()
    {
        var draw = SingleDraw(page => page.AddImage(Jpeg(200, 80), 50, 700, 100, 100));

        Assert.Equal(new[] { 100d, 0, 0, 100, 50, 700 }, draw.Matrix);
    }

    [Fact]
    public void Table_document_with_images_saves_valid_pdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddImage(Jpeg(30, 30), 400, 760);
        var table = page.AddTable(50, 700, 300);
        table.SetColumnWidths(100, 200);
        table.GetOrCreateCell(0, 0).Text = "Item";
        table.GetOrCreateCell(0, 1).Background = PdfColor.LightGray;

        PdfStructureChecker.AssertValid(Save(document));
    }

    #endregion

    #region Private Methods

    private static (double X, double Y) Apply(double[] m, double u, double v) =>
        (Math.Round(m[0] * u + m[2] * v + m[4], 6), Math.Round(m[1] * u + m[3] * v + m[5], 6));

    private static (double X, double Y) Corner(string corner, double x, double y, double w, double h) => corner switch
    {
        "TL" => (x, y + h),
        "TR" => (x + w, y + h),
        "BL" => (x, y),
        _ => (x + w, y)
    };

    private static int CountImageObjects(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Subtype /Image\b").Count;

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        }
        return -1;
    }

    private static PdfImage Jpeg(int width, int height) => PdfImage.FromBytes(new JpegBuilder { Width = width, Height = height }.Build());

    private static List<(double[] Matrix, string Name)> ParseDraws(byte[] content) =>
        DrawPattern.Matches(Encoding.ASCII.GetString(content))
            .Select(m => (Enumerable.Range(1, 6).Select(i => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray(), m.Groups[7].Value))
            .ToList();

    private static byte[] Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static (double[] Matrix, string Name) SingleDraw(Action<PdfPageBuilder> place)
    {
        var page = new PdfDocument().AddPage();
        place(page);
        return ParseDraws(page.BuildContent().Data).Single();
    }

    #endregion

    #endregion
}
