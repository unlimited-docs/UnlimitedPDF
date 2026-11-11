using UnlimitedPDF.Models.Internal;
using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Importing;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The output side of HTML text, links, and heading bookmarks (WR-038, AC-13, AC-18): standard-font dictionaries, link
/// annotations that can only open an address or jump to a page, and headings nested by level.
/// </summary>
public class HtmlSerializerHookTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Standard_font_dictionaries_use_WinAnsi_and_fixed_resource_names()
    {
        string dictionary = new StandardFontObject(StandardFont.TimesBoldItalic).ToString();
        Assert.Contains("/BaseFont /Times-BoldItalic", dictionary);
        Assert.Contains("/Encoding /WinAnsiEncoding", dictionary);
        Assert.Contains("/Subtype /Type1", dictionary);
        Assert.Equal("HF1", StandardFontObject.ResourceName(StandardFont.Helvetica));
        Assert.Equal("HF12", StandardFontObject.ResourceName(StandardFont.CourierBoldOblique));
    }

    [Fact]
    public void Draw_sink_lists_each_font_once_in_resource_order()
    {
        var sink = new PageDrawSink(_ => "/Im1");
        Assert.Equal("HF6", sink.FontResource(StandardFont.TimesBold));
        sink.FontResource(StandardFont.Helvetica);
        sink.FontResource(StandardFont.TimesBold);
        Assert.Equal(new[] { StandardFont.Helvetica, StandardFont.TimesBold }, sink.Fonts);
    }

    [Fact]
    public void Uri_links_escape_the_address_and_have_no_border()
    {
        var links = new[] { new PageLink(10, 20, 30, 12, "https://example.com/a(b)", null) };
        Assert.Equal(
            @"[<< /Type /Annot /Subtype /Link /Rect [10 20 40 32] /Border [0 0 0] /A << /S /URI /URI (https://example.com/a\(b\)) >> >>]",
            LinkAnnotations.Syntax(links, _ => null));
    }

    [Fact]
    public void Id_links_jump_to_the_target_page_and_unknown_ids_are_not_written()
    {
        var links = new[]
        {
            new PageLink(0, 0, 5, 5, null, "costs"),
            new PageLink(0, 10, 5, 5, null, "missing")
        };
        string? syntax = LinkAnnotations.Syntax(links, id => id == "costs" ? (12, 36.5, 700) : null);
        Assert.Equal("[<< /Type /Annot /Subtype /Link /Rect [0 0 5 5] /Border [0 0 0] /Dest [12 0 R /XYZ 36.5 700 null] >>]", syntax);
        Assert.Null(LinkAnnotations.Syntax(new[] { links[1] }, _ => null));
    }

    [Fact]
    public void Headings_nest_by_level()
    {
        var sources = new[]
        {
            Heading("Summary", 1, 700),
            Heading("Costs", 2, 600),
            Heading("Detail", 3, 500),
            Heading("Revenue", 2, 400),
            Heading("Appendix", 1, 300)
        };
        var objects = OutlineBuilder.Build(sources, index => 100 + index);

        // Root plus five bookmarks; two top-level entries, all open: 5 visible.
        Assert.Equal(6, objects.Count);
        Assert.Equal("<< /Type /Outlines /First 101 0 R /Last 105 0 R /Count 5 >>", objects[0]);
        Assert.Contains("/First 102 0 R /Last 104 0 R /Count 3", objects[1]); // Summary holds Costs and Revenue
        Assert.Contains("/Parent 102 0 R", objects[3]);                      // Detail under Costs
        Assert.Contains("/Dest [9 0 R /XYZ 36 500 null]", objects[3]);
    }

    [Fact]
    public void Non_ASCII_heading_titles_are_written_as_UTF16()
    {
        var objects = OutlineBuilder.Build(new[] { Heading("Café ₹", 1, 700) }, index => 1 + index);
        Assert.Contains("/Title <FEFF00430061006600E9002020B9>", objects[1]);
    }

    #endregion

    #region Private Methods

    private static OutlineSource Heading(string title, int level, double y) =>
        OutlineSource.ForHeading(new OutlineHeading(title, level, 9, 36, y));

    #endregion

    #endregion
}
