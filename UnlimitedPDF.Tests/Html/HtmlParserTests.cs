using UnlimitedPDF.Models.Internal.Html;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// HTML is decoded, tokenized, and built into a tree with browser-style recovery; ignored elements are dropped and
/// reported; nesting past the limit fails without crashing (WR-038, AC-03, AC-05, AC-12, AC-19, AC-20).
/// </summary>
public class HtmlParserTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x43, 0x61, 0x66, 0xC3, 0xA9 }, "Café")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x43, 0x00, 0xE9, 0x00 }, "Cé")]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x43, 0x00, 0xE9 }, "Cé")]
    [InlineData(new byte[] { 0x43, 0x61, 0x66, 0xC3, 0xA9 }, "Café")]
    public void Bytes_decode_by_byte_order_mark_else_UTF8(byte[] data, string expected)
    {
        Assert.Equal(expected, HtmlDecoder.Decode(data, data.Length, new HtmlWarnings(false)));
    }

    [Theory]
    [InlineData("<meta charset=\"windows-1252\"><p>Café \u0080</p>")]
    [InlineData("<META HTTP-EQUIV='Content-Type' CONTENT='text/html; charset=ISO-8859-1'><p>Café \u0080</p>")]
    public void Meta_declarations_select_windows1252(string html)
    {
        byte[] data = html.Select(c => (byte)c).ToArray();
        string text = HtmlDecoder.Decode(data, data.Length, new HtmlWarnings(false));
        Assert.EndsWith("<p>Café €</p>", text);
    }

    [Fact]
    public void Unknown_character_sets_read_as_UTF8_with_a_warning()
    {
        var warnings = new HtmlWarnings(false);
        byte[] data = Encoding.UTF8.GetBytes("<meta charset=koi8-r><p>é</p>");
        Assert.EndsWith("<p>é</p>", HtmlDecoder.Decode(data, data.Length, warnings));
        var warning = Assert.Single(warnings.Items);
        Assert.Equal(PdfHtmlWarningKind.UnsupportedValue, warning.Kind);
        Assert.Equal("charset=koi8-r", warning.Source);
    }

    [Theory]
    [InlineData("&amp;&lt;&gt;&quot;&apos;", "&<>\"'")]
    [InlineData("&nbsp;&copy;&yuml;&euro;&mdash;&hellip;", " ©ÿ€—…")]
    [InlineData("&#233;&#xE9;&#X41;&#128;&#0;", "ééA€�")]
    [InlineData("&copy 2024 &amp more", "© 2024 & more")]
    [InlineData("&unknown; & ;", "&unknown; & ;")]
    public void Character_references_decode_as_browsers_do(string html, string expected)
    {
        var text = Assert.Single(new HtmlTokenizer(html).Tokens());
        Assert.Equal(expected, text.Value);
    }

    [Fact]
    public void Attributes_take_every_quoting_style_and_keep_the_first_duplicate()
    {
        var tag = new HtmlTokenizer("<IMG SRC=\"a.png\" alt='x &amp; y' width=10 data-x data-x=2 hidden/>").Tokens().First();
        Assert.Equal("img", tag.Value);
        Assert.True(tag.SelfClosing);
        Assert.Equal(new[] { "src=a.png", "alt=x & y", "width=10", "data-x=", "hidden=" }, tag.Attributes.Select(a => a.Key + "=" + a.Value));
    }

    [Fact]
    public void Attribute_values_keep_legacy_references_before_letters()
    {
        var tag = new HtmlTokenizer("<a href=\"?a=1&copy=2&amp;b=3\">").Tokens().First();
        Assert.Equal("?a=1&copy=2&b=3", tag.Attributes[0].Value);
    }

    [Fact]
    public void Raw_text_elements_are_not_parsed()
    {
        var tokens = new HtmlTokenizer("<style>p > b { color: red } </p></style><p>x</p>").Tokens().ToList();
        Assert.Equal("p > b { color: red } </p>", tokens[1].Value);
        Assert.Equal(HtmlTokenKind.EndTag, tokens[2].Kind);
    }

    [Fact]
    public void Lines_are_counted()
    {
        var tokens = new HtmlTokenizer("<p>a\r\nb</p>\n\n<img>").Tokens().ToList();
        Assert.Equal(4, tokens.Last().Line);
    }

    [Theory]
    [InlineData("<p>one<p>two", "<p>one</p><p>two</p>")]
    [InlineData("<ul><li>a<li>b</ul>", "<ul><li>a</li><li>b</li></ul>")]
    [InlineData("<dl><dt>t<dd>d<dt>u</dl>", "<dl><dt>t</dt><dd>d</dd><dt>u</dt></dl>")]
    [InlineData("<p>text<div>block</div>", "<p>text</p><div>block</div>")]
    [InlineData("<b>1<i>2</b>3</i>", "<b>1<i>2</i></b><i>3</i>")]
    [InlineData("<p><b>bold<p>still", "<p><b>bold</b></p><p><b>still</b></p>")]
    [InlineData("text</span></div>after", "textafter")]
    [InlineData("<h1>a<h2>b", "<h1>a</h1><h2>b</h2>")]
    [InlineData("<table><tr><td>A<td>B</table>", "<table><tbody><tr><td>A</td><td>B</td></tr></tbody></table>")]
    [InlineData("<table><td>x</table>", "<table><tbody><tr><td>x</td></tr></tbody></table>")]
    [InlineData("<table>stray<tr><td>x</table>", "stray<table><tbody><tr><td>x</td></tr></tbody></table>")]
    [InlineData("<table><thead><tr><th>H<tbody><tr><td>1</table>", "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td>1</td></tr></tbody></table>")]
    [InlineData("<table><tr><td><table><tr><td>in</table>out</table>", "<table><tbody><tr><td><table><tbody><tr><td>in</td></tr></tbody></table>out</td></tr></tbody></table>")]
    [InlineData("<td><b>x</td>y", "<b>xy</b>")]
    [InlineData("<pre>\nkeep\n</pre>", "<pre>keep\n</pre>")]
    [InlineData("<a href=1>one<a href=2>two</a>", "<a>one</a><a>two</a>")]
    [InlineData("<br></br>", "<br><br>")]
    public void Malformed_html_is_recovered_like_browsers_do(string html, string expected)
    {
        Assert.Equal(expected, Body(html));
    }

    [Fact]
    public void Text_and_elements_before_body_land_in_the_body()
    {
        var tree = Build("<html><head><title>T</title><style>p{}</style></head>Hello<body class=x><p>p</p>", out _);
        Assert.Equal("Hello<p>p</p>", Dump(tree.Body));
        Assert.Equal("T", tree.Title);
        Assert.Equal("p{}", Assert.Single(tree.StyleSources).Text);
        Assert.Equal("x", tree.Body.Attribute("class"));
    }

    [Fact]
    public void Stylesheet_links_keep_document_order()
    {
        var tree = Build("<link rel=stylesheet href=a.css><style>s1</style><link rel='icon' href=i.png><link rel='alternate stylesheet' href=b.css>", out _);
        Assert.Equal(new[] { "a.css", "s1", "b.css" }, tree.StyleSources.Select(s => s.Href ?? s.Text));
    }

    [Theory]
    [InlineData("<script>document.write('PAID')</script>ok", "script")]
    [InlineData("<svg><svg><text>x</text></svg></svg>ok", "svg")]
    [InlineData("<select><option>a</option></select>ok", "select")]
    [InlineData("<input value=x>ok", "input")]
    [InlineData("<iframe src=x>fallback</iframe>ok", "iframe")]
    [InlineData("<noscript>enable</noscript>ok", "noscript")]
    public void Ignored_elements_drop_their_content_with_a_warning(string html, string element)
    {
        var tree = Build(html, out var warnings);
        Assert.Equal("ok", Dump(tree.Body));
        var warning = Assert.Single(warnings.Items);
        Assert.Equal(PdfHtmlWarningKind.UnsupportedElement, warning.Kind);
        Assert.Equal(element, warning.Source);
        Assert.Equal(1, warning.Line);
    }

    [Fact]
    public void Unknown_elements_keep_their_content_and_are_reported_once()
    {
        var tree = Build("<center>a</center><center>b</center>", out var warnings);
        Assert.Equal("<center>a</center><center>b</center>", Dump(tree.Body));
        Assert.Equal("center", Assert.Single(warnings.Items).Source);
    }

    [Fact]
    public void Strict_mode_throws_the_first_warning()
    {
        var ex = Assert.Throws<PdfHtmlException>(() => new HtmlTreeBuilder(256, new HtmlWarnings(true)).Build(new HtmlTokenizer("<p>a</p><video></video>").Tokens()));
        Assert.Equal(PdfHtmlErrorReason.Warning, ex.Reason);
        Assert.Equal("video", ex.Warning!.Source);
    }

    [Fact]
    public void Nesting_past_the_limit_fails_without_crashing()
    {
        string html = string.Concat(Enumerable.Repeat("<div>", 20000)) + "deep";
        PdfHtmlException? caught = null;

        // A thread with the default stack size: the parser must not recurse.
        var thread = new Thread(() => caught = Record.Exception(() => Build(html, out _)) as PdfHtmlException);
        thread.Start();
        thread.Join();

        Assert.NotNull(caught);
        Assert.Equal(PdfHtmlErrorReason.NestingTooDeep, caught!.Reason);
        Assert.Contains("256", caught.Message);
    }

    [Fact]
    public void Nesting_within_the_limit_is_kept()
    {
        string html = string.Concat(Enumerable.Repeat("<div>", 253)) + "x";
        var tree = Build(html, out _);
        int depth = 0;
        HtmlElement current = tree.Body;
        while (current.Children.FirstOrDefault() is HtmlElement child) { current = child; depth++; }
        Assert.Equal(253, depth);
    }

    #endregion

    #region Internal Methods

    internal static HtmlDocumentTree Build(string html, out HtmlWarnings warnings)
    {
        warnings = new HtmlWarnings(false);
        return new HtmlTreeBuilder(256, warnings).Build(new HtmlTokenizer(html).Tokens());
    }

    internal static string Dump(HtmlElement element)
    {
        var sb = new StringBuilder();
        foreach (var child in element.Children)
        {
            if (child is HtmlText text) sb.Append(text.Text);
            else if (child is HtmlElement e)
            {
                sb.Append('<').Append(e.Name).Append('>');
                if (e.Name is not ("br" or "img" or "hr" or "col")) sb.Append(Dump(e)).Append("</").Append(e.Name).Append('>');
            }
        }
        return sb.ToString();
    }

    #endregion

    #region Private Methods

    private static string Body(string html) => Dump(Build(html, out _).Body);

    #endregion

    #endregion
}
