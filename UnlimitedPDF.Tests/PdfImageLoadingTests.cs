using System.ComponentModel;
using System.Reflection;
using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Loading from every source, stream and buffer ownership, reported size and resolution, and async loading
/// (AC-01, AC-19, AC-26; D-026, D-028, D-040).
/// </summary>
public class PdfImageLoadingTests
{
    #region Fields and Constants

    private static readonly byte[] Jpeg = new JpegBuilder { Width = 640, Height = 480 }.Build();

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public async Task Async_loads_equal_sync_loads()
    {
        var path = WriteTempFile(Jpeg);
        try
        {
            var fromFile = await PdfImage.FromFileAsync(path);
            var fromStream = await PdfImage.FromStreamAsync(new MemoryStream(Jpeg));
            var sync = PdfImage.FromBytes(Jpeg);

            Assert.Equal(sync.Data.ContentKey, fromFile.Data.ContentKey);
            Assert.Equal(sync.Data.ContentKey, fromStream.Data.ContentKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Cancelling_during_a_stream_read_throws_and_produces_no_image()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new GatedStream(Jpeg);

        var load = PdfImage.FromStreamAsync(stream, cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
    }

    [Fact]
    public async Task Changing_the_array_after_load_has_no_effect()
    {
        var bytes = Jpeg.ToArray();
        var image = PdfImage.FromBytes(bytes);
        var key = image.Data.ContentKey;

        Array.Clear(bytes);

        Assert.Equal(Jpeg, image.Data.EncodedData);
        Assert.Equal(key, (await PdfImage.FromStreamAsync(new MemoryStream(Jpeg))).Data.ContentKey);
    }

    [Fact]
    public void Disposing_the_stream_after_load_has_no_effect()
    {
        var stream = new NonSeekableStream(Jpeg);
        var image = PdfImage.FromStream(stream);

        stream.Dispose();

        Assert.Equal(Jpeg, image.Data.EncodedData);
        Assert.Equal(640, image.PixelWidth);
    }

    [Fact]
    public void Identical_bytes_share_a_content_key_and_different_bytes_do_not()
    {
        var first = PdfImage.FromBytes(Jpeg);
        var second = PdfImage.FromBytes(Jpeg.ToArray());
        var other = PdfImage.FromBytes(new JpegBuilder { Width = 641, Height = 480 }.Build());

        Assert.Equal(first.Data.ContentKey, second.Data.ContentKey);
        Assert.NotEqual(first.Data.ContentKey, other.Data.ContentKey);
    }

    [Fact]
    public void Loads_from_a_file()
    {
        var path = WriteTempFile(Jpeg);
        try
        {
            var image = PdfImage.FromFile(path);

            Assert.Equal(640, image.PixelWidth);
            Assert.Equal(480, image.PixelHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loads_from_a_non_seekable_stream_and_leaves_it_open()
    {
        var stream = new NonSeekableStream(Jpeg);

        var image = PdfImage.FromStream(stream);

        Assert.Equal(640, image.PixelWidth);
        Assert.False(stream.IsDisposed);
    }

    [Fact]
    public void Loads_from_the_current_stream_position_to_the_end()
    {
        var prefix = Encoding.ASCII.GetBytes("unrelated header bytes");
        var stream = new MemoryStream(prefix.Concat(Jpeg).ToArray());
        stream.Position = prefix.Length;

        var image = PdfImage.FromStream(stream);

        Assert.Equal(Jpeg, image.Data.EncodedData);
        Assert.Equal(stream.Length, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Pre_cancelled_token_throws_for_file_and_stream()
    {
        var path = WriteTempFile(Jpeg);
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfImage.FromFileAsync(path, cancellationToken: cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfImage.FromStreamAsync(new MemoryStream(Jpeg), cancellationToken: cancellation.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Public_methods_carry_version_metadata()
    {
        var methods = new[] { typeof(PdfImage), typeof(PdfImageException) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(t.GetConstructors()))
            .Where(m => !m.IsSpecialName || m is ConstructorInfo);

        Assert.All(methods, method =>
        {
            Assert.Equal("1.0.0", method.GetCustomAttribute<AddedVersionAttribute>()?.Version);
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
        });
    }

    [Theory]
    [InlineData(1, 300, 300, 300d)]
    [InlineData(2, 118, 118, 299.72)]
    [InlineData(0, 1, 1, null)]
    [InlineData(1, 0, 0, null)]
    public void Reports_jfif_resolution(byte units, ushort x, ushort y, double? expectedDpi)
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 2480, Height = 3508, Jfif = (units, x, y) }.Build());

        Assert.Equal(2480, image.PixelWidth);
        Assert.Equal(3508, image.PixelHeight);
        AssertDpi(expectedDpi, image.DpiX);
        AssertDpi(expectedDpi, image.DpiY);
    }

    [Fact]
    public void Reports_no_resolution_without_jfif()
    {
        var image = PdfImage.FromBytes(Jpeg);

        Assert.Null(image.DpiX);
        Assert.Null(image.DpiY);
    }

    #endregion

    #region Private Methods

    private static void AssertDpi(double? expected, double? actual)
    {
        if (expected is null)
            Assert.Null(actual);
        else
            Assert.Equal(expected.Value, actual!.Value, 6);
    }

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"unlimitedpdf-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    #endregion

    #endregion
}
