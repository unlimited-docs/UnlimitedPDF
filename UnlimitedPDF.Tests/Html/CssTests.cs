using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Html;
using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// CSS values, selectors, the cascade, presentational attributes, and the default look (WR-038, AC-05 to AC-07, AC-12).
/// </summary>
public class CssTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("16px", 12)]
    [InlineData("12pt", 12)]
    [InlineData("1pc", 12)]
    [InlineData("1in", 72)]
    [InlineData("2.54cm", 72)]
    [InlineData("25.4mm", 72)]
    [InlineData("2em", 20)]
    [InlineData("2rem", 24)]
    [InlineData("50%", 50)]
    [InlineData("0", 0)]
    public void Lengths_convert_to_points(string value, double expected)
    {
        Assert.True(CssValues.TryParseLength(value, out var length));
        Assert.Equal(expected, length.ToPoints(fontSize: 10, rootFontSize: 12, percentBase: 100), 6);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("px")]
    [InlineData("calc(1px + 2px)")]
    [InlineData("1e400px")]
    public void Invalid_lengths_are_rejected(string value)
    {
        Assert.False(CssValues.TryParseLength(value, out _));
    }

    [Theory]
    [InlineData("#002A73", 0, 0.1647, 0.451)]
    [InlineData("#0f0", 0, 1, 0)]
    [InlineData("navy", 0, 0, 0.502)]
    [InlineData("RebeccaPurple", 0.4, 0.2, 0.6)]
    [InlineData("rgb(255, 0, 0)", 1, 0, 0)]
    [InlineData("rgb(100% 50% 0%)", 1, 0.5, 0)]
    [InlineData("rgba(0, 0, 0, 0.5)", 0.5, 0.5, 0.5)]
    [InlineData("rgb(0 0 0 / 25%)", 0.75, 0.75, 0.75)]
    [InlineData("#00000080", 0.498, 0.498, 0.498)]
    public void Colors_parse_and_blend_alpha_onto_white(string value, double r, double g, double b)
    {
        Assert.True(CssValues.TryParseColor(value, out var color, out bool transparent));
        Assert.False(transparent);
        Assert.Equal(r, color.R, 3);
        Assert.Equal(g, color.G, 3);
        Assert.Equal(b, color.B, 3);
    }

    [Fact]
    public void All_148_named_colors_are_known_and_transparent_is_transparent()
    {
        Assert.Equal(148, CssValues.NamedColorCount);
        Assert.True(CssValues.TryParseColor("transparent", out _, out bool transparent));
        Assert.True(transparent);
        Assert.False(CssValues.TryParseColor("notacolor", out _, out _));
    }

    [Theory]
    [InlineData("p", 1)]
    [InlineData(".a", 100)]
    [InlineData("#x", 10000)]
    [InlineData("div > p.a[title]:first-child", 302)]
    [InlineData("*", 0)]
    public void Specificity_counts_ids_classes_and_types(string selector, int expected)
    {
        Assert.True(CssSelector.TryParseList(selector, out var list));
        Assert.Equal(expected, list[0].Specificity);
    }

    [Theory]
    [InlineData("a + b")]
    [InlineData("a ~ b")]
    [InlineData("p::before")]
    [InlineData("li:nth-child(2)")]
    [InlineData("p:not(.x)")]
    [InlineData("[href^=http]")]
    public void Unsupported_selectors_drop_the_rule(string selector)
    {
        Assert.False(CssSelector.TryParseList(selector, out _));
    }

    [Fact]
    public void Selectors_match_type_class_id_attribute_descendant_child_and_position()
    {
        var styles = Styles(@"<style>
            .note { color: red }
            #main p { color: green }
            div > span { color: blue }
            [data-x] { color: navy }
            [lang=fr] { color: teal }
            li:first-child { color: orange }
            li:last-child { color: purple }
            a:link { color: maroon }
            a:hover { color: lime }
            * { letter-spacing: 1pt }
        </style>
        <p class='note other'>n</p><div id=main><section><p>m</p></section></div><div><span>s</span><b><span>t</span></b></div>
        <i data-x>x</i><i lang=fr>f</i><ul><li>1<li>2<li>3</ul><a href=#>l</a><a>no</a>", out var tree);
        Assert.Equal(Rgb("red"), Color(styles, tree, "p", 0));
        Assert.Equal(Rgb("green"), Color(styles, tree, "p", 1));
        Assert.Equal(Rgb("blue"), Color(styles, tree, "span", 0));
        Assert.Equal(PdfColor.Black, Color(styles, tree, "span", 1));
        Assert.Equal(Rgb("navy"), Color(styles, tree, "i", 0));
        Assert.Equal(Rgb("teal"), Color(styles, tree, "i", 1));
        Assert.Equal(Rgb("orange"), Color(styles, tree, "li", 0));
        Assert.Equal(PdfColor.Black, Color(styles, tree, "li", 1));
        Assert.Equal(Rgb("purple"), Color(styles, tree, "li", 2));
        Assert.Equal(Rgb("maroon"), Color(styles, tree, "a", 0));
        Assert.Equal(PdfColor.Black, Color(styles, tree, "a", 1));
        Assert.Equal(1, styles[Find(tree, "b", 0)].LetterSpacing);
    }

    [Fact]
    public void Cascade_orders_by_importance_specificity_and_source()
    {
        var styles = Styles(@"<style>
            p { color: red }
            p { color: green }
            .x { color: blue }
            p { font-weight: bold !important }
            .y { font-weight: normal }
        </style>
        <p>a</p><p class=x>b</p><p class=x style='color: navy'>c</p><p class=y style='font-weight: normal'>d</p>
        <p class=x style='color: teal !important'>e</p>", out var tree);
        Assert.Equal(Rgb("green"), Color(styles, tree, "p", 0));
        Assert.Equal(Rgb("blue"), Color(styles, tree, "p", 1));
        Assert.Equal(Rgb("navy"), Color(styles, tree, "p", 2));
        Assert.Equal(700, styles[Find(tree, "p", 3)].FontWeight);
        Assert.Equal(Rgb("teal"), Color(styles, tree, "p", 4));
    }

    [Fact]
    public void Inherited_properties_flow_to_children_and_others_do_not()
    {
        var styles = Styles("<div style='color: red; font-family: Georgia, serif; border: 2px solid; padding: 5px; inherit-test: 1'><span>x</span><p style='color: inherit; padding: inherit'>y</p></div>", out var tree);
        var span = styles[Find(tree, "span", 0)];
        Assert.Equal(Rgb("red"), span.Color);
        Assert.Equal(StandardFontFamily.Times, span.FontFamily);
        Assert.Equal(0, span.Paddings[0].Value);
        Assert.Equal(HtmlBorderStyle.None, span.Borders[0].Style);
        var div = styles[Find(tree, "div", 0)];
        Assert.Equal(Rgb("red"), div.Borders[2].Color); // currentcolor
        Assert.Equal(1.5, div.Borders[2].Width);
        Assert.Equal(3.75, styles[Find(tree, "p", 0)].Paddings[1].Value);
    }

    [Fact]
    public void Font_sizes_follow_keywords_em_percent_and_rem()
    {
        var styles = Styles("<div style='font-size: 20px'><p style='font-size: 2em'>a</p><p style='font-size: 50%'>b</p><p style='font-size: 2rem'>c</p><p style='font-size: x-large'>d</p><p><small>e</small></p></div>", out var tree);
        Assert.Equal(30, styles[Find(tree, "p", 0)].FontSize, 6);
        Assert.Equal(7.5, styles[Find(tree, "p", 1)].FontSize, 6);
        Assert.Equal(24, styles[Find(tree, "p", 2)].FontSize, 6);
        Assert.Equal(18, styles[Find(tree, "p", 3)].FontSize, 6);
        Assert.Equal(12.5, styles[Find(tree, "small", 0)].FontSize, 6);
    }

    [Fact]
    public void Font_shorthand_and_media_rules_apply()
    {
        var styles = Styles(@"<style>
            h1 { font: italic bold 24px/1.5 'Courier New', monospace }
            @media screen { p { color: red } }
            @media print { p { color: green } }
            @media only screen and (max-width: 600px) { p { color: blue } }
        </style><h1>t</h1><p>p</p>", out var tree);
        var h1 = styles[Find(tree, "h1", 0)];
        Assert.True(h1.Italic);
        Assert.Equal(700, h1.FontWeight);
        Assert.Equal(18, h1.FontSize, 6);
        Assert.Equal(27, h1.LineHeight, 6);
        Assert.Equal(StandardFontFamily.Courier, h1.FontFamily);
        Assert.Equal(Rgb("green"), Color(styles, tree, "p", 0));
    }

    [Fact]
    public void Default_look_follows_the_HTML_rendering_defaults()
    {
        var styles = Styles("<h1>a</h1><h2>b</h2><h3>c</h3><h6>d</h6><p>e</p><ul><li>f<ul><li>g<ul><li>h</ul></ul></ul><ol><li>i</ol><pre>j</pre><table><tr><th>k<td>l</table><a href=x>m</a><blockquote>n</blockquote><dl><dd>o</dl>", out var tree);
        Assert.Equal(24, styles[Find(tree, "h1", 0)].FontSize, 6);
        Assert.Equal(700, styles[Find(tree, "h1", 0)].FontWeight);
        Assert.Equal(18, styles[Find(tree, "h2", 0)].FontSize, 6);
        Assert.Equal(14.04, styles[Find(tree, "h3", 0)].FontSize, 6);
        Assert.Equal(8.04, styles[Find(tree, "h6", 0)].FontSize, 6);
        Assert.Equal(CssDisplay.Block, styles[Find(tree, "p", 0)].Display);
        Assert.Equal(12, styles[Find(tree, "p", 0)].Margins[0]!.Value.Value, 6);
        Assert.Equal(30, styles[Find(tree, "ul", 0)].Paddings[3].Value, 6);
        Assert.Equal(CssListStyleType.Disc, styles[Find(tree, "li", 0)].ListStyleType);
        Assert.Equal(CssListStyleType.Circle, styles[Find(tree, "li", 1)].ListStyleType);
        Assert.Equal(CssListStyleType.Square, styles[Find(tree, "li", 2)].ListStyleType);
        Assert.Equal(CssListStyleType.Decimal, styles[Find(tree, "li", 3)].ListStyleType);
        Assert.Equal(CssWhiteSpace.Pre, styles[Find(tree, "pre", 0)].WhiteSpace);
        Assert.Equal(StandardFontFamily.Courier, styles[Find(tree, "pre", 0)].FontFamily);
        Assert.Equal(CssTextAlign.Center, styles[Find(tree, "th", 0)].TextAlign);
        Assert.Equal(700, styles[Find(tree, "th", 0)].FontWeight);
        Assert.Equal(CssVerticalAlign.Middle, styles[Find(tree, "td", 0)].VerticalAlign);
        Assert.True(styles[Find(tree, "a", 0)].Underline);
        Assert.Equal(30, styles[Find(tree, "blockquote", 0)].Margins[3]!.Value.Value, 6);
        Assert.Equal(StandardFontFamily.Times, styles[Find(tree, "p", 0)].FontFamily);
    }

    [Fact]
    public void Presentational_attributes_apply_below_author_styles()
    {
        var styles = Styles("<style>.w { width: 50px }</style><table border=2 cellpadding=6 cellspacing=0 width=600 bgcolor=#eeeeee><tr><td align=right valign=top>a<td class=w width=10 nowrap>b</table><ol type=I><li>x</ol>", out var tree);
        var table = styles[Find(tree, "table", 0)];
        Assert.Equal(450, table.Width!.Value.Value, 6);
        Assert.Equal(0, table.BorderSpacingHorizontal);
        Assert.Equal(1.5, table.Borders[0].Width);
        var td = styles[Find(tree, "td", 0)];
        Assert.Equal(4.5, td.Paddings[0].Value, 6);
        Assert.Equal(CssTextAlign.Right, td.TextAlign);
        Assert.Equal(CssVerticalAlign.Top, td.VerticalAlign);
        Assert.Equal(HtmlBorderStyle.Solid, td.Borders[0].Style);
        var second = styles[Find(tree, "td", 1)];
        Assert.Equal(37.5, second.Width!.Value.Value, 6);
        Assert.Equal(CssWhiteSpace.NoWrap, second.WhiteSpace);
        Assert.Equal(CssListStyleType.UpperRoman, styles[Find(tree, "li", 0)].ListStyleType);
    }

    [Fact]
    public void Unsupported_properties_and_values_are_reported_once_per_rule_or_attribute()
    {
        var tree = HtmlParserTests.Build(@"<style>
            .a { float: left; color: nope; display: flex; -webkit-box-shadow: none; background: url(x.png) #fff }
            @font-face { font-family: X }
        </style><p class=a style='transform: rotate(1deg)'>x</p><p class=a>y</p>", out var warnings);
        var rules = new List<CssRule>();
        int order = 0;
        foreach (var source in tree.StyleSources) CssParser.ParseStyleSheet(source.Text!, source.Line, ref order, rules, warnings);
        CssCascade.Apply(tree, rules, warnings);
        Assert.Equal(new[] { "@font-face", "float", "color", "display", "background-image", "transform" }, warnings.Items.Select(w => w.Source));
        Assert.Equal(PdfHtmlWarningKind.UnsupportedValue, warnings.Items[3].Kind);
        Assert.Contains("laid out as a block", warnings.Items[3].Message);
    }

    #endregion

    #region Internal Methods

    internal static Dictionary<HtmlElement, ComputedStyle> Styles(string html, out HtmlDocumentTree tree)
    {
        tree = HtmlParserTests.Build(html, out var warnings);
        var rules = new List<CssRule>();
        int order = 0;
        foreach (var source in tree.StyleSources.Where(s => s.Text is not null))
            CssParser.ParseStyleSheet(source.Text!, source.Line, ref order, rules, warnings);
        return CssCascade.Apply(tree, rules, warnings);
    }

    internal static HtmlElement Find(HtmlDocumentTree tree, string name, int index)
    {
        var found = new List<HtmlElement>();
        var pending = new Stack<HtmlElement>();
        pending.Push(tree.Root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element.Name == name) found.Add(element);
            for (int i = element.Children.Count - 1; i >= 0; i--)
                if (element.Children[i] is HtmlElement child) pending.Push(child);
        }
        return found[index];
    }

    #endregion

    #region Private Methods

    private static PdfColor Color(Dictionary<HtmlElement, ComputedStyle> styles, HtmlDocumentTree tree, string name, int index) => styles[Find(tree, name, index)].Color;

    private static PdfColor Rgb(string name)
    {
        CssValues.TryParseColor(name, out var color, out _);
        return color;
    }

    #endregion

    #endregion
}
