namespace UnlimitedPDF.Tests;

/// <summary>
/// The structural checker must reject each defect it exists to catch (AC-27).
/// </summary>
public class PdfStructureCheckerTests
{
    #region Fields and Constants

    private static readonly byte[] ImageData = { 0x10, 0x20, 0x30, 0x40 };
    private static readonly byte[] MaskData = { 0xFF, 0x00 };

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Duplicate_image_is_reported()
    {
        // The same image, with the same mask, stored twice.
        var pdf = PageWithImage("/XObject << /Im1 5 0 R /Im2 7 0 R >>")
            .AddStream(5, Image(6), ImageData)
            .AddStream(6, Mask(), MaskData)
            .AddStream(7, Image(6), ImageData)
            .ToBytes();

        AssertReports(pdf, "Image object 7 duplicates image object 5");
    }

    [Fact]
    public void Duplicate_mask_is_reported()
    {
        // Two images whose identical masks are stored as two objects.
        var pdf = PageWithImage("/XObject << /Im1 5 0 R /Im2 7 0 R >>")
            .AddStream(5, Image(6), ImageData)
            .AddStream(6, Mask(), MaskData)
            .AddStream(7, Image(8), ImageData)
            .AddStream(8, Mask(), MaskData)
            .ToBytes();

        AssertReports(pdf, "Image object 8 duplicates image object 6");
    }

    [Fact]
    public void Images_sharing_color_data_with_different_masks_are_not_duplicates()
    {
        var pdf = PageWithImage("/XObject << /Im1 5 0 R /Im2 7 0 R >>")
            .AddStream(5, Image(6), ImageData)
            .AddStream(6, Mask(), MaskData)
            .AddStream(7, Image(8), ImageData)
            .AddStream(8, Mask(), new byte[] { 0x00, 0xFF })
            .ToBytes();

        Assert.Empty(PdfStructureChecker.Check(pdf));
    }

    [Fact]
    public void Missing_page_resource_is_reported()
    {
        var pdf = PageWithImage("/XObject << /Im1 9 0 R >>")
            .AddStream(5, Image(6), ImageData)
            .AddStream(6, Mask(), MaskData)
            .ToBytes();

        AssertReports(pdf, "Page object 3 references missing object 9");
    }

    [Fact]
    public void Missing_transparency_mask_is_reported()
    {
        // Object 6 exists but is not an image, so the mask is effectively missing.
        var pdf = PageWithImage("/XObject << /Im1 5 0 R >>")
            .AddStream(5, Image(6), ImageData)
            .AddObject(6, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
            .ToBytes();

        AssertReports(pdf, "names transparency mask 6, which is missing or not an image");
    }

    [Fact]
    public void Shifted_xref_offset_is_reported()
    {
        var pdf = ValidDocument().ShiftXrefOffset(3, 1).ToBytes();

        AssertReports(pdf, "Xref entry for object 3 points at offset");
    }

    [Fact]
    public void Startxref_not_pointing_at_xref_is_reported()
    {
        var pdf = ValidDocument().ToBytes();
        var text = Encoding.Latin1.GetString(pdf);
        var index = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        var broken = Encoding.Latin1.GetBytes(text[..index] + "7\n%%EOF\n");

        AssertReports(broken, "does not point at 'xref'");
    }

    [Fact]
    public void Valid_document_passes()
    {
        PdfStructureChecker.AssertValid(ValidDocument().ToBytes());
    }

    [Fact]
    public void Valid_library_output_passes()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Hello", 72, 720);
        using var stream = new MemoryStream();
        document.Save(stream);

        PdfStructureChecker.AssertValid(stream.ToArray());
    }

    [Fact]
    public void Wrong_header_is_reported()
    {
        AssertReports(ValidDocument().ToBytes("%PDX-1.7"), "Header does not start with '%PDF-1.'");
    }

    [Fact]
    public void Wrong_stream_length_is_reported()
    {
        var pdf = PageWithImage("/XObject << /Im1 5 0 R >>")
            .AddStream(5, Image(6), ImageData, lengthOverride: ImageData.Length + 1)
            .AddStream(6, Mask(), MaskData)
            .ToBytes();

        AssertReports(pdf, "Stream object 5 has /Length 5, which does not match its data");
    }

    #endregion

    #region Private Methods

    private static void AssertReports(byte[] pdf, string expected)
    {
        var errors = PdfStructureChecker.Check(pdf);
        Assert.Contains(errors, e => e.Contains(expected, StringComparison.Ordinal));
    }

    private static string Image(int maskNumber) =>
        $"/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /SMask {maskNumber} 0 R";

    private static string Mask() =>
        "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8";

    private static RawPdfBuilder PageWithImage(string resources) => new RawPdfBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
        .AddObject(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << {resources} >> >>")
        .AddStream(4, string.Empty, Encoding.ASCII.GetBytes("q 2 0 0 1 0 0 cm /Im1 Do Q"));

    private static RawPdfBuilder ValidDocument() => PageWithImage("/XObject << /Im1 5 0 R >>")
        .AddStream(5, Image(6), ImageData)
        .AddStream(6, Mask(), MaskData);

    #endregion

    #endregion
}
