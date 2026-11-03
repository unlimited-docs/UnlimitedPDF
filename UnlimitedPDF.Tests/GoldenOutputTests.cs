namespace UnlimitedPDF.Tests;

/// <summary>
/// Documents without images must keep producing the bytes the library wrote before the image feature (AC-14).
/// </summary>
public class GoldenOutputTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [MemberData(nameof(Documents))]
    public void Golden_documents_are_structurally_valid(string name)
    {
        PdfStructureChecker.AssertValid(GoldenDocuments.Build(name));
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Output_matches_golden_bytes(string name)
    {
        var actual = GoldenDocuments.Build(name);
        GoldenFiles.CaptureIfRequested(name, actual);

        var path = GoldenFiles.PathFor(name);
        Assert.True(File.Exists(path), $"Golden file '{path}' is missing. Capture it only from a build without the change under test, with {GoldenFiles.CaptureVariable}=1.");
        Assert.Equal(File.ReadAllBytes(path), actual);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<string> Documents => new()
    {
        GoldenDocuments.Invoice,
        GoldenDocuments.MiddleTable,
        GoldenDocuments.TextPages
    };

    #endregion
}
