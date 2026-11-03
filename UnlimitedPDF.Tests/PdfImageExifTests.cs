using UnlimitedPDF.Models.Internal.Imaging;

namespace UnlimitedPDF.Tests;

/// <summary>
/// EXIF orientation is honored by default for all 8 values, reported sizes are the displayed sizes, the JPEG bytes
/// stay unchanged, and damaged EXIF is ignored safely (AC-23; D-036; A06).
/// </summary>
public class PdfImageExifTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Block_reaching_past_the_buffer_is_ignored()
    {
        var block = JpegBuilder.TiffBlock(6);

        Assert.Equal(1, ExifReader.ReadOrientation(block, 0, block.Length + 10));
    }

    [Fact]
    public void Entry_count_past_the_block_is_ignored()
    {
        var block = JpegBuilder.TiffBlock(6);
        block[8] = 0xFF; // Entry count 0xFFFF (little-endian) far exceeds the block.
        block[9] = 0xFF;

        Assert.Equal(1, ExifReader.ReadOrientation(block, 0, block.Length));
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void Honors_each_orientation_and_reports_displayed_size(ushort orientation, bool bigEndian)
    {
        var jpeg = PhonePhoto(JpegBuilder.ExifSegment(orientation, bigEndian));

        var image = PdfImage.FromBytes(jpeg);

        var swapped = orientation >= 5;
        Assert.Equal(swapped ? 3024 : 4032, image.PixelWidth);
        Assert.Equal(swapped ? 4032 : 3024, image.PixelHeight);
        Assert.Equal(orientation, image.Data.Orientation);
        Assert.Equal(jpeg, image.Data.EncodedData);
        Assert.Contains("/Width 4032", image.Data.DictionaryEntries);
        Assert.Contains("/Height 3024", image.Data.DictionaryEntries);
    }

    [Fact]
    public void Matches_story_example_portrait_photo()
    {
        // AC-23: stored 4032 x 3024 with orientation 6 appears upright and reports 3024 x 4032.
        var image = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(6)));

        Assert.Equal(3024, image.PixelWidth);
        Assert.Equal(4032, image.PixelHeight);
    }

    [Fact]
    public void Only_the_first_exif_block_counts()
    {
        var jpeg = new JpegBuilder
        {
            Width = 4032,
            Height = 3024,
            ExtraSegments = { JpegBuilder.ExifSegment(3), JpegBuilder.ExifSegment(6) }
        }.Build();

        Assert.Equal(3, PdfImage.FromBytes(jpeg).Data.Orientation);
    }

    [Fact]
    public void Opt_out_keeps_stored_orientation_and_size()
    {
        var image = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(6)), new PdfImageLoadOptions { ApplyExifOrientation = false });

        Assert.Equal(4032, image.PixelWidth);
        Assert.Equal(3024, image.PixelHeight);
        Assert.Equal(1, image.Data.Orientation);
    }

    [Fact]
    public void Orientation_does_not_change_the_stored_image_key()
    {
        var upright = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(6)), new PdfImageLoadOptions { ApplyExifOrientation = false });
        var rotated = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(6)));

        Assert.Equal(upright.Data.ContentKey, rotated.Data.ContentKey);
    }

    public static TheoryData<ushort, bool> Orientations()
    {
        var data = new TheoryData<ushort, bool>();
        for (ushort orientation = 1; orientation <= 8; orientation++)
        {
            data.Add(orientation, false);
            data.Add(orientation, true);
        }
        return data;
    }

    [Theory]
    [InlineData(4)]          // Inside the header.
    [InlineData(200)]        // Past the end of the block.
    [InlineData(0xFFFFFFF0)] // Would overflow 32-bit arithmetic.
    public void Out_of_range_directory_offset_is_ignored(uint directoryOffset)
    {
        var image = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(6, directoryOffset: directoryOffset)));

        Assert.Equal(1, image.Data.Orientation);
        Assert.Equal(4032, image.PixelWidth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(65535)]
    public void Out_of_range_values_are_treated_as_upright(ushort orientation)
    {
        var image = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(orientation)));

        Assert.Equal(1, image.Data.Orientation);
        Assert.Equal(4032, image.PixelWidth);
    }

    [Fact]
    public void Self_referencing_directory_chain_is_not_followed()
    {
        // The block's next-directory offset points back at its own directory; reading must still finish.
        var block = JpegBuilder.TiffBlock(8, bigEndian: true);

        Assert.Equal(8, ExifReader.ReadOrientation(block, 0, block.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Truncated_header_is_ignored(int length)
    {
        var block = JpegBuilder.TiffBlock(6);

        Assert.Equal(1, ExifReader.ReadOrientation(block, 0, length));
    }

    [Fact]
    public void Unknown_byte_order_is_ignored()
    {
        var block = JpegBuilder.TiffBlock(6);
        block[0] = (byte)'X';
        block[1] = (byte)'X';

        Assert.Equal(1, ExifReader.ReadOrientation(block, 0, block.Length));
    }

    [Fact]
    public void Wrong_magic_number_is_ignored()
    {
        var block = JpegBuilder.TiffBlock(6);
        block[2] = 43;

        Assert.Equal(1, ExifReader.ReadOrientation(block, 0, block.Length));
    }

    [Fact]
    public void Wrong_value_type_is_ignored()
    {
        var image = PdfImage.FromBytes(PhonePhoto(JpegBuilder.ExifSegment(6, tagType: 4)));

        Assert.Equal(1, image.Data.Orientation);
    }

    #endregion

    #region Private Methods

    private static byte[] PhonePhoto(byte[] exifSegment) =>
        new JpegBuilder { Width = 4032, Height = 3024, Jfif = (1, 72, 72), ExtraSegments = { exifSegment } }.Build();

    #endregion

    #endregion
}
