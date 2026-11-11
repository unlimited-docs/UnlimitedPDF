using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Regression tests for the 14-html-to-pdf review findings on the input path: hostile or sloppy input fails with a
/// limit or loads in linear time, never crashing or hanging (A06, A10, PR-018; review F-01 to F-07).
/// </summary>
[Collection(ProcessWideHtmlSettingsCollection.Name)]
public class HtmlReviewRegressionTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Deeply_nested_media_blocks_fail_with_the_nesting_limit()
    {
        // F-01: 20,000 nested @media blocks overflowed the stack and killed the process.
        string css = string.Concat(Enumerable.Repeat("@media print{", 20000));
        PdfHtmlException? caught = null;
        var thread = new Thread(() => caught = Record.Exception(() => PdfHtml.FromString("<style>" + css + "</style>")) as PdfHtmlException);
        thread.Start();
        thread.Join();
        Assert.NotNull(caught);
        Assert.Equal(PdfHtmlErrorReason.NestingTooDeep, caught!.Reason);
    }

    [Fact]
    public void Nested_media_blocks_within_the_limit_still_apply()
    {
        var html = PdfHtml.FromString("<style>@media print { @media all { p { color: red } } }</style><p>x</p>");
        Assert.Equal(1, html.Model.Styles.Single(pair => pair.Key.Name == "p").Value.Color.R);
    }

    [Fact]
    public void Descendant_selectors_over_deep_nesting_match_quickly()
    {
        // F-02: descendant combinators retried every combination of ancestors (over 100 s for this input).
        string html = "<style>p div div div div div div div div div span { color: red }</style>"
            + string.Concat(Enumerable.Repeat("<div>", 250)) + "<span>x</span>";
        var watch = Stopwatch.StartNew();
        var loaded = PdfHtml.FromString(html);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Equal(0, loaded.Model.Styles.Single(pair => pair.Key.Name == "span").Value.Color.R); // no p ancestor
    }

    [Fact]
    public void Selectors_with_child_and_descendant_combinators_still_backtrack_correctly()
    {
        // The nearest matching ancestor is not always the right one: "a > b c" needs the b whose parent is an a.
        var loaded = PdfHtml.FromString("<style>section > article span { color: red }</style><section><article><div><article><span>x</span></article></div></article></section>");
        Assert.Equal(1, loaded.Model.Styles.Single(pair => pair.Key.Name == "span").Value.Color.R);
    }

    [Fact]
    public void First_child_over_many_siblings_is_fast()
    {
        // F-04: each match listed every sibling (74 s for this input).
        string html = "<style>i:first-child { color: red } i:last-child { color: blue }</style><div>" + string.Concat(Enumerable.Repeat("<i>x</i>", 50000)) + "</div>";
        var watch = Stopwatch.StartNew();
        var loaded = PdfHtml.FromString(html);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
        var items = loaded.Model.Styles.Where(pair => pair.Key.Name == "i").ToList();
        Assert.Equal(50000, items.Count);
    }

    [Theory]
    [InlineData("a<!---->", 200000)]
    [InlineData("a</x>", 200000)]
    [InlineData("<i></i>", 100000)]
    public void Split_text_and_moved_table_content_build_in_linear_time(string piece, int count)
    {
        // F-03: joined text was copied on every piece, and content moved before a table searched the whole list.
        string html = (piece == "<i></i>" ? "<table>" : "<p>") + string.Concat(Enumerable.Repeat(piece, count));
        var watch = Stopwatch.StartNew();
        HtmlParserTests.Build(html, out _);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Joined_text_keeps_every_piece()
    {
        Assert.Equal("<p>aaa</p>", HtmlParserTests.Dump(HtmlParserTests.Build("<p>a<!---->a</x>a", out _).Body));
    }

    [Theory]
    [InlineData("&#٣;", "&#٣;")]
    [InlineData("&#x٣;", "&#x٣;")]
    [InlineData("&#65٣;", "A٣;")]
    public void Numeric_references_read_only_ASCII_digits(string text, string expected)
    {
        // F-06: non-ASCII digits threw FormatException.
        var token = Assert.Single(new HtmlTokenizer(text).Tokens());
        Assert.Equal(expected, token.Value);
        PdfHtml.FromString($"<p title='{text}'>{text}</p>");
    }

    [Fact]
    public void Unclosed_formatting_tags_stay_bounded()
    {
        // Repeated "<p><b>x" reopened every unclosed b and failed the nesting limit; browsers keep three.
        var tree = HtmlParserTests.Build(string.Concat(Enumerable.Repeat("<p><b>x", 300)), out _);
        int depth = 0;
        HtmlElement? current = (HtmlElement)tree.Body.Children.Last();
        while (current?.Children.OfType<HtmlElement>().FirstOrDefault() is { } child) { current = child; depth++; }
        Assert.InRange(depth, 1, 4); // three reopened plus the new one, as in the HTML Standard
    }

    [Fact]
    public void The_nesting_limit_counts_author_elements_only()
    {
        HtmlParserTests.Build(string.Concat(Enumerable.Repeat("<div>", 256)), out _);
        Assert.Equal(PdfHtmlErrorReason.NestingTooDeep, Assert.Throws<PdfHtmlException>(() => HtmlParserTests.Build(string.Concat(Enumerable.Repeat("<div>", 257)), out _)).Reason);
    }

    [Theory]
    [InlineData("./a.png:hidden")]
    [InlineData("./a.png::$DATA")]
    [InlineData("img/a.png:x")]
    public void Alternate_data_stream_paths_are_blocked(string src)
    {
        // F-07: "./name:stream" read an NTFS alternate data stream of a file inside the base folder.
        string folder = Path.Combine(Path.GetTempPath(), "updf-ads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "img"));
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "a.png"), PdfHtmlLoadingTests.Png());
            var html = PdfHtml.FromString($"<img src='{src}'>", new PdfHtmlLoadOptions { BaseFolder = folder });
            Assert.Equal(PdfHtmlWarningKind.ResourceBlocked, Assert.Single(html.Warnings).Kind);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void File_names_outside_the_basic_plane_are_found()
    {
        // F-07: each half of a surrogate pair was decoded on its own and the file was never found.
        string folder = Path.Combine(Path.GetTempPath(), "updf-emoji-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "\U0001F600.png"), PdfHtmlLoadingTests.Png());
            var html = PdfHtml.FromString("<img src='\U0001F600.png'><img src='%F0%9F%98%80.png'>", new PdfHtmlLoadOptions { BaseFolder = folder });
            Assert.Empty(html.Warnings);
            Assert.Equal(2, html.Model.Images.Count);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    #endregion

    #endregion
}
