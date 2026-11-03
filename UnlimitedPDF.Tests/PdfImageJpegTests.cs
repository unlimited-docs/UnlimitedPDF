using UnlimitedPDF.Enums;

namespace UnlimitedPDF.Tests;

/// <summary>
/// JPEG variants, CMYK inversion, and JPEG damage detection (AC-02, AC-07; D-019, D-037, D-051; FA-01, FA-02).
/// </summary>
public class PdfImageJpegTests
{
    #region Fields and Constants

    private const string CmykDecode = "/Decode [1 0 1 0 1 0 1 0]";

    #endregion

    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(PdfCmykInversion.Auto, true, true)]
    [InlineData(PdfCmykInversion.Auto, false, false)]
    [InlineData(PdfCmykInversion.Inverted, false, true)]
    [InlineData(PdfCmykInversion.NotInverted, true, false)]
    public void Cmyk_inversion_follows_adobe_marker_and_override(PdfCmykInversion inversion, bool adobe, bool expectDecode)
    {
        var jpeg = new JpegBuilder { Components = 4, Adobe = adobe }.Build();

        var image = PdfImage.FromBytes(jpeg, new PdfImageLoadOptions { CmykInversion = inversion });

        Assert.Contains("/ColorSpace /DeviceCMYK", image.Data.DictionaryEntries);
        Assert.Equal(expectDecode, image.Data.DictionaryEntries.Contains(CmykDecode));
    }

    [Fact]
    public void Cmyk_override_does_not_affect_rgb()
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Adobe = true }.Build(), new PdfImageLoadOptions { CmykInversion = PdfCmykInversion.Inverted });

        Assert.DoesNotContain(CmykDecode, image.Data.DictionaryEntries);
    }

    [Theory]
    [InlineData(0xC0, 1, "/DeviceGray")]
    [InlineData(0xC0, 3, "/DeviceRGB")]
    [InlineData(0xC0, 4, "/DeviceCMYK")]
    [InlineData(0xC1, 3, "/DeviceRGB")]
    [InlineData(0xC2, 1, "/DeviceGray")]
    [InlineData(0xC2, 3, "/DeviceRGB")]
    [InlineData(0xC2, 4, "/DeviceCMYK")]
    public void Loads_baseline_and_progressive_variants(byte frameMarker, int components, string colorSpace)
    {
        var jpeg = new JpegBuilder { FrameMarker = frameMarker, Components = components, Width = 33, Height = 17, ScanCount = frameMarker == 0xC2 ? 3 : 1 }.Build();

        var image = PdfImage.FromBytes(jpeg);

        Assert.Equal(new[] { "/Width 33", "/Height 17", $"/ColorSpace {colorSpace}", "/BitsPerComponent 8", "/Filter /DCTDecode" },
            image.Data.DictionaryEntries.Take(5));
        Assert.Equal(jpeg, image.Data.EncodedData);
        Assert.Null(image.Data.SoftMask);
    }

    [Fact]
    public void Rejects_12_bit_samples()
    {
        var exception = AssertLoadFails(new JpegBuilder { Precision = 12 }.Build(), PdfImageErrorReason.UnsupportedFormat);

        Assert.Contains("12-bit", exception.Message);
    }

    [Fact]
    public void Rejects_height_set_by_a_later_marker()
    {
        var exception = AssertLoadFails(new JpegBuilder { Height = 0 }.Build(), PdfImageErrorReason.UnsupportedFormat);

        Assert.Contains("DNL marker", exception.Message);
    }

    [Fact]
    public void Rejects_two_component_images()
    {
        AssertLoadFails(new JpegBuilder { Components = 2 }.Build(), PdfImageErrorReason.UnsupportedFormat);
    }

    [Theory]
    [InlineData(0xC3, "lossless")]
    [InlineData(0xC5, "hierarchical")]
    [InlineData(0xC9, "arithmetic")]
    [InlineData(0xCA, "arithmetic")]
    public void Rejects_unsupported_coding(byte frameMarker, string coding)
    {
        var exception = AssertLoadFails(new JpegBuilder { FrameMarker = frameMarker }.Build(), PdfImageErrorReason.UnsupportedFormat);

        Assert.Contains(coding, exception.Message);
    }

    [Fact]
    public void Reports_garbage_between_segments_as_corrupt()
    {
        // After the start-of-image marker (2 bytes) and the 67-byte quantization segment (marker + 65-byte payload + length).
        var jpeg = new JpegBuilder().Build().ToList();
        jpeg.InsertRange(2 + 2 + 2 + 65, new byte[] { 0x00, 0x11 });

        AssertLoadFails(jpeg.ToArray(), PdfImageErrorReason.CorruptData);
    }

    [Fact]
    public void Reports_missing_end_of_image_as_corrupt()
    {
        var exception = AssertLoadFails(new JpegBuilder { IncludeEndOfImage = false }.Build(), PdfImageErrorReason.CorruptData);

        Assert.Contains("end-of-image", exception.Message);
    }

    [Fact]
    public void Reports_scan_before_frame_as_corrupt()
    {
        AssertLoadFails(new JpegBuilder { IncludeFrame = false }.Build(), PdfImageErrorReason.CorruptData);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(40)]
    public void Reports_truncated_data_as_corrupt(int keepBytes)
    {
        var jpeg = new JpegBuilder().Build();

        AssertLoadFails(jpeg[..keepBytes], PdfImageErrorReason.CorruptData);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void Reports_zero_width_as_corrupt(int height)
    {
        var exception = AssertLoadFails(new JpegBuilder { Width = 0, Height = height }.Build(), PdfImageErrorReason.CorruptData);

        Assert.Contains("zero width", exception.Message);
    }

    #endregion

    #region Private Methods

    private static PdfImageException AssertLoadFails(byte[] data, PdfImageErrorReason reason)
    {
        var exception = Assert.Throws<PdfImageException>(() => PdfImage.FromBytes(data));
        Assert.Equal(reason, exception.Reason);
        Assert.StartsWith("The JPEG image", exception.Message);
        return exception;
    }

    #endregion

    #endregion
}
