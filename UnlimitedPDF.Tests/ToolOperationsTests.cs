using System.Text;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Each free tool makes the PDF its options ask for: merge order, one picture a page, split parts, pages kept or
/// removed, and HTML with the pictures the visitor added; a bad request comes back as a message (26112201 AC-01, AC-04,
/// AC-09; OWASP A08, A10).
/// </summary>
public class ToolOperationsTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public async Task Merge_joins_the_files_in_the_order_given()
    {
        var result = await ToolOperations.MergeAsync([Numbered("A", 2), Numbered("B", 3)], "merged.pdf");

        Assert.True(result.Succeeded);
        var pdf = result.File!;
        Assert.Equal("merged.pdf", pdf.FileName);
        Assert.Equal(5, pdf.PageCount);
        PdfStructureChecker.AssertValid(pdf.Bytes);
        Assert.Equal(["A1", "A2", "B1", "B2", "B3"], SplitFixtures.PageTexts(pdf.Bytes));
    }

    [Fact]
    public async Task Merge_with_no_files_gives_a_message()
    {
        var result = await ToolOperations.MergeAsync([], "merged.pdf");

        Assert.False(result.Succeeded);
        Assert.Equal("Add at least one PDF.", result.Error);
    }

    [Theory]
    [InlineData(PdfPageSize.A4, PictureFit.InsideMargins)]
    [InlineData(PdfPageSize.Letter, PictureFit.EdgeToEdge)]
    public async Task Images_to_pdf_puts_one_picture_on_each_page(PdfPageSize size, PictureFit fit)
    {
        var pictures = new[]
        {
            PdfImage.FromBytes(new JpegBuilder { Width = 640, Height = 480 }.Build()),
            PdfImage.FromBytes(new PngBuilder { Width = 8, Height = 8 }.Build()),
            PdfImage.FromBytes(new JpegBuilder { Width = 300, Height = 600 }.Build()),
        };

        var result = await ToolOperations.ImagesToPdfAsync(pictures, size, fit, "pictures.pdf");

        Assert.True(result.Succeeded, result.Error);
        PdfStructureChecker.AssertValid(result.File!.Bytes);
        var file = PdfFile.FromBytes(result.File.Bytes);
        Assert.Equal(3, file.PageCount);
        Assert.Equal(3, result.File.PageCount);
        var pages = PdfPageReader.Read(result.File.Bytes);
        var (width, height) = size.ToDimensions();
        Assert.All(pages, page => Assert.Equal(1, ImageDraws(page.Content)));
        for (int i = 0; i < file.PageCount; i++)
        {
            var (w, h) = UnlimitedPDF.Models.Internal.Rendering.PdfPageRenderer.Size(file, i)!.Value;
            Assert.Equal(width, w, 1);
            Assert.Equal(height, h, 1);
        }
    }

    [Fact]
    public async Task Split_every_n_pages_makes_numbered_parts()
    {
        var result = await ToolOperations.SplitEveryAsync(Numbered("P", 10), 4, "handbook");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["handbook-1.pdf", "handbook-2.pdf", "handbook-3.pdf"], result.Files.Select(f => f.FileName));
        Assert.Equal([(1, 4), (5, 8), (9, 10)], result.Files.Select(f => (f.FirstPage, f.LastPage)));
        Assert.Equal(["P9", "P10"], SplitFixtures.PageTexts(result.Files[2].Bytes));
        Assert.All(result.Files, f => PdfStructureChecker.AssertValid(f.Bytes));
    }

    [Fact]
    public async Task Split_by_ranges_makes_one_part_per_range()
    {
        var result = await ToolOperations.SplitRangesAsync(Numbered("P", 6), "1-2, 5-6, 3", "report");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal([2, 2, 1], result.Files.Select(f => f.PageCount));
        Assert.Equal(["P5", "P6"], SplitFixtures.PageTexts(result.Files[1].Bytes));
    }

    [Fact]
    public async Task Extract_keeps_the_pages_picked_in_order()
    {
        var result = await ToolOperations.ExtractAsync(Numbered("P", 8), "1, 3-5", "contract-extract.pdf");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(4, result.File!.PageCount);
        Assert.Equal(["P1", "P3", "P4", "P5"], SplitFixtures.PageTexts(result.File.Bytes));
    }

    [Fact]
    public async Task Remove_drops_the_pages_marked_and_keeps_the_rest_in_order()
    {
        var result = await ToolOperations.RemoveAsync(Numbered("P", 6), "2, 6", "scan-trimmed.pdf");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(4, result.File!.PageCount);
        Assert.Equal(["P1", "P3", "P4", "P5"], SplitFixtures.PageTexts(result.File.Bytes));
    }

    [Theory]
    [InlineData("", "The text holds no page numbers.")]
    [InlineData("1-20", null)]
    public async Task A_bad_page_list_gives_a_message(string pages, string? expected)
    {
        var result = await ToolOperations.ExtractAsync(Numbered("P", 8), pages, "x.pdf");

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        if (expected is not null) Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task Removing_every_page_gives_a_message()
    {
        var result = await ToolOperations.RemoveAsync(Numbered("P", 3), "1-3", "x.pdf");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void The_html_scan_lists_each_picture_once_and_marks_web_addresses()
    {
        const string Html = """
            <img src="images/logo.png" alt="Logo"><img src="banner.JPG?v=2" alt="Banner">
            <img src="images/logo.png" alt="Again"><img src="https://example.com/a.png" alt="Web">
            <img src="data:image/png;base64,iVBORw0KGgo=" alt="Inline">
            """;

        var pictures = HtmlPictureScan.Scan(Html);

        Assert.Equal(["images/logo.png", "banner.JPG?v=2", "https://example.com/a.png"], pictures.Select(p => p.Reference));
        Assert.Equal(["logo.png", "banner.JPG", "a.png"], pictures.Select(p => p.FileName));
        Assert.Equal([false, false, true], pictures.Select(p => p.IsWebAddress));
    }

    [Fact]
    public async Task Html_to_pdf_uses_the_added_pictures_and_lists_the_ones_left_out()
    {
        const string Html = """
            <h1>Invoice 1042</h1><img src="images/logo.png" alt="Logo"><img src="banner.jpg" alt="Banner">
            <img src="https://example.com/a.png" alt="Web">
            """;
        var added = new Dictionary<string, byte[]> { ["LOGO.png"] = new PngBuilder { Width = 8, Height = 8 }.Build() };

        var result = await ToolOperations.HtmlToPdfAsync(Html, added, PdfPageSize.A4, "invoice.pdf");

        Assert.True(result.Succeeded, result.Error);
        PdfStructureChecker.AssertValid(result.File!.Bytes);
        Assert.Equal(1, result.File.PageCount);
        Assert.Equal(["banner.jpg", "https://example.com/a.png"], result.LeftOut);
        Assert.Equal(1, ImageDraws(PdfPageReader.Read(result.File.Bytes)[0].Content));
    }

    [Fact]
    public async Task Html_script_is_not_run_and_the_pdf_is_still_made()
    {
        var result = await ToolOperations.HtmlToPdfAsync("<p>Hi</p><script>alert(1)</script>", new Dictionary<string, byte[]>(), PdfPageSize.A4, "x.pdf");

        Assert.True(result.Succeeded, result.Error);
        Assert.DoesNotContain("alert", Encoding.Latin1.GetString(result.File!.Bytes), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static int ImageDraws(string content) => System.Text.RegularExpressions.Regex.Matches(content, @"/\S+ Do Q").Count;

    private static PdfFile Numbered(string prefix, int pages)
    {
        var document = new PdfDocument();
        for (int i = 1; i <= pages; i++) document.AddPage().AddText($"{prefix}{i}", 72, 720);
        using var stream = new MemoryStream();
        document.Save(stream);
        return PdfFile.FromBytes(stream.ToArray());
    }

    #endregion

    #endregion
}
