namespace UnlimitedPDF.Tests;

/// <summary>
/// A merge writes the same bytes whatever the host culture (A06, AC-18).
/// </summary>
public class MergeGoldenCultureTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    [InlineData("tr-TR")]
    public void Merge_bytes_do_not_depend_on_the_host_culture(string culture)
    {
        var reference = GoldenDocuments.Build(GoldenDocuments.Merge);

        byte[] actual;
        using (new CultureScope(culture))
        {
            // Build switches to the invariant culture itself, so the merge is also run directly under the culture.
            var document = new PdfDocument();
            document.Footer.Height = 20;
            document.Footer.AddText("Page {page} of {pages}", 0, 4, 9, PdfTextAlignment.Right);
            document.AddPage().AddText("Merged report", 72, 760, 18);
            document.AddPages(PdfFile.FromBytes(WithExtras()), firstPage: 2, lastPage: 3, bookmarkTitle: "Report");
            document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2, "(Appendix)")));
            actual = ImportedPagesTests.Save(document);
        }

        Assert.Equal(reference, actual);
    }

    #endregion

    #region Private Methods

    private static byte[] WithExtras() => ReaderFixtureBuilder.Document(3, catalogEntries: "/Outlines 40 0 R ")
        .AddObject(40, "<< /Type /Outlines /First 41 0 R /Last 42 0 R /Count 2 >>")
        .AddObject(41, "<< /Title (Summary) /Parent 40 0 R /Next 42 0 R /Dest [6 0 R /Fit] >>")
        .AddObject(42, "<< /Title (Details) /Parent 40 0 R /Prev 41 0 R /Dest [8 0 R /XYZ 0 792 0] >>")
        .ToBytes();

    #endregion

    #endregion
}
