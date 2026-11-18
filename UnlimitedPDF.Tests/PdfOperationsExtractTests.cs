using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// <see cref="PdfOperations.Extract(PdfFile, IEnumerable{PdfPageRange}, Stream)"/> and
/// <see cref="PdfOperations.Remove(PdfFile, IEnumerable{PdfPageRange}, Stream)"/>: chosen pages in the order given, or
/// the rest in source order; what the new PDF keeps; every argument checked before any output (WR-040, AC-01, AC-02,
/// AC-04, AC-09, AC-10, AC-13).
/// </summary>
public class PdfOperationsExtractTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Extract_writes_the_chosen_pages_in_the_order_given_with_repeats()
    {
        var pdf = Extract(Source(10), "9,2,2-3");

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(new[] { "Page 9", "Page 2", "Page 2", "Page 3" }, SplitFixtures.PageTexts(pdf));
    }

    [Theory]
    [InlineData("10,1,1")]
    [InlineData("1,10")]
    [InlineData("10, 1")]
    public void Remove_writes_the_other_pages_in_source_order_whatever_the_order_or_repeats(string text)
    {
        var pdf = Remove(Source(10), text);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(SplitFixtures.Texts(2, 9), SplitFixtures.PageTexts(pdf));
    }

    [Fact]
    public void Remove_with_a_descending_range_drops_those_pages()
    {
        Assert.Equal(new[] { "Page 1", "Page 5", "Page 6" }, SplitFixtures.PageTexts(Remove(Source(6), "4-2")));
    }

    [Fact]
    public void A_path_is_created_or_overwritten()
    {
        string path = Path.Combine(Path.GetTempPath(), $"extract-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("old content that is longer than nothing"));
        try
        {
            var file = PdfFile.FromBytes(Source(5));

            PdfOperations.Extract(file, PdfPageRange.Parse("2-3"), path);
            Assert.Equal(Extract(Source(5), "2-3"), File.ReadAllBytes(path));

            PdfOperations.Remove(file, PdfPageRange.Parse("2-3"), path);
            Assert.Equal(Remove(Source(5), "2-3"), File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_stream_is_written_from_its_position_and_left_open()
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 1, 2, 3 });

        PdfOperations.Extract(PdfFile.FromBytes(Source(3)), PdfPageRange.Parse("1"), stream);

        Assert.True(stream.CanWrite);
        var written = stream.ToArray();
        Assert.Equal(new byte[] { 1, 2, 3 }, written.Take(3));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(written, 3, 5));
    }

    [Fact]
    public void The_loaded_file_is_not_changed()
    {
        var file = PdfFile.FromBytes(Source(10));
        var ranges = PdfPageRange.Parse("2,5,7-9");
        var first = Extract(file, ranges);

        Remove(file, PdfPageRange.Parse("1"));
        var document = new PdfDocument();
        document.AddPages(file, ranges);
        ImportedPagesTests.Save(document);

        Assert.Equal(first, Extract(file, ranges));
    }

    [Fact]
    public void Bookmarks_pointing_at_kept_pages_stay_at_their_own_levels_with_no_wrapper()
    {
        var pdf = Extract(SplitFixtures.Bookmarked(), "4-5,8");

        Assert.Equal(new[] { "Sales", "Costs" }, SplitFixtures.Outline(pdf).Select(b => b.Title));
        var sales = SplitFixtures.Outline(pdf)[0];
        Assert.Equal(1, sales.TargetPage);
        Assert.Equal(("Europe", (int?)2), (Assert.Single(sales.Children).Title, Assert.Single(sales.Children).TargetPage));
        Assert.Equal(3, SplitFixtures.Outline(pdf)[1].TargetPage);
    }

    [Fact]
    public void Remove_keeps_the_bookmarks_of_the_pages_left()
    {
        var pdf = Remove(SplitFixtures.Bookmarked(), "1,8");

        Assert.Equal(new[] { ("Sales", (int?)3) }, SplitFixtures.Outline(pdf).Select(b => (b.Title, b.TargetPage)));
    }

    [Fact]
    public void Document_information_and_metadata_are_kept()
    {
        var source = ReaderFixtureBuilder.Document(3, catalogEntries: "/Metadata 42 0 R ")
            .AddObject(40, "<< /Title (Annual Report) /Author (Jane) /Subject (Results) /Keywords (sales) >>")
            .AddStream(42, "/Type /Metadata /Subtype /XML", Encoding.ASCII.GetBytes("<x:xmpmeta/>"))
            .ToBytes(trailerEntries: "/Root 1 0 R /Info 40 0 R");

        foreach (var pdf in new[] { Extract(source, "2"), Remove(source, "2") })
        {
            var output = PdfFile.FromBytes(pdf);
            var info = Assert.IsType<PdfReadDictionary>(output.Source.Objects.Resolve(output.Source.Objects.Trailer["Info"]));
            Assert.Equal("Annual Report", output.Title);
            Assert.Equal(new[] { "Title", "Author", "Subject", "Keywords" }, info.Keys);
            Assert.IsType<PdfReadStream>(output.Source.Objects.Resolve(output.Source.Catalog["Metadata"]));
        }
    }

    [Fact]
    public void A_page_past_the_end_fails_before_any_output_and_names_the_page_count()
    {
        string path = Path.Combine(Path.GetTempPath(), $"extract-{Guid.NewGuid():N}.pdf");
        byte[] existing = Encoding.ASCII.GetBytes("existing");
        File.WriteAllBytes(path, existing);
        try
        {
            var file = PdfFile.FromBytes(Source(10));

            var extract = Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Extract(file, PdfPageRange.Parse("2,5,12"), path));
            var remove = Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Remove(file, PdfPageRange.Parse("12"), path));

            Assert.Equal("ranges", extract.ParamName);
            Assert.Contains("Page range 3 (12)", extract.Message);
            Assert.Contains("last page, 10", remove.Message);
            Assert.Equal(existing, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_empty_selection_and_removing_every_page_fail_before_any_output()
    {
        string path = Path.Combine(Path.GetTempPath(), $"extract-{Guid.NewGuid():N}.pdf");
        byte[] existing = Encoding.ASCII.GetBytes("existing");
        File.WriteAllBytes(path, existing);
        try
        {
            var file = PdfFile.FromBytes(Source(4));

            Assert.Equal("ranges", Assert.Throws<ArgumentException>(() => PdfOperations.Extract(file, Array.Empty<PdfPageRange>(), path)).ParamName);
            Assert.Equal("ranges", Assert.Throws<ArgumentException>(() => PdfOperations.Remove(file, Array.Empty<PdfPageRange>(), path)).ParamName);
            var all = Assert.Throws<ArgumentException>(() => PdfOperations.Remove(file, PdfPageRange.Parse("4-1, 2"), path));

            Assert.Equal("ranges", all.ParamName);
            Assert.Contains("leaves no pages", all.Message);
            Assert.Equal(existing, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Null_arguments_are_rejected_by_name()
    {
        var file = PdfFile.FromBytes(Source(2));
        var ranges = PdfPageRange.Parse("1");
        using var stream = new MemoryStream();

        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => PdfOperations.Extract(null!, ranges, stream)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentNullException>(() => PdfOperations.Extract(file, null!, stream)).ParamName);
        Assert.Equal("output", Assert.Throws<ArgumentNullException>(() => PdfOperations.Extract(file, ranges, (Stream)null!)).ParamName);
        Assert.Equal("outputPath", Assert.Throws<ArgumentNullException>(() => PdfOperations.Extract(file, ranges, (string)null!)).ParamName);
        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => PdfOperations.Remove(null!, ranges, stream)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentNullException>(() => PdfOperations.Remove(file, new PdfPageRange[] { null! }, stream)).ParamName);
        Assert.Equal("output", Assert.Throws<ArgumentNullException>(() => PdfOperations.Remove(file, ranges, (Stream)null!)).ParamName);
        Assert.Equal("outputPath", Assert.Throws<ArgumentNullException>(() => PdfOperations.Remove(file, ranges, (string)null!)).ParamName);
        Assert.Equal(0, stream.Length);
    }

    #endregion

    #region Internal Methods

    internal static byte[] Extract(byte[] source, string pages) => Extract(PdfFile.FromBytes(source), PdfPageRange.Parse(pages));

    internal static byte[] Extract(PdfFile file, IEnumerable<PdfPageRange> ranges)
    {
        using var stream = new MemoryStream();
        PdfOperations.Extract(file, ranges, stream);
        return stream.ToArray();
    }

    internal static byte[] Remove(byte[] source, string pages) => Remove(PdfFile.FromBytes(source), PdfPageRange.Parse(pages));

    internal static byte[] Remove(PdfFile file, IEnumerable<PdfPageRange> ranges)
    {
        using var stream = new MemoryStream();
        PdfOperations.Remove(file, ranges, stream);
        return stream.ToArray();
    }

    internal static byte[] Source(int pages) => ReaderFixtureBuilder.DocumentBytes(pages);

    #endregion

    #endregion
}
