using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// Links, form XObjects, and uploaded pages: safe links only, merged form-field appearances drawn, pages with
/// content the renderer doesn't know left undrawn one by one, and the PDF bytes untouched (26111701 AC-12, V-06,
/// V-09, V-11).
/// </summary>
public class PageRendererLinksAndFormsTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_merged_page_draws_its_form_fields_and_keeps_only_safe_links()
    {
        var page = Merged()[0];

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(new[] { "Page 1", "Alice" }, page.TextLines); // the field's appearance is drawn through its form XObject
        var links = Regex.Matches(page.Svg!, "<a href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("https://example.com", links);
        Assert.Contains("#t0-page-2", links);
        Assert.Contains("#t0-page-3", links);
        Assert.DoesNotContain(links, link => link.Contains("alert", StringComparison.Ordinal) || link.Contains("calc", StringComparison.Ordinal));
        Assert.Equal(links.Count, page.Counts.Links);
    }

    [Fact]
    public void External_links_open_in_a_new_tab_and_say_where_they_go()
    {
        var page = Merged()[0];

        Assert.Contains("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\"><title>https://example.com</title>", page.Svg);
        Assert.Contains("data-pdf-page=\"3\"><title>Go to page 3</title>", page.Svg);
    }

    [Theory]
    [InlineData("(javascript:alert\\(1\\))")]
    [InlineData("(data:text/html,<script>alert\\(1\\)</script>)")]
    [InlineData("(vbscript:msgbox)")]
    [InlineData("(file:///etc/passwd)")]
    [InlineData("(https://ex ample.com)")]
    [InlineData("(/relative/path)")]
    [InlineData("<68747470733A2F2F610A2E636F6D>")]
    public void Unsafe_link_targets_are_dropped(string uri)
    {
        var pdf = RenderTestFiles.OnePage("0 0 1 1 re f", pageEntries: $"/Annots [<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI {uri} >> >>]");

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Equal(0, page.Counts.Links);
        Assert.DoesNotContain("<a ", page.Svg);
    }

    [Fact]
    public void A_link_target_with_markup_characters_stays_an_attribute()
    {
        var pdf = RenderTestFiles.OnePage("0 0 1 1 re f", pageEntries: "/Annots [<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI (https://a.com/?q=\"><script>) >> >>]");

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.Contains("href=\"https://a.com/?q=&quot;&gt;&lt;script&gt;\"", page.Svg);
        Assert.DoesNotContain("<script", page.Svg);
    }

    [Fact]
    public void A_form_xobject_is_drawn_through_its_matrix_and_clipped_to_its_box()
    {
        var pdf = RenderTestFiles.OnePage("q 1 0 0 1 10 10 cm /Fm1 Do Q", "<< /XObject << /Fm1 5 0 R >> >>",
            new[] { (5, "/Type /XObject /Subtype /Form /BBox [0 0 50 50] /Matrix [2 0 0 2 0 0]", (byte[]?)Encoding.ASCII.GetBytes("0 0 100 100 re f")) });

        var page = RenderTestFiles.RenderFirst(pdf);

        Assert.True(page.IsDrawn, page.Reason);
        Assert.Contains("<clipPath id=\"t-c0\"><path d=\"M0 0L50 0L50 50L0 50Z\" transform=\"matrix(2 0 0 -2 10 832)\"", page.Svg);
        Assert.Contains("<path d=\"M0 0L100 0L100 100L0 100Z\" transform=\"matrix(2 0 0 -2 10 832)\" fill=\"#000000\"/>", page.Svg);
    }

    [Theory]
    [InlineData("/Group << /S /Transparency >>", "transparency group")]
    [InlineData("/OC 9 0 R", "optional content")]
    [InlineData("", "no bounding box")]
    public void Form_xobjects_it_does_not_draw_are_refused(string entries, string reason)
    {
        string box = entries.Length == 0 ? string.Empty : "/BBox [0 0 1 1] ";
        var pdf = RenderTestFiles.OnePage("/Fm1 Do", "<< /XObject << /Fm1 5 0 R >> >>",
            new[] { (5, $"/Type /XObject /Subtype /Form {box}{entries}", (byte[]?)Encoding.ASCII.GetBytes("0 0 1 1 re f")) });

        Assert.Contains(reason, RenderTestFiles.RenderFirst(pdf).Reason);
    }

    [Fact]
    public void Form_xobjects_that_nest_or_recurse_stop_at_the_depth_bound()
    {
        var pdf = RenderTestFiles.OnePage("/Fm1 Do", "<< /XObject << /Fm1 5 0 R >> >>",
            new[] { (5, "/Type /XObject /Subtype /Form /BBox [0 0 1 1] /Resources << /XObject << /Fm1 5 0 R >> >>", (byte[]?)Encoding.ASCII.GetBytes("/Fm1 Do")) });

        Assert.Contains("form XObjects too deeply", RenderTestFiles.RenderFirst(pdf).Reason);
    }

    [Fact]
    public void Pages_with_content_it_does_not_know_are_left_undrawn_one_by_one()
    {
        // A library page, then an uploaded page with a shading, then another library page.
        var foreign = RenderTestFiles.OnePage("/Sh1 sh");
        var document = new PdfDocument();
        document.AddPage().AddTable(50, 800, 100).GetOrCreateCell(0, 0).Text = "mine";
        document.AddPages(PdfFile.FromBytes(foreign));
        document.AddPage();
        byte[] pdf = PdfPageReader.SaveBytes(document);

        var pages = RenderTestFiles.RenderAll(pdf);

        Assert.Equal(new[] { true, false, true }, pages.Select(p => p.IsDrawn));
        Assert.Contains("operator sh", pages[1].Reason);
    }

    [Fact]
    public void Drawing_leaves_the_pdf_bytes_unchanged()
    {
        byte[] pdf = BuildFixture();
        byte[] before = SHA256.HashData(pdf);

        RenderTestFiles.RenderAll(pdf);

        Assert.Equal(before, SHA256.HashData(pdf));
    }

    #endregion

    #region Private Methods

    // Three pages with links of every kind, a named destination, a text field, and actions that must be dropped.
    private static byte[] BuildFixture()
    {
        const string Rect = "/Rect [0 0 10 10]";
        return new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R /Dests 30 0 R /AcroForm << /Fields [17 0 R] >> >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R 6 0 R 8 0 R] /Count 3 /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /Contents 5 0 R /Annots [10 0 R 11 0 R 12 0 R 13 0 R 14 0 R 17 0 R 21 0 R] >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 1) Tj ET"))
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /Contents 7 0 R >>")
            .AddStream(7, "", Encoding.ASCII.GetBytes("BT ET"))
            .AddObject(8, "<< /Type /Page /Parent 2 0 R /Contents 9 0 R >>")
            .AddStream(9, "", Encoding.ASCII.GetBytes("BT ET"))
            .AddObject(10, $"<< /Type /Annot /Subtype /Link {Rect} /Dest [8 0 R /Fit] /P 4 0 R >>")
            .AddObject(11, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /GoTo /D [6 0 R /XYZ 0 0 0] >> >>")
            .AddObject(12, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /URI /URI (https://example.com) >> >>")
            .AddObject(13, $"<< /Type /Annot /Subtype /Link {Rect} /Dest /chapter3 >>")
            .AddObject(14, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /JavaScript /JS (app.alert(1)) >> >>")
            .AddObject(17, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Alice) /Rect [100 100 200 120] /AP << /N 19 0 R >> >>")
            .AddStream(19, "/Type /XObject /Subtype /Form /BBox [0 0 100 20] /Resources << /Font << /F1 3 0 R >> >>", Encoding.ASCII.GetBytes("BT /F1 10 Tf 2 5 Td (Alice) Tj ET"))
            .AddObject(21, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /Launch /F (calc.exe) >> >>")
            .AddObject(30, "<< /chapter3 [8 0 R /Fit] >>")
            .ToBytes();
    }

    // The fixture merged by the library, as visitors' uploads are in the Merge demos, then drawn.
    private static List<PageRenderResult> Merged()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(BuildFixture()));
        return RenderTestFiles.RenderAll(PdfPageReader.SaveBytes(document));
    }

    #endregion

    #endregion
}
