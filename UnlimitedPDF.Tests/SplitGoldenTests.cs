namespace UnlimitedPDF.Tests;

/// <summary>
/// Pins the bytes of one split: the ten-page bookmarked source split every 4 pages (WR-037, AC-17). The golden parts
/// are also the files for the manual viewer check (T-13).
/// </summary>
public class SplitGoldenTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Split_every_4_matches_golden_bytes(int partNumber)
    {
        byte[] actual;
        using (new CultureScope(""))
            actual = PdfOperationsSplitTests.SplitToMemory(PdfFile.FromBytes(SplitFixtures.Bookmarked()), 4)[partNumber - 1].Bytes;
        string name = "split-every-4-" + partNumber.ToString(CultureInfo.InvariantCulture);
        GoldenFiles.CaptureIfRequested(name, actual);

        var path = GoldenFiles.PathFor(name);
        PdfStructureChecker.AssertValid(actual);
        Assert.True(File.Exists(path), $"Golden file '{path}' is missing. Capture it with {GoldenFiles.CaptureVariable}=1.");
        Assert.Equal(File.ReadAllBytes(path), actual);
    }

    #endregion

    #endregion
}
