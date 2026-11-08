using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The playground's Merge demos and packs build what CR-001 sets (AC-23 to AC-29).
/// </summary>
public class MergePlaygroundTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Bookmarks_and_numbers_titles_read_back_and_numbers_count_every_page()
    {
        var input = new BookmarksAndNumbersInput { FooterText = "Page {page} of {pages}" };
        input.Files.Add(new MergeFileInput("a.pdf", File(2)) { BookmarkTitle = "Q3 (draft) \\ final" });
        input.Files.Add(new MergeFileInput("b.pdf", File(1)));

        var pdf = Save(BookmarksAndNumbersSample.Build(input));
        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(pdf));

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(5, texts.Length);
        Assert.Contains("Page 1 of 5", texts[0], StringComparison.Ordinal);
        Assert.Equal(new[] { "Page 1", "Page 2", "Page 1" }, texts.Skip(1).Take(3));
        Assert.Contains("Page 5 of 5", texts[4], StringComparison.Ordinal);
        Assert.Equal(new[] { "Q3 (draft) \\ final", "b.pdf" }, BookmarkTitles(pdf));
    }

    [Fact]
    public void Catalog_lists_the_merge_category_after_create_in_order()
    {
        var categories = SampleCatalog.Categories;
        var merge = categories[1];

        Assert.Equal("Create", categories[0].Title);
        Assert.Equal("Merge", merge.Title);
        Assert.Equal(
            new[] { "Join files", "Cover + ranges", "Pick pages", "Bookmarks & numbers", "Month-end pack", "Employee pack", "Patient pack" },
            merge.Demos.Select(demo => demo.Title));
        Assert.Equal(
            new[] { "join-files", "cover-and-ranges", "pick-pages", "bookmarks-and-numbers", "month-end-pack", "employee-pack", "patient-pack" },
            merge.Demos.Select(demo => demo.Route.Replace("playground/merge/", string.Empty, StringComparison.Ordinal)));
        Assert.All(merge.Demos, demo => Assert.StartsWith("playground/merge/", demo.Route, StringComparison.Ordinal));
        Assert.DoesNotContain(categories.SelectMany(c => c.Demos), demo => demo.Route.Contains("merge-pdfs", StringComparison.Ordinal));
    }

    [Fact]
    public void Join_files_preview_equals_the_one_call_merge_and_follows_the_order()
    {
        var input = new JoinFilesInput();
        input.Files.Add(new MergeFileInput("two", File(2)));
        input.Files.Add(new MergeFileInput("three", File(3)));

        var preview = Save(JoinFilesSample.Build(input));
        (input.Files[0], input.Files[1]) = (input.Files[1], input.Files[0]);
        var reordered = ImportedPagesTests.PageTexts(PdfFile.FromBytes(Save(JoinFilesSample.Build(input))));

        PdfStructureChecker.AssertValid(preview);
        (input.Files[0], input.Files[1]) = (input.Files[1], input.Files[0]);
        Assert.Equal(preview, JoinFilesSample.Merge(input));
        Assert.Equal(new[] { "Page 1", "Page 2", "Page 1", "Page 2", "Page 3" }, ImportedPagesTests.PageTexts(PdfFile.FromBytes(preview)));
        Assert.Equal(new[] { "Page 1", "Page 2", "Page 3", "Page 1", "Page 2" }, reordered);
        Assert.Equal(2, BookmarkTitles(preview).Count);
    }

    [Fact]
    public void Merge_demos_without_files_build_nothing_and_say_why()
    {
        Assert.Equal("Add at least one PDF.", Assert.Throws<InvalidOperationException>(() => JoinFilesSample.Build(new JoinFilesInput())).Message);
        Assert.Equal("Add at least one PDF.", Assert.Throws<InvalidOperationException>(() => BookmarksAndNumbersSample.Build(new BookmarksAndNumbersInput())).Message);
        Assert.Equal("Choose a PDF.", Assert.Throws<InvalidOperationException>(() => PickPagesSample.Build(new PickPagesInput())).Message);
    }

    [Fact]
    public void Pack_holds_the_cover_and_each_included_document_under_its_name()
    {
        var input = Pack();

        var pdf = Save(MergePackSample.Build(input));
        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(pdf));

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(1 + 2 + 1 + 3, texts.Length);
        Assert.Contains("Month-end pack", texts[0], StringComparison.Ordinal);
        Assert.Contains("Receipt (1 page)", texts[0], StringComparison.Ordinal);
        Assert.Equal(new[] { "Invoice", "Receipt", "Account statement" }, BookmarkTitles(pdf));
    }

    [Fact]
    public void Pack_without_a_document_leaves_its_pages_out_and_with_none_builds_nothing()
    {
        var input = Pack();
        input.Documents[1].Include = false;

        var pdf = Save(MergePackSample.Build(input));

        Assert.Equal(1 + 2 + 3, PdfFile.FromBytes(pdf).PageCount);
        Assert.Equal(new[] { "Invoice", "Account statement" }, BookmarkTitles(pdf));

        foreach (var document in input.Documents) document.Include = false;
        Assert.Equal("Choose at least one document.", Assert.Throws<InvalidOperationException>(() => MergePackSample.Build(input)).Message);
    }

    [Theory]
    [InlineData("3, 1-2", new[] { 3, 1, 2 })]
    [InlineData(" 2 ,4-5 ", new[] { 2, 4, 5 })]
    [InlineData("5", new[] { 5 })]
    [InlineData("1-5", new[] { 1, 2, 3, 4, 5 })]
    public void Pick_pages_takes_the_pages_in_the_order_written(string pages, int[] expected)
    {
        var input = new PickPagesInput { File = new MergeFileInput("five.pdf", File(5)), Pages = pages };

        var pdf = Save(PickPagesSample.Build(input));

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(expected.Select(n => $"Page {n}"), ImportedPagesTests.PageTexts(PdfFile.FromBytes(pdf)));
    }

    [Theory]
    [InlineData("", "Enter the pages")]
    [InlineData(" , ", "Enter the pages")]
    [InlineData("3-", "'3-' is not a page")]
    [InlineData("a", "'a' is not a page")]
    [InlineData("0", "'0' is not a page")]
    [InlineData("1-2-3", "'1-2-3' is not a page")]
    [InlineData("4-2", "runs backwards; write 2-4")]
    [InlineData("2, 9", "Page 9 is past the last page (5)")]
    public void Pick_pages_rejects_an_invalid_list_and_builds_nothing(string pages, string message)
    {
        var input = new PickPagesInput { File = new MergeFileInput("five.pdf", File(5)), Pages = pages };

        var error = Assert.Throws<InvalidOperationException>(() => PickPagesSample.Build(input));

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static List<string> BookmarkTitles(byte[] pdf)
    {
        var output = PdfFile.FromBytes(pdf);
        var table = output.Source.Objects;
        var root = Assert.IsType<PdfReadDictionary>(table.Resolve(output.Source.Catalog["Outlines"]));
        var titles = new List<string>();
        for (var item = table.Resolve(root["First"]) as PdfReadDictionary; item is not null; item = table.Resolve(item["Next"]) as PdfReadDictionary)
            titles.Add(PdfTextString.Decode(Assert.IsType<PdfReadString>(table.Resolve(item["Title"])).Bytes));
        return titles;
    }

    private static PdfFile File(int pages) => PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(pages));

    private static MergePackInput Pack()
    {
        var input = new MergePackInput("Month-end pack", "Invoice", "Receipt", "Account statement");
        input.Documents[0].File = File(2);
        input.Documents[1].File = File(1);
        input.Documents[2].File = File(3);
        return input;
    }

    private static byte[] Save(PdfDocument document) => ImportedPagesTests.Save(document);

    #endregion

    #endregion
}
