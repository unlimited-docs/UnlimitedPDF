using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The playground's Extract pages and Remove pages demos write the PDF their page list asks for, explain a bad list,
/// and sit in the Split and extract tab's own group (WR-040, AC-16, D-018, D-019).
/// </summary>
public class ExtractPlaygroundTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("2, 4, 1-2", new[] { 2, 4, 1, 2 })]
    [InlineData("5-3", new[] { 5, 4, 3 })]
    [InlineData(" 1 ", new[] { 1 })]
    public void Extract_pages_writes_the_pages_in_the_order_written(string pages, int[] expected)
    {
        var pdf = ExtractPagesSample.Build(new PageSelectionInput { File = new MergeFileInput("five.pdf", File(5)), Pages = pages });

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(expected.Select(n => $"Page {n}"), SplitFixtures.PageTexts(pdf));
    }

    [Theory]
    [InlineData("2-3", new[] { 1, 4, 5 })]
    [InlineData("5, 1, 1", new[] { 2, 3, 4 })]
    public void Remove_pages_writes_the_pages_left_in_their_order(string pages, int[] expected)
    {
        var pdf = RemovePagesSample.Build(new PageSelectionInput { File = new MergeFileInput("five.pdf", File(5)), Pages = pages });

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(expected.Select(n => $"Page {n}"), SplitFixtures.PageTexts(pdf));
    }

    [Theory]
    [InlineData("", "The text holds no page numbers.")]
    [InlineData("2, last", "Item 2 (\"last\") is not a page number or a range such as 7-9.")]
    [InlineData("2, 9", "Ranges: page range 2 (9) goes past the file's last page, 5.")]
    public void A_bad_page_list_is_explained_in_plain_words(string pages, string message)
    {
        var input = new PageSelectionInput { File = new MergeFileInput("five.pdf", File(5)), Pages = pages };

        Assert.Equal(message, ErrorText.Plain(Record.Exception(() => ExtractPagesSample.Build(input))!));
        Assert.Equal(message, ErrorText.Plain(Record.Exception(() => RemovePagesSample.Build(input))!));
    }

    [Fact]
    public void Removing_every_page_is_explained()
    {
        var input = new PageSelectionInput { File = new MergeFileInput("five.pdf", File(5)), Pages = "1-5" };

        Assert.Equal("Ranges: removing these pages leaves no pages.", ErrorText.Plain(Assert.Throws<ArgumentException>(() => RemovePagesSample.Build(input))));
    }

    [Fact]
    public void Demos_without_a_file_ask_for_one()
    {
        Assert.Equal("Choose a PDF.", Assert.Throws<InvalidOperationException>(() => ExtractPagesSample.Build(new PageSelectionInput { Pages = "1" })).Message);
        Assert.Equal("Choose a PDF.", Assert.Throws<InvalidOperationException>(() => RemovePagesSample.Build(new PageSelectionInput { Pages = "1" })).Message);
    }

    [Fact]
    public void Default_lists_fit_the_built_in_report()
    {
        var report = PdfFile.FromBytes(ImportedPagesTests.Save(HeadersFootersSample.Build(HeadersFootersInput.Report())));

        var extracted = ExtractPagesSample.Build(new PageSelectionInput { File = new MergeFileInput("Report", report), Pages = "4, 1-2" });
        var remaining = RemovePagesSample.Build(new PageSelectionInput { File = new MergeFileInput("Report", report), Pages = "1-4" });

        // The card pictures are the first pages of these outputs: report pages 4 and 5, which no other card shows.
        Assert.Equal(6, report.PageCount);
        Assert.Equal(new[] { "Page 4 of 6", "Page 1 of 6", "Page 2 of 6" }, Footers(extracted));
        Assert.Equal(new[] { "Page 5 of 6", "Page 6 of 6" }, Footers(remaining));
    }

    [Fact]
    public void The_demos_sit_in_the_split_and_extract_tab_in_their_own_group()
    {
        var split = SampleCatalog.Categories.Single(c => c.Id == "split");

        Assert.Equal("Split and extract", split.Title);
        Assert.Equal(new[] { "Split", "Extract and remove" }, split.Groups!.Select(g => g.Title));
        Assert.Equal(new[] { "Extract pages", "Remove pages" }, split.Groups![1].Demos.Select(d => d.Title));
        Assert.Equal(new[] { "playground/split/extract-pages", "playground/split/remove-pages" }, split.Groups![1].Demos.Select(d => d.Route));
        Assert.Equal(new[] { "ExtractPagesSample.cs", "RemovePagesSample.cs" }, split.Groups![1].Demos.Select(d => d.SourceFile));
        Assert.Equal(new[] { "extracted-pages.pdf", "remaining-pages.pdf" }, split.Groups![1].Demos.Select(d => d.FileName));
    }

    [Fact]
    public void The_scenario_pages_use_the_one_call_operations_and_parse_the_text()
    {
        foreach (var (page, call) in new[] { ("Pages/Playground/Split/ExtractPages.razor", "ExtractPagesSample.Build"), ("Pages/Playground/Split/RemovePages.razor", "RemovePagesSample.Build") })
        {
            string razor = DemosFile(page);
            Assert.Contains("BuildFile=\"BuildAsync\"", razor, StringComparison.Ordinal);
            Assert.Contains(call, razor, StringComparison.Ordinal);
            Assert.Contains("aria-describedby=", razor, StringComparison.Ordinal);
        }
        Assert.Contains("PdfOperations.Extract(file, PdfPageRange.Parse(input.Pages), output);", DemosFile("Samples/ExtractPagesSample.cs"), StringComparison.Ordinal);
        Assert.Contains("PdfOperations.Remove(file, PdfPageRange.Parse(input.Pages), output);", DemosFile("Samples/RemovePagesSample.cs"), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relativePath));

    // The "Page n of 6" footer text of each page, as the report wrote it.
    private static string[] Footers(byte[] pdf) => PdfInspector.Read(pdf).PageText()
        .Select(text => System.Text.RegularExpressions.Regex.Match(text, @"Page \d+ of \d+").Value)
        .ToArray();

    private static PdfFile File(int pages) => PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(pages));

    #endregion

    #endregion
}
