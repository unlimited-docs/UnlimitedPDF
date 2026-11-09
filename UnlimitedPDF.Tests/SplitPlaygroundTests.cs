using System.IO.Compression;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The playground's Split demos build the parts they list and pack them into one zip (WR-037, AC-15).
/// </summary>
public class SplitPlaygroundTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Every_n_pages_demo_lists_each_part_with_its_pages()
    {
        var input = new SplitInput { File = new MergeFileInput("report.pdf", File(5)), PagesPerPart = 2 };

        var parts = SplitEveryNSample.Build(input);

        Assert.Equal(new[] { "1-2", "3-4", "5-5" }, parts.Select(p => $"{p.Part.FirstPage}-{p.Part.LastPage}"));
        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf", "part-3.pdf" }, parts.Select(p => p.Part.FileName));
        foreach (var part in parts)
        {
            PdfStructureChecker.AssertValid(part.Bytes);
            Assert.Equal(SplitFixtures.Texts(part.Part.FirstPage, part.Part.LastPage), SplitFixtures.PageTexts(part.Bytes));
        }
    }

    [Fact]
    public void Page_ranges_demo_makes_one_part_per_range_in_order()
    {
        var input = new SplitInput { File = new MergeFileInput("report.pdf", File(5)), Ranges = "4-5, 1, 2-4" };

        var parts = SplitRangesSample.Build(input);

        Assert.Equal(new[] { SplitFixtures.Texts(4, 5), SplitFixtures.Texts(1, 1), SplitFixtures.Texts(2, 4) },
            parts.Select(p => SplitFixtures.PageTexts(p.Bytes)));
    }

    [Theory]
    [InlineData("", "Enter the pages")]
    [InlineData("1-9", "past the last page")]
    [InlineData("3-1", "runs backwards")]
    public void Page_ranges_demo_explains_a_bad_range_list(string ranges, string message)
    {
        var input = new SplitInput { File = new MergeFileInput("report.pdf", File(5)), Ranges = ranges };

        var exception = Assert.Throws<InvalidOperationException>(() => SplitRangesSample.Build(input));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demos_without_a_file_ask_for_one()
    {
        Assert.Throws<InvalidOperationException>(() => SplitEveryNSample.Build(new SplitInput()));
        Assert.Throws<InvalidOperationException>(() => SplitRangesSample.Build(new SplitInput()));
    }

    [Fact]
    public void Catalog_lists_the_split_category_right_after_merge()
    {
        var categories = SampleCatalog.Categories;
        var split = categories[2];

        Assert.Equal("Merge", categories[1].Title);
        Assert.Equal("Split", split.Title);
        Assert.Equal(new[] { "Every N pages", "Page ranges" }, split.Demos.Select(demo => demo.Title));
        Assert.Equal(new[] { "playground/split/every-n-pages", "playground/split/page-ranges" }, split.Demos.Select(demo => demo.Route));
        Assert.Equal(new[] { "SplitEveryNSample.cs", "SplitRangesSample.cs" }, split.Demos.Select(demo => demo.SourceFile));
        Assert.All(split.Demos, demo => Assert.EndsWith(".zip", demo.FileName, StringComparison.Ordinal));
    }

    [Fact]
    public void Default_inputs_split_the_built_in_sample()
    {
        var file = PdfFile.FromBytes(ImportedPagesTests.Save(EmployeeDirectorySample.Build(new EmployeeDirectoryInput())));
        var input = new SplitInput { File = new MergeFileInput("Employee directory", file) };

        var everyTwo = SplitEveryNSample.Build(input);
        var ranges = SplitRangesSample.Build(input);

        Assert.True(file.PageCount >= 4, "The built-in sample has enough pages for the default ranges.");
        Assert.Equal((file.PageCount + 1) / 2, everyTwo.Count);
        Assert.Equal(new[] { "1-2", "3-3", "2-4" }, ranges.Select(p => $"{p.Part.FirstPage}-{p.Part.LastPage}"));
        Assert.All(everyTwo.Concat(ranges), part => PdfStructureChecker.AssertValid(part.Bytes));
    }

    [Fact]
    public void Zip_holds_every_part_under_its_file_name()
    {
        var parts = SplitEveryNSample.Build(new SplitInput { File = new MergeFileInput("report.pdf", File(3)), PagesPerPart = 1 });

        using var zip = new ZipArchive(new MemoryStream(SplitPartFile.Zip(parts)), ZipArchiveMode.Read);

        Assert.Equal(parts.Select(p => p.Part.FileName), zip.Entries.Select(e => e.FullName));
        foreach (var (entry, part) in zip.Entries.Zip(parts))
        {
            using var data = new MemoryStream();
            using (var stream = entry.Open()) stream.CopyTo(data);
            Assert.Equal(part.Bytes, data.ToArray());
        }
    }

    #endregion

    #region Private Methods

    private static PdfFile File(int pages) => PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(pages));

    #endregion

    #endregion
}
