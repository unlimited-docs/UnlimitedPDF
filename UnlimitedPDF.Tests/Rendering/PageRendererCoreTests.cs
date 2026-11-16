using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// The page renderer's core: page geometry, paths, colors, line styles, clipping, and the pages it does not draw
/// (26111701 AC-12, A06, A10).
/// </summary>
public class PageRendererCoreTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_page_is_drawn_at_its_media_box_size_with_y_turned_down()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("1 0 0 rg 10 20 30 40 re f"));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(595, page.Width);
        Assert.Equal(842, page.Height);
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 595 842\"", page.Svg);
        Assert.Contains("<path d=\"M10 20L40 20L40 60L10 60Z\" transform=\"matrix(1 0 0 -1 0 842)\" fill=\"#ff0000\"/>", page.Svg);
        Assert.Equal(1, page.Counts.Paths);
    }

    [Fact]
    public void Rotated_pages_swap_their_size_and_turn_the_drawing()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 10 10 re f", pageEntries: "/Rotate 90"));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(842, page.Width);
        Assert.Equal(595, page.Height);
        Assert.Contains("transform=\"matrix(0 1 1 0 0 0)\"", page.Svg);
    }

    [Fact]
    public void Transformations_concatenate_and_are_restored()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("q 2 0 0 2 100 100 cm 0 0 m 5 5 l S Q 0 0 m 1 1 l S"));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains("<path d=\"M0 0L5 5\" transform=\"matrix(2 0 0 -2 100 742)\" fill=\"none\" stroke=\"#000000\" stroke-width=\"1\"", page.Svg);
        Assert.Contains("<path d=\"M0 0L1 1\" transform=\"matrix(1 0 0 -1 0 842)\" fill=\"none\"", page.Svg);
    }

    [Fact]
    public void Curves_closing_and_line_styles_are_drawn()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(
            "0 0 1 RG 2.5 w 1 J 1 j [3 2] 1 d 10 10 m 20 30 40 30 50 10 c 60 0 70 0 v 80 10 90 10 y h S"));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains("d=\"M10 10C20 30 40 30 50 10C50 10 60 0 70 0C80 10 90 10 90 10Z\"", page.Svg);
        Assert.Contains("stroke=\"#0000ff\" stroke-width=\"2.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-dasharray=\"3 2\" stroke-dashoffset=\"1\"", page.Svg);
    }

    [Theory]
    [InlineData("0.5 g", "#808080")]
    [InlineData("0 0 0 0 k", "#ffffff")]
    [InlineData("0 1 1 0 k", "#ff0000")]
    [InlineData("0.2 0.4 0.6 rg", "#336699")]
    public void Device_colors_become_rgb(string color, string expected)
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage($"{color} 0 0 5 5 re f"));

        Assert.Contains($"fill=\"{expected}\"", page.Svg);
    }

    [Fact]
    public void Clipping_paths_wrap_the_drawing_until_the_state_is_restored()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("q 0 0 100 100 re W n 0 0 500 500 re f Q 0 0 1 1 re f"));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains("<clipPath id=\"t-c0\"><path d=\"M0 0L100 0L100 100L0 100Z\"", page.Svg);
        int open = page.Svg!.IndexOf("<g clip-path=\"url(#t-c0)\">", StringComparison.Ordinal);
        int close = page.Svg.IndexOf("</g>", open, StringComparison.Ordinal);
        int clipped = page.Svg.IndexOf("M0 0L500 0", StringComparison.Ordinal);
        int after = page.Svg.IndexOf("M0 0L1 0", StringComparison.Ordinal);
        Assert.True(open < clipped && clipped < close && close < after);
    }

    [Fact]
    public void Even_odd_fills_and_clips_keep_their_rule()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 10 10 re W* n 0 0 10 10 re f*"));

        Assert.Contains("clip-rule=\"evenodd\"", page.Svg);
        Assert.Contains("fill-rule=\"evenodd\"", page.Svg);
    }

    [Fact]
    public void Tables_the_library_writes_are_drawn_with_a_path_per_background_and_border()
    {
        var document = new PdfDocument();
        document.AddPage().AddTable(50, 800, 300).GetOrCreateCell(0, 0).Text = "A";
        byte[] pdf = Save(document);

        var file = PdfFile.FromBytes(pdf);
        var page = PdfPageRenderer.Render(file, 0, "x-");

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(2, Regex.Matches(page.Svg!, "<path d=\"M50 ").Count); // the cell's background and its border
        Assert.Contains("fill=\"none\" stroke=", page.Svg);
        Assert.Equal(new[] { "A" }, page.TextLines);
    }

    [Theory]
    [InlineData("0 0 m 1 1 l 2 2 zz S", "zz")]
    [InlineData("/Gs1 gs 0 0 1 1 re f", "gs")]
    [InlineData("/P1 sh", "sh")]
    [InlineData("/Cs1 cs 0 0 1 1 re f", "cs")]
    public void Operators_it_does_not_draw_leave_the_page_undrawn_with_the_reason(string content, string op)
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(content));

        Assert.False(page.IsDrawn);
        Assert.Null(page.Svg);
        Assert.Contains($"operator {op}", page.Reason);
        Assert.Empty(page.TextLines);
    }

    [Fact]
    public void Inline_images_and_damaged_content_are_not_drawn()
    {
        Assert.False(RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("BI /W 1 /H 1 /BPC 8 /CS /G ID x EI")).IsDrawn);
        Assert.False(RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 m ) l")).IsDrawn);
        Assert.False(RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 1 re f")).IsDrawn);
        Assert.False(RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("1e5 0 m 1 1 l S")).IsDrawn);
    }

    [Fact]
    public void A_content_stream_with_an_unsupported_filter_is_not_drawn()
    {
        var pdf = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>")
            .AddStream(4, "/Filter /LZWDecode", new byte[] { 1, 2, 3 })
            .ToBytes();

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.False(page.IsDrawn);
        Assert.Contains("LZWDecode", page.Reason);
    }

    [Fact]
    public void Each_work_bound_stops_the_page_within_it()
    {
        var tight = PageRenderLimits.Default with { MaxOperators = 50, MaxPathSegments = 50, MaxStateDepth = 4, MaxOutputCharacters = 400 };

        Assert.Contains("too many drawing operators",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(string.Concat(Enumerable.Repeat("0 0 m ", 60))), tight).Reason);
        Assert.Contains("too many path segments",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(string.Concat(Enumerable.Repeat("0 0 1 1 re ", 20))), tight).Reason);
        Assert.Contains("nests saved states",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("q q q q q Q Q Q Q Q"), tight).Reason);
        Assert.Contains("drawing is too large",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(string.Concat(Enumerable.Repeat("0 0 1 1 re f ", 8))), tight).Reason);
    }

    [Fact]
    public void Unbalanced_restores_are_ignored_and_open_states_are_closed()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("Q Q q 0 0 9 9 re W n q 0 0 5 5 re W n 0 0 1 1 re f"));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(CountOf(page.Svg!, "<g "), CountOf(page.Svg!, "</g>"));
    }

    #endregion

    #region Private Methods

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static byte[] Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    #endregion

    #endregion
}
