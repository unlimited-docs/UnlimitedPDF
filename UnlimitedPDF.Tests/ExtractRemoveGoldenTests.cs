namespace UnlimitedPDF.Tests;

/// <summary>
/// Pins the bytes of one extract and one remove of a ten-page source with bookmarks, document information, and XMP
/// metadata, in every culture (WR-040, AC-18, V-15). The golden files are also the files for the manual viewer check
/// (T-15).
/// </summary>
public class ExtractRemoveGoldenTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("hi-IN")]
    public void Extract_matches_golden_bytes(string culture)
    {
        byte[] actual;
        using (new CultureScope(culture))
            actual = PdfOperationsExtractTests.Extract(Source(), "9,2,2-5");

        AssertGolden("extract", actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("hi-IN")]
    public void Remove_matches_golden_bytes(string culture)
    {
        byte[] actual;
        using (new CultureScope(culture))
            actual = PdfOperationsExtractTests.Remove(Source(), "1,10");

        AssertGolden("remove", actual);
    }

    #endregion

    #region Private Methods

    private static void AssertGolden(string name, byte[] actual)
    {
        GoldenFiles.CaptureIfRequested(name, actual);

        var path = GoldenFiles.PathFor(name);
        PdfStructureChecker.AssertValid(actual);
        Assert.True(File.Exists(path), $"Golden file '{path}' is missing. Capture it with {GoldenFiles.CaptureVariable}=1.");
        Assert.Equal(File.ReadAllBytes(path), actual);
    }

    // Ten pages with the split fixture's bookmarks (Intro 1, Sales 4 holding Europe 5, Costs 8), an /Info dictionary,
    // and an XMP stream.
    private static byte[] Source() => ReaderFixtureBuilder.Document(10, catalogEntries: "/Outlines 40 0 R /Metadata 50 0 R ")
        .AddObject(40, "<< /Type /Outlines /First 41 0 R /Last 44 0 R /Count 4 >>")
        .AddObject(41, $"<< /Title (Intro) /Parent 40 0 R /Next 42 0 R /Dest [{SplitFixtures.Page(1)} 0 R /Fit] >>")
        .AddObject(42, $"<< /Title (Sales) /Parent 40 0 R /Prev 41 0 R /Next 44 0 R /First 43 0 R /Last 43 0 R /Count 1 /Dest [{SplitFixtures.Page(4)} 0 R /Fit] >>")
        .AddObject(43, $"<< /Title (Europe) /Parent 42 0 R /Dest [{SplitFixtures.Page(5)} 0 R /Fit] >>")
        .AddObject(44, $"<< /Title (Costs) /Parent 40 0 R /Prev 42 0 R /Dest [{SplitFixtures.Page(8)} 0 R /Fit] >>")
        .AddStream(50, "/Type /Metadata /Subtype /XML", Encoding.ASCII.GetBytes("<x:xmpmeta xmlns:x=\"urn:unlimitedpdf:test\"/>"))
        .AddObject(60, "<< /Title (Quarterly report) /Author (Finance team) /Subject (Results) /Keywords (sales, costs) >>")
        .ToBytes(trailerEntries: "/Root 1 0 R /Info 60 0 R");

    #endregion

    #endregion
}
