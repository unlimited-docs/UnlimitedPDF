using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Read limits stop oversized and hostile files at load (WR-035, AC-14, A06).
/// </summary>
public class PdfFileLimitTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Decompression_bomb_fails_as_too_large()
    {
        // About 2 MB of zeros compress to a few kilobytes inside the object stream that holds the catalog.
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 >>")
            .AddObject(3, "(" + new string('0', 2_000_000) + ")")
            .ToBytesWithCrossReferenceStream(compressed: new[] { 1, 2, 3 });

        var error = Assert.Throws<PdfFileException>(() => PdfFileReader.Load(data, Limits(maxStreamSize: 100_000)));

        Assert.Equal(PdfFileErrorReason.TooLarge, error.Reason);
        Assert.True(data.Length < 20_000);
    }

    [Fact]
    public void Default_limits_are_500_and_100_megabytes_and_five_million_objects()
    {
        Assert.Equal(524_288_000, PdfReadLimits.Default.MaxFileSize);
        Assert.Equal(104_857_600, PdfReadLimits.Default.MaxStreamSize);
        Assert.Equal(5_000_000, PdfReadLimits.Default.MaxObjectCount);
    }

    [Fact]
    public void File_at_the_size_limit_loads_and_one_byte_over_fails()
    {
        var data = Document().ToBytes();

        PdfFileReader.Load(data, Limits(maxFileSize: data.Length));
        var error = Assert.Throws<PdfFileException>(() => PdfFileReader.Load(data, Limits(maxFileSize: data.Length - 1)));

        Assert.Equal(PdfFileErrorReason.TooLarge, error.Reason);
    }

    [Fact]
    public void Too_many_objects_fail_as_too_large()
    {
        var data = Document().ToBytes();

        var error = Assert.Throws<PdfFileException>(() => PdfFileReader.Load(data, Limits(maxObjectCount: 2)));

        Assert.Equal(PdfFileErrorReason.TooLarge, error.Reason);
    }

    [Fact]
    public void Too_many_objects_found_by_recovery_fail_as_too_large()
    {
        var data = Document().ToBytes();
        var broken = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(data).Replace("startxref", "startxrex"));

        var error = Assert.Throws<PdfFileException>(() => PdfFileReader.Load(broken, Limits(maxObjectCount: 2)));

        Assert.Equal(PdfFileErrorReason.TooLarge, error.Reason);
    }

    #endregion

    #region Private Methods

    private static ReaderFixtureBuilder Document() => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 >>")
        .AddObject(3, "(three)");

    private static PdfReadLimits Limits(long? maxFileSize = null, long? maxStreamSize = null, int? maxObjectCount = null) => new(
        maxFileSize ?? PdfReadLimits.Default.MaxFileSize,
        maxStreamSize ?? PdfReadLimits.Default.MaxStreamSize,
        maxObjectCount ?? PdfReadLimits.Default.MaxObjectCount);

    #endregion

    #endregion
}
