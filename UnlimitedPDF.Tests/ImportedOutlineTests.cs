using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Each import adds one top-level bookmark holding the file's own bookmarks (WR-036, AC-07).
/// </summary>
public class ImportedOutlineTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Bookmarks_nest_under_one_bookmark_per_import_and_keep_their_state()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(WithBookmarks()), bookmarkTitle: "Report");

        var outline = Outline(ImportedPagesTests.Save(document));

        var report = Assert.Single(outline);
        Assert.Equal("Report", report.Title);
        Assert.Equal(new[] { "Intro", "Chapter", "End" }, report.Children.Select(c => c.Title));
        var chapter = report.Children[1];
        Assert.False(chapter.Open);
        Assert.Equal(new[] { "Section A", "Café" }, chapter.Children.Select(c => c.Title));
        Assert.Equal(2, chapter.TargetPage);
        Assert.Equal(3, chapter.Children[1].TargetPage);
        Assert.Equal(1, report.TargetPage);
    }

    [Fact]
    public void Bookmarks_to_pages_left_out_are_dropped_and_their_children_move_up()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(WithBookmarks()), firstPage: 3, lastPage: 3, bookmarkTitle: "Last page");

        var report = Assert.Single(Outline(ImportedPagesTests.Save(document)));

        Assert.Equal(new[] { "Section A", "Café", "End" }, report.Children.Select(c => c.Title));
        Assert.All(report.Children, child => Assert.Equal(1, child.TargetPage));
    }

    [Fact]
    public void Cyclic_bookmark_links_stop_their_branch()
    {
        var data = ReaderFixtureBuilder.Document(1, catalogEntries: "/Outlines 40 0 R ")
            .AddObject(40, "<< /Type /Outlines /First 41 0 R /Last 41 0 R /Count 1 >>")
            .AddObject(41, "<< /Title (Loop) /Parent 40 0 R /Next 41 0 R /First 41 0 R /Dest [4 0 R /Fit] >>")
            .ToBytes();
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(data));

        var pdf = ImportedPagesTests.Save(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(new[] { "Loop" }, Assert.Single(Outline(pdf)).Children.Select(c => c.Title));
    }

    [Fact]
    public void Titles_fall_back_to_the_document_title_the_file_name_and_the_import_position()
    {
        var titled = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1, "(Annual Report)"));
        var untitled = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1));
        string path = Path.Combine(Path.GetTempPath(), $"invoice-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, ReaderFixtureBuilder.DocumentBytes(1));
        try
        {
            var document = new PdfDocument();
            document.AddPages(titled);
            document.AddPages(PdfFile.FromFile(path));
            document.AddPages(untitled);
            document.AddPages(titled, bookmarkTitle: "Chosen");

            var titles = Outline(ImportedPagesTests.Save(document)).Select(item => item.Title);

            Assert.Equal(new[] { "Annual Report", Path.GetFileName(path), "Document 3", "Chosen" }, titles);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Q3 (draft) \\ final")]
    [InlineData("東京 été 📈")]
    public void Caller_titles_read_back_exactly(string title)
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1)), bookmarkTitle: title);

        var pdf = ImportedPagesTests.Save(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(title, Assert.Single(Outline(pdf)).Title);
    }

    [Fact]
    public void Later_imports_and_saves_rewrite_the_bookmarks_in_place()
    {
        var file = PdfFile.FromBytes(WithBookmarks());
        var document = new PdfDocument();
        document.AddPages(file, bookmarkTitle: "First");
        var first = ImportedPagesTests.Save(document);
        Assert.Equal(first, ImportedPagesTests.Save(document));

        document.AddPages(file, firstPage: 2, lastPage: 2, bookmarkTitle: "Second");
        var second = ImportedPagesTests.Save(document);

        PdfStructureChecker.AssertValid(second);
        var outline = Outline(second);
        Assert.Equal(new[] { "First", "Second" }, outline.Select(item => item.Title));
        Assert.Equal(new[] { "Chapter" }, outline[1].Children.Select(c => c.Title));
        Assert.Equal(4, outline[1].TargetPage);
    }

    [Fact]
    public void Document_without_imports_has_no_bookmarks()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Plain", 72, 72);

        Assert.DoesNotContain("/Outlines", Encoding.ASCII.GetString(ImportedPagesTests.Save(document)), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static List<Item> Outline(byte[] pdf)
    {
        var output = PdfFile.FromBytes(pdf);
        var table = output.Source.Objects;
        var pages = output.Pages.Select((page, index) => (page.ObjectNumber, index + 1)).ToDictionary(p => p.ObjectNumber, p => p.Item2);

        List<Item> Siblings(PdfReadValue? first, int depth)
        {
            var items = new List<Item>();
            var visited = new HashSet<int>();
            for (var current = first; current is PdfReadReference reference && visited.Add(reference.ObjectNumber) && depth < 20;)
            {
                var item = Assert.IsType<PdfReadDictionary>(table.Resolve(current));
                var destination = table.Resolve(item["Dest"]) as PdfReadArray;
                int? target = destination?[0] is PdfReadReference page && pages.TryGetValue(page.ObjectNumber, out int number) ? number : null;
                bool open = table.Resolve(item["Count"]) is not PdfReadNumber { Value: < 0 };
                string title = PdfTextString.Decode(Assert.IsType<PdfReadString>(table.Resolve(item["Title"])).Bytes);
                items.Add(new Item(title, target, open, Siblings(item["First"], depth + 1)));
                current = item["Next"];
            }
            return items;
        }

        var root = Assert.IsType<PdfReadDictionary>(table.Resolve(output.Source.Catalog["Outlines"]));
        return Siblings(root["First"], 0);
    }

    // Three pages with bookmarks: Intro (page 1), Chapter (page 2, closed) holding Section A (page 3) and Café (a
    // named destination to page 3), and End (page 3).
    private static byte[] WithBookmarks() => ReaderFixtureBuilder.Document(3, catalogEntries: "/Outlines 40 0 R /Dests 50 0 R ")
        .AddObject(40, "<< /Type /Outlines /First 41 0 R /Last 43 0 R /Count 3 >>")
        .AddObject(41, "<< /Title (Intro) /Parent 40 0 R /Next 42 0 R /Dest [4 0 R /Fit] >>")
        .AddObject(42, "<< /Title (Chapter) /Parent 40 0 R /Prev 41 0 R /Next 43 0 R /First 44 0 R /Last 45 0 R /Count -2 /A << /S /GoTo /D [6 0 R /Fit] >> >>")
        .AddObject(43, "<< /Title (End) /Parent 40 0 R /Prev 42 0 R /Dest [8 0 R /Fit] >>")
        .AddObject(44, "<< /Title (Section A) /Parent 42 0 R /Next 45 0 R /Dest [8 0 R /XYZ 0 792 0] >>")
        .AddObject(45, "<< /Title (Caf\\351) /Parent 42 0 R /Prev 44 0 R /Dest /named3 >>")
        .AddObject(50, "<< /named3 [8 0 R /Fit] >>")
        .ToBytes();

    #endregion

    #endregion

    #region Nested Types

    private sealed record Item(string Title, int? TargetPage, bool Open, List<Item> Children);

    #endregion
}
