using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Invalid input fails at load: null arguments and file errors throw standard exceptions, and other bad images throw
/// <see cref="PdfImageException"/> (AC-07; D-011, D-029; FA-07).
/// </summary>
public class PdfImageErrorTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Empty_data_is_an_unsupported_format()
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(Array.Empty<byte>()));

        Assert.Equal(PdfImageErrorReason.UnsupportedFormat, exception.Reason);
        Assert.Contains("empty", exception.Message);
    }

    [Fact]
    public async Task Missing_file_throws_file_not_found()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");

        Assert.Throws<FileNotFoundException>(() => PdfImage.FromFile(path));
        await Assert.ThrowsAsync<FileNotFoundException>(() => PdfImage.FromFileAsync(path));
    }

    [Fact]
    public async Task Null_arguments_throw_argument_null_with_parameter_names()
    {
        Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => PdfImage.FromBytes(null!)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => PdfImage.FromFile(null!)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => PdfImage.FromStream(null!)).ParamName);
        Assert.Equal("path", (await Assert.ThrowsAsync<ArgumentNullException>(() => PdfImage.FromFileAsync(null!))).ParamName);
        Assert.Equal("stream", (await Assert.ThrowsAsync<ArgumentNullException>(() => PdfImage.FromStreamAsync(null!))).ParamName);
    }

    [Fact]
    public void One_catch_handles_every_bad_image()
    {
        var badInputs = new[]
        {
            Array.Empty<byte>(),
            Encoding.ASCII.GetBytes("GIF89a"),
            new JpegBuilder { IncludeEndOfImage = false }.Build(),
            new JpegBuilder { Width = 2, Height = 2 }.Build()
        };
        var options = new PdfImageLoadOptions { MaxPixels = 1 };

        foreach (var input in badInputs)
        {
            try
            {
                PdfImage.FromBytes(input, options);
                Assert.Fail("Expected the load to fail.");
            }
            catch (PdfImageException)
            {
                // Expected: every content problem surfaces as the one library exception type.
            }
        }
    }

    [Theory]
    [InlineData("GIF89a\x01\x00\x01\x00", "GIF")]
    [InlineData("GIF87a\x01\x00\x01\x00", "GIF")]
    [InlineData("BM\x46\x00\x00\x00", "BMP")]
    [InlineData("II*\x00\x08\x00\x00\x00", "TIFF")]
    [InlineData("MM\x00*\x00\x00\x00\x08", "TIFF")]
    [InlineData("RIFF\x24\x00\x00\x00WEBPVP8 ", "WebP")]
    public void Other_image_formats_are_named_and_unsupported(string header, string formatName)
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(Encoding.Latin1.GetBytes(header)));

        Assert.Equal(PdfImageErrorReason.UnsupportedFormat, exception.Reason);
        Assert.Contains($"a {formatName} image", exception.Message);
        Assert.Contains("Only JPEG and PNG", exception.Message);
    }

    [Fact]
    public void Unrecognized_data_is_an_unsupported_format()
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(Encoding.ASCII.GetBytes("plain text, not an image")));

        Assert.Equal(PdfImageErrorReason.UnsupportedFormat, exception.Reason);
        Assert.Contains("not a recognized image format", exception.Message);
    }

    #endregion

    #endregion
}
