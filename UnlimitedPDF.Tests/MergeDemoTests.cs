using UnlimitedPDF.Demo.ConsoleApp;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The console demo's merge builds a valid pack: a cover, two report pages, and the whole invoice, with one bookmark
/// per input (AC-20).
/// </summary>
public class MergeDemoTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Merge_demo_builds_a_valid_pack()
    {
        var invoicePages = PdfFile.FromBytes(ImportedPagesTests.Save(DemoDocument.Build(Fixtures.DemoAssets()))).PageCount;

        var pdf = ImportedPagesTests.Save(MergeDemo.Build(Fixtures.DemoAssets()));
        var merged = PdfFile.FromBytes(pdf);
        var texts = ImportedPagesTests.PageTexts(merged);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(1 + 2 + invoicePages, merged.PageCount);
        Assert.Contains("Pack page 1 of", texts[0], StringComparison.Ordinal);
        Assert.Contains("Results", texts[1], StringComparison.Ordinal);
        Assert.Contains("Outlook", texts[2], StringComparison.Ordinal);
        Assert.DoesNotContain("Pack page", texts[1], StringComparison.Ordinal);
        Assert.True(merged.Source.Catalog.ContainsKey("Outlines"));
    }

    [Fact]
    public void Playground_merge_sample_builds_a_cover_and_the_chosen_ranges()
    {
        var input = new MergeInput { CoverTitle = "Pack (draft)" };
        input.Files.Add(new MergeFileInput("Report", PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(4))) { FirstPage = 2, LastPage = 3 });
        input.Files.Add(new MergeFileInput("Appendix", PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1))));

        var pdf = ImportedPagesTests.Save(MergeSample.Build(input));
        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(pdf));

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(4, texts.Length);
        Assert.Contains("Pack (draft)", texts[0], StringComparison.Ordinal);
        Assert.Contains("Report: pages 2 to 3", texts[0], StringComparison.Ordinal);
        Assert.Contains("4 pages", texts[0], StringComparison.Ordinal);
        Assert.Equal(new[] { "Page 2", "Page 3", "Page 1" }, texts.Skip(1));
    }

    [Fact]
    public void Playground_merge_sample_without_a_cover_is_only_the_files()
    {
        var input = new MergeInput { AddCover = false };
        input.Files.Add(new MergeFileInput("One", PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2))));

        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(ImportedPagesTests.Save(MergeSample.Build(input))));

        Assert.Equal(new[] { "Page 1", "Page 2" }, texts);
    }

    #endregion

    #endregion
}
