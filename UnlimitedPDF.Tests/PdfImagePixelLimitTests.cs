using UnlimitedPDF.Enums;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Tests that change the process-wide default limit run alone, so they cannot affect other loads.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessWideImageSettingsCollection
{
    public const string Name = "Process-wide image settings";
}

/// <summary>
/// The pixel limit: a 100,000,000-pixel built-in default, a process-wide setting, and a per-load override that wins
/// (AC-08; D-012, D-030, D-046).
/// </summary>
[Collection(ProcessWideImageSettingsCollection.Name)]
public class PdfImagePixelLimitTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public async Task Changing_the_default_affects_only_loads_that_start_afterwards()
    {
        var jpeg = new JpegBuilder { Width = 10, Height = 10 }.Build();
        var original = PdfImage.DefaultMaxPixels;
        try
        {
            var stream = new GatedStream(jpeg);
            var startedBeforeChange = PdfImage.FromStreamAsync(stream);

            PdfImage.DefaultMaxPixels = 50;
            stream.Release();

            Assert.Equal(10, (await startedBeforeChange).PixelWidth);
            Assert.Equal(PdfImageErrorReason.TooLarge, Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(jpeg)).Reason);
        }
        finally
        {
            PdfImage.DefaultMaxPixels = original;
        }
    }

    [Fact]
    public void Default_is_one_hundred_million_pixels_and_inclusive()
    {
        Assert.Equal(100_000_000, PdfImage.DefaultMaxPixels);

        var atLimit = PdfImage.FromBytes(new JpegBuilder { Width = 10_000, Height = 10_000 }.Build());
        Assert.Equal(10_000, atLimit.PixelWidth);

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(new JpegBuilder { Width = 10_001, Height = 10_000 }.Build()));
        Assert.Equal(PdfImageErrorReason.TooLarge, exception.Reason);
    }

    [Fact]
    public void Message_is_culture_invariant()
    {
        using var culture = new CultureScope("de-DE");

        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(new JpegBuilder { Width = 10_001, Height = 10_000 }.Build()));

        Assert.Contains("100,010,000", exception.Message);
    }

    [Fact]
    public void Message_states_pixels_limit_and_both_settings()
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(new JpegBuilder { Width = 10_001, Height = 10_000 }.Build()));

        Assert.Contains("10001 x 10000 pixels (100,010,000 pixels)", exception.Message);
        Assert.Contains("limit of 100,000,000 pixels", exception.Message);
        Assert.Contains("PdfImageLoadOptions.MaxPixels", exception.Message);
        Assert.Contains("PdfImage.DefaultMaxPixels", exception.Message);
    }

    [Fact]
    public void Non_positive_limits_are_rejected()
    {
        Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => PdfImage.DefaultMaxPixels = 0).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => new PdfImageLoadOptions { MaxPixels = -1 }).ParamName);
        Assert.Equal(100_000_000, PdfImage.DefaultMaxPixels);
    }

    [Fact]
    public void Per_load_limit_wins_over_the_default()
    {
        var jpeg = new JpegBuilder { Width = 10, Height = 10 }.Build();
        var original = PdfImage.DefaultMaxPixels;
        try
        {
            Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(jpeg, new PdfImageLoadOptions { MaxPixels = 99 }));
            Assert.Equal(10, PdfImage.FromBytes(jpeg, new PdfImageLoadOptions { MaxPixels = 100 }).PixelWidth);

            PdfImage.DefaultMaxPixels = 1;
            Assert.Equal(10, PdfImage.FromBytes(jpeg, new PdfImageLoadOptions { MaxPixels = 1_000 }).PixelWidth);
        }
        finally
        {
            PdfImage.DefaultMaxPixels = original;
        }
    }

    #endregion

    #endregion
}
