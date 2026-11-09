namespace UnlimitedPDF.Tests;

/// <summary>
/// A split part keeps the source bookmarks that point into it, at their own levels, with no wrapper; a part with no
/// surviving bookmark has no outline (WR-037, AC-09). Merge keeps its wrapper (WR-036, AC-12).
/// </summary>
public class SplitOutlineTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Part_keeps_the_bookmarks_into_its_pages_without_a_wrapper()
    {
        var pdf = Part(5, 8);

        PdfStructureChecker.AssertValid(pdf);
        var outline = SplitFixtures.Outline(pdf);
        Assert.Equal(new[] { "Europe", "Costs" }, outline.Select(b => b.Title));
        Assert.Equal(new int?[] { 1, 4 }, outline.Select(b => b.TargetPage));
        Assert.All(outline, b => Assert.Empty(b.Children));
    }

    [Fact]
    public void Whole_file_part_keeps_the_source_tree()
    {
        var outline = SplitFixtures.Outline(Part(1, 10));

        Assert.Equal(new[] { "Intro", "Sales", "Costs" }, outline.Select(b => b.Title));
        Assert.Equal(new int?[] { 1, 4, 8 }, outline.Select(b => b.TargetPage));
        var europe = Assert.Single(outline[1].Children);
        Assert.Equal("Europe", europe.Title);
        Assert.Equal(5, europe.TargetPage);
    }

    [Fact]
    public void Part_with_no_surviving_bookmark_has_no_outline()
    {
        var pdf = Part(2, 3);

        PdfStructureChecker.AssertValid(pdf);
        Assert.DoesNotContain("/Outlines", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
        Assert.Empty(SplitFixtures.Outline(pdf));
    }

    [Fact]
    public void Merge_still_wraps_each_import()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(SplitFixtures.Bookmarked()), 5, 8, "Part");

        var wrapper = Assert.Single(SplitFixtures.Outline(ImportedPagesTests.Save(document)));

        Assert.Equal("Part", wrapper.Title);
        Assert.Equal(new[] { "Europe", "Costs" }, wrapper.Children.Select(b => b.Title));
    }

    #endregion

    #region Private Methods

    private static byte[] Part(int firstPage, int lastPage)
    {
        var document = new PdfDocument { WrapImportBookmarks = false };
        document.AddPages(PdfFile.FromBytes(SplitFixtures.Bookmarked()), firstPage, lastPage);
        return ImportedPagesTests.Save(document);
    }

    #endregion

    #endregion
}
