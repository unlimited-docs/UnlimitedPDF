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

    [Fact]
    public void Web_link_bookmarks_stay_out_of_parts_and_their_children_move_up()
    {
        // Review F-04: a web link points to no page of any part, so parts drop it; merge keeps it.
        var file = PdfFile.FromBytes(WithWebLinks());
        var parts = PdfOperationsSplitTests.SplitToMemory(file, 1);

        Assert.Equal(new[] { "Intro" }, SplitFixtures.Outline(parts[0].Bytes).Select(b => b.Title));
        Assert.Empty(SplitFixtures.Outline(parts[1].Bytes));
        Assert.DoesNotContain("/Outlines", Encoding.Latin1.GetString(parts[1].Bytes), StringComparison.Ordinal);
        var third = Assert.Single(SplitFixtures.Outline(parts[2].Bytes));
        Assert.Equal("Section", third.Title);
        Assert.Equal(1, third.TargetPage);
        Assert.All(parts, p => Assert.DoesNotContain("/URI", Encoding.Latin1.GetString(p.Bytes), StringComparison.Ordinal));

        var merged = new PdfDocument();
        merged.AddPages(file, bookmarkTitle: "All");
        var wrapper = Assert.Single(SplitFixtures.Outline(ImportedPagesTests.Save(merged)));
        Assert.Equal(new[] { "Intro", "Website", "Links" }, wrapper.Children.Select(b => b.Title));
    }

    #endregion

    #region Private Methods

    // Three pages with bookmarks: Intro (page 1), Website (a web link), and Links (a web link) holding Section (page 3).
    private static byte[] WithWebLinks() => ReaderFixtureBuilder.Document(3, catalogEntries: "/Outlines 40 0 R ")
        .AddObject(40, "<< /Type /Outlines /First 41 0 R /Last 43 0 R /Count 4 >>")
        .AddObject(41, $"<< /Title (Intro) /Parent 40 0 R /Next 42 0 R /Dest [{SplitFixtures.Page(1)} 0 R /Fit] >>")
        .AddObject(42, "<< /Title (Website) /Parent 40 0 R /Prev 41 0 R /Next 43 0 R /A << /S /URI /URI (https://example.com) >> >>")
        .AddObject(43, "<< /Title (Links) /Parent 40 0 R /Prev 42 0 R /First 44 0 R /Last 44 0 R /Count 1 /A << /S /URI /URI (https://example.org) >> >>")
        .AddObject(44, $"<< /Title (Section) /Parent 43 0 R /Dest [{SplitFixtures.Page(3)} 0 R /Fit] >>")
        .ToBytes();

    private static byte[] Part(int firstPage, int lastPage)
    {
        var document = new PdfDocument { WrapImportBookmarks = false };
        document.AddPages(PdfFile.FromBytes(SplitFixtures.Bookmarked()), firstPage, lastPage);
        return ImportedPagesTests.Save(document);
    }

    #endregion

    #endregion
}
