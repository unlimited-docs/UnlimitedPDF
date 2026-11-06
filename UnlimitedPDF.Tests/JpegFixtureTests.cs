using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Real encoded JPEG fixtures (Fixtures/Jpeg, PR-011) load with the expected color space, size, resolution, CMYK
/// handling, and EXIF orientation, and embed unchanged (AC-01, AC-02, AC-23; D-019, D-037, D-050, D-051; FA-14).
/// </summary>
public class JpegFixtureTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void All_fixtures_embed_in_one_valid_pdf()
    {
        var names = new[] { "rgb-baseline.jpg", "rgb-300dpi.jpg", "gray-baseline.jpg", "cmyk-baseline.jpg" }
            .Concat(Enumerable.Range(1, 8).Select(o => $"exif-orientation-{o}.jpg"))
            .ToArray();
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = page.AddTable(40, 800, 500);
        table.RowHeightMode = PdfRowHeightMode.AtLeast;
        for (int i = 0; i < names.Length; i++)
        {
            var image = PdfImage.FromFile(Fixtures.Jpeg(names[i]));
            page.AddImage(image, 40 + (i % 6) * 90, 300 - (i / 6) * 90, 72);
            table.GetOrCreateCell(i / 4, i % 4).SetContent(image, names[i]);
        }

        using var stream = new MemoryStream();
        document.Save(stream);

        PdfStructureChecker.AssertValid(stream.ToArray());
    }

    [Theory]
    [InlineData(PdfCmykInversion.Auto, true)]
    [InlineData(PdfCmykInversion.NotInverted, false)]
    [InlineData(PdfCmykInversion.Inverted, true)]
    public void Cmyk_fixture_has_an_adobe_marker_and_follows_the_override(PdfCmykInversion inversion, bool expectDecode)
    {
        var image = PdfImage.FromFile(Fixtures.Jpeg("cmyk-baseline.jpg"), new PdfImageLoadOptions { CmykInversion = inversion });

        Assert.Contains("/ColorSpace /DeviceCMYK", image.Data.DictionaryEntries);
        Assert.Equal(expectDecode, image.Data.DictionaryEntries.Contains("/Decode [1 0 1 0 1 0 1 0]"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Exif_fixtures_report_their_orientation_and_upright_size(int orientation)
    {
        var path = Fixtures.Jpeg($"exif-orientation-{orientation}.jpg");

        var image = PdfImage.FromFile(path);

        Assert.Equal(orientation, image.Data.Orientation);
        Assert.Equal(96, image.PixelWidth);
        Assert.Equal(64, image.PixelHeight);
        Assert.Contains(orientation >= 5 ? "/Width 64" : "/Width 96", image.Data.DictionaryEntries);
        Assert.Equal(File.ReadAllBytes(path), image.Data.EncodedData);
    }

    [Theory]
    [InlineData("rgb-baseline.jpg", "/DeviceRGB", 96d)]
    [InlineData("rgb-300dpi.jpg", "/DeviceRGB", 300d)]
    [InlineData("gray-baseline.jpg", "/DeviceGray", 96d)]
    public void Fixtures_load_with_color_space_size_and_resolution(string name, string colorSpace, double dpi)
    {
        var path = Fixtures.Jpeg(name);

        var image = PdfImage.FromFile(path);

        Assert.Equal(96, image.PixelWidth);
        Assert.Equal(64, image.PixelHeight);
        Assert.Equal(dpi, image.DpiX);
        Assert.Equal(dpi, image.DpiY);
        Assert.Contains($"/ColorSpace {colorSpace}", image.Data.DictionaryEntries);
        Assert.DoesNotContain(image.Data.DictionaryEntries, e => e.StartsWith("/Decode", StringComparison.Ordinal));
        Assert.Equal(File.ReadAllBytes(path), image.Data.EncodedData);
    }

    #endregion

    #endregion
}
