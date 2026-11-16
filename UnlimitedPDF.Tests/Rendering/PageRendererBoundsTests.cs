using System.Diagnostics;
using System.IO.Compression;
using UnlimitedPDF.Models.Internal.Reading;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// The page renderer's geometry, paint operators, and line styles in detail, and its bounds against hostile content:
/// every probe ends in a page that is not drawn, quickly and without an escaping exception (26111701 AC-12, A06,
/// A10).
/// </summary>
public class PageRendererBoundsTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("/Rotate 180", 595, 842, "matrix(-1 0 0 1 595 0)")]
    [InlineData("/Rotate 270", 842, 595, "matrix(0 -1 -1 0 842 595)")]
    [InlineData("/Rotate -90", 842, 595, "matrix(0 -1 -1 0 842 595)")]
    [InlineData("/CropBox [10 20 110 220]", 100, 200, "matrix(1 0 0 -1 -10 220)")]
    [InlineData("/CropBox [-1000 -1000 5000 5000]", 595, 842, "matrix(1 0 0 -1 0 842)")]
    public void Rotation_and_crop_box_set_the_size_and_the_page_matrix(string entries, double width, double height, string matrix)
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 1 1 re f", pageEntries: entries));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal((width, height), (page.Width, page.Height));
        Assert.Contains($"transform=\"{matrix}\"", page.Svg);
    }

    [Theory]
    [InlineData("0 0 1 1 re n", 0, null)]
    [InlineData("0 0 m 1 1 l s", 1, "d=\"M0 0L1 1Z\" transform=\"matrix(1 0 0 -1 0 842)\" fill=\"none\" stroke=")]
    [InlineData("0 0 m 1 1 l b", 1, "d=\"M0 0L1 1Z\" transform=\"matrix(1 0 0 -1 0 842)\" fill=\"#000000\" stroke=")]
    [InlineData("0 0 m 1 1 l b*", 1, "fill=\"#000000\" fill-rule=\"evenodd\" stroke=")]
    [InlineData("0 0 m 1 1 l B", 1, "d=\"M0 0L1 1\" transform=\"matrix(1 0 0 -1 0 842)\" fill=\"#000000\" stroke=")]
    [InlineData("0 0 m 1 1 l B*", 1, "fill=\"#000000\" fill-rule=\"evenodd\" stroke=")]
    [InlineData("0 0 m 1 1 l F", 1, "d=\"M0 0L1 1\" transform=\"matrix(1 0 0 -1 0 842)\" fill=\"#000000\"/>")]
    public void Each_paint_operator_fills_strokes_and_closes_as_the_specification_says(string content, int paths, string? expected)
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(content));

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(paths, page.Counts.Paths);
        if (expected is null) Assert.DoesNotContain("<path", page.Svg);
        else Assert.Contains(expected, page.Svg);
    }

    [Theory]
    [InlineData("0.5 G", "stroke=\"#808080\"")]
    [InlineData("0 0 0 0.5 K", "stroke=\"#808080\"")]
    [InlineData("0.25 0 0 0.5 K", "stroke=\"#408080\"")]
    [InlineData("0 w", "stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"")]
    [InlineData("3 M", "stroke-miterlimit=\"3\"")]
    [InlineData("2 J", "stroke-linecap=\"square\"")]
    [InlineData("2 j", "stroke-linejoin=\"bevel\"")]
    public void Stroke_colors_and_line_styles_reach_the_stroke(string setting, string expected)
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage($"{setting} 0 0 m 1 1 l S"));

        Assert.Contains(expected, page.Svg);
        Assert.Contains("fill=\"none\"", page.Svg);
    }

    [Fact]
    public void A_clipping_path_that_is_also_filled_is_painted_before_the_clip_applies()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 5 5 re W f 0 0 9 9 re f"));

        string svg = page.Svg!;
        int ownPaint = svg.IndexOf("<path d=\"M0 0L5 0", StringComparison.Ordinal);
        int clip = svg.IndexOf("<g clip-path=", StringComparison.Ordinal);
        int next = svg.IndexOf("<path d=\"M0 0L9 0", StringComparison.Ordinal);
        Assert.True(ownPaint >= 0 && ownPaint < clip && clip < next);
    }

    [Fact]
    public void The_streams_of_a_contents_array_share_one_graphics_state()
    {
        var pdf = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents [4 0 R 5 0 R 6 0 R] >>")
            .AddStream(4, string.Empty, Encoding.ASCII.GetBytes("q 2 0 0 2 0 0 cm"))
            .AddStream(5, string.Empty, Encoding.ASCII.GetBytes("0 0 10 10 re f"))
            .AddStream(6, string.Empty, Encoding.ASCII.GetBytes("Q 0 0 1 1 re f"))
            .ToBytes();

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains("d=\"M0 0L10 0L10 10L0 10Z\" transform=\"matrix(2 0 0 -2 0 100)\"", page.Svg);
        Assert.Contains("d=\"M0 0L1 0L1 1L0 1Z\" transform=\"matrix(1 0 0 -1 0 100)\"", page.Svg);
    }

    [Theory]
    [InlineData("h f", "doesn't start with a move")]
    [InlineData("10 10 l S", "doesn't start with a move")]
    [InlineData("0 0 m 2 0 0 2 0 0 cm 1 1 l S", "not finished before the operator cm")]
    [InlineData("0 0 m q", "not finished before the operator q")]
    public void Malformed_paths_leave_the_page_undrawn(string content, string reason)
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(content));

        Assert.False(page.IsDrawn);
        Assert.Contains(reason, page.Reason);
    }

    [Theory]
    [InlineData("/Rotate 90.5")]
    [InlineData("/Rotate 45")]
    public void Rotations_that_are_not_whole_quarter_turns_leave_the_page_undrawn(string entries)
    {
        Assert.Contains("rotation", RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 1 1 re f", pageEntries: entries)).Reason);
    }

    [Fact]
    public void A_long_operator_name_is_shortened_in_the_reason()
    {
        string name = new('x', 5000);

        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage($"{name}"));

        Assert.True(page.Reason!.Length < 200);
        Assert.Contains(new string('x', 32) + "...", page.Reason);
    }

    [Fact]
    public void Hostile_dash_patterns_numbers_and_stroke_counts_stop_within_the_output_bound()
    {
        string longDash = "[" + string.Concat(Enumerable.Repeat("1 ", 33)) + "] 0 d 0 0 m 1 1 l S";
        Assert.Contains("dash pattern", RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(longDash)).Reason);

        Assert.Contains("out of range",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("1" + new string('0', 300) + " 0 0 1 0 0 cm 0 0 m 1 1 l S")).Reason);

        var tight = PageRenderLimits.Default with { MaxOutputCharacters = 10_000 };
        string strokes = "[" + string.Concat(Enumerable.Repeat("1 ", 32)) + "] 0 d" + string.Concat(Enumerable.Repeat(" 0 0 m 1 1 l S", 5000));
        var watch = Stopwatch.StartNew();
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(strokes), tight);
        Assert.Contains("drawing is too large", page.Reason);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"Took {watch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void Too_many_operands_deep_clips_and_too_much_content_stop_the_page()
    {
        Assert.Contains("too many operands",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(string.Concat(Enumerable.Repeat("0 ", 4097)) + "n")).Reason);
        Assert.Contains("clipping paths too deeply",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(string.Concat(Enumerable.Repeat("0 0 1 1 re W n ", 257)))).Reason);
        Assert.Contains("content is too large",
            RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0 0 1 1 re f 0 0 1 1 re f"), PageRenderLimits.Default with { MaxContentBytes = 10 }).Reason);
    }

    [Theory]
    [InlineData(12, 536870910)]
    [InlineData(12, 300000000)]
    [InlineData(12, 100000000)]
    [InlineData(2, 536870910)]
    public void Predictor_columns_far_beyond_the_data_fail_fast_without_an_escaping_exception(int predictor, int columns)
    {
        var pdf = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>")
            .AddStream(4, $"/Filter /FlateDecode /DecodeParms << /Predictor {predictor} /Columns {columns} >>", Deflate(Encoding.ASCII.GetBytes("0 0 1 1 re f")))
            .ToBytes();

        var watch = Stopwatch.StartNew();
        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.False(page.IsDrawn);
        Assert.Contains("predictor row is longer", page.Reason);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"Took {watch.ElapsedMilliseconds} ms.");
    }

    [Theory]
    [InlineData("/Colors 0")]
    [InlineData("/Colors 33")]
    [InlineData("/BitsPerComponent 3")]
    [InlineData("/Columns 0")]
    public void Predictor_parameters_out_of_range_are_damaged(string parameter)
    {
        var stream = new PdfReadStream(Dictionary("Filter", new PdfReadName("FlateDecode"), "DecodeParms",
            ParseDictionary($"<< /Predictor 12 {parameter} >>")), Deflate(new byte[] { 0, 1 }));

        var exception = Assert.Throws<PdfReadException>(() => PdfStreamDecoder.Decode(stream, v => v ?? PdfReadNull.Instance, 1000));
        Assert.Contains("out of range", exception.Message);
    }

    [Theory]
    [InlineData("bad\"id")]
    [InlineData("a)b")]
    [InlineData("é")]
    public void An_id_prefix_that_could_break_the_markup_is_refused(string prefix)
    {
        var file = PdfFile.FromBytes(RenderTestFiles.OnePage("0 0 1 1 re f"));

        Assert.Throws<ArgumentException>(() => PdfPageRenderer.Render(file, 0, prefix));
    }

    [Fact]
    public void Small_scale_terms_keep_their_value()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("0.00005 0 0 0.333333 0 0 cm 0 0 m 1000000 1000 l S"));

        Assert.Contains("transform=\"matrix(0.00005 0 0 -0.333333 0 842)\"", page.Svg);
    }

    [Fact]
    public void One_huge_path_stops_at_the_output_bound_while_it_is_built()
    {
        string rectangles = string.Concat(Enumerable.Repeat("0 0 1 1 re ", 20_000)) + "f";
        var watch = Stopwatch.StartNew();

        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(rectangles), PageRenderLimits.Default with { MaxOutputCharacters = 1000 });

        Assert.Contains("drawing is too large", page.Reason);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"Took {watch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void Clips_released_by_restores_do_not_count_toward_the_clip_depth()
    {
        var page = RenderTestFiles.RenderFirst(RenderTestFiles.OnePage(string.Concat(Enumerable.Repeat("q 0 0 1 1 re W n Q ", 300))));

        Assert.True(page.IsDrawn, page.Reason);
    }

    [Fact]
    public void A_compressed_content_stream_that_inflates_past_the_bound_is_too_large()
    {
        var pdf = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>")
            .AddStream(4, "/Filter /FlateDecode", Deflate(Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("0 0 1 1 re f ", 100)))))
            .ToBytes();

        var page = RenderTestFiles.RenderFirst(pdf, PageRenderLimits.Default with { MaxContentBytes = 200 });

        Assert.Contains("content is too large", page.Reason);
    }

    [Fact]
    public void A_clip_without_a_path_is_damaged()
    {
        Assert.Contains("clips without a path", RenderTestFiles.RenderFirst(RenderTestFiles.OnePage("W q 0 0 1 1 re f Q")).Reason);
    }

    [Fact]
    public void Reasons_that_quote_the_pdf_are_kept_short()
    {
        var pdf = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>")
            .AddStream(4, "/Filter /" + new string('X', 5000), new byte[] { 1 })
            .ToBytes();

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.False(page.IsDrawn);
        Assert.True(page.Reason!.Length <= 203, $"Reason is {page.Reason.Length} characters.");
    }

    [Fact]
    public void An_empty_compressed_stream_with_a_predictor_is_an_empty_page()
    {
        var pdf = new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>")
            .AddStream(4, "/Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns 4 >>", Deflate(Array.Empty<byte>()))
            .ToBytes();

        Assert.True(RenderTestFiles.RenderFirst(pdf).IsDrawn);
    }

    #endregion

    #region Private Methods

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true)) zlib.Write(data);
        return output.ToArray();
    }

    private static PdfReadDictionary Dictionary(string key1, PdfReadValue value1, string key2, PdfReadValue value2) =>
        ParseDictionary("<< >>").With(key1, value1).With(key2, value2);

    private static PdfReadDictionary ParseDictionary(string text) =>
        (PdfReadDictionary)new PdfParser(new PdfLexer(Encoding.ASCII.GetBytes(text))).ParseValue();

    #endregion

    #endregion
}

/// <summary>
/// Builds read dictionaries for tests.
/// </summary>
internal static class PdfReadDictionaryTestExtensions
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Sets <paramref name="key"/> and returns the dictionary.
    /// </summary>
    public static PdfReadDictionary With(this PdfReadDictionary dictionary, string key, PdfReadValue value)
    {
        dictionary.Set(key, value);
        return dictionary;
    }

    #endregion

    #endregion
}
