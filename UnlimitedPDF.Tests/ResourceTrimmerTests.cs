using UnlimitedPDF.Models.Internal.Importing;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The content scanner reports the resource names operators use, and the trimmer keeps only those, following forms,
/// tiling patterns, Type 3 glyphs, and appearances; content it cannot read keeps its dictionary whole (WR-040, AC-11,
/// A06, A09).
/// </summary>
public class ResourceTrimmerTests
{
    #region Fields and Constants

    // Every resource category, with one used and one unused name each, plus a default color space and a ProcSet.
    private const string AllResources =
        "<< /ProcSet [/PDF /Text] " +
        "/Font << /F1 10 0 R /F2 10 0 R /F3 11 0 R >> " +
        "/XObject << /Im1 12 0 R /Im2 12 0 R /Fm1 13 0 R /Fm2 14 0 R >> " +
        "/ExtGState << /GS1 << /CA 0.5 >> /GS2 << /CA 1 >> >> " +
        "/ColorSpace << /CS1 [/ICCBased 15 0 R] /CS2 /DeviceRGB /DefaultRGB /DeviceRGB >> " +
        "/Pattern << /P1 16 0 R /P2 16 0 R >> " +
        "/Shading << /Sh1 << /ShadingType 2 >> /Sh2 << /ShadingType 2 >> >> " +
        "/Properties << /MC1 << /Name (one) >> /MC2 << /Name (two) >> >> >>";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Scanner_reports_the_name_each_operator_uses()
    {
        var uses = ContentNameScanner.Scan(Encoding.ASCII.GetBytes(
            "q /GS1 gs /CS1 cs 1 0 0 sc /Pattern CS /P1 SCN BT /F1 12 Tf (/F9 Tf) Tj ET /Im1 Do /Sh1 sh " +
            "/Span /MC1 BDC EMC /Tag << /MCID 0 >> BDC EMC /Tag /MC2 DP Q"));

        Assert.Equal(new[]
        {
            ("ExtGState", "GS1"), ("ColorSpace", "CS1"), ("ColorSpace", "Pattern"), ("Pattern", "P1"), ("Font", "F1"),
            ("XObject", "Im1"), ("Shading", "Sh1"), ("Properties", "MC1"), ("Properties", "MC2"),
        }, uses);
    }

    [Fact]
    public void Scanner_treats_an_inline_image_as_unreadable()
    {
        Assert.Throws<PdfReadException>(() => ContentNameScanner.Scan(Encoding.ASCII.GetBytes("BI /W 1 /H 1 /CS /G /BPC 8 ID \u0001 EI")));
    }

    [Fact]
    public void Only_used_names_are_kept_in_every_category()
    {
        var (trimmer, page) = Load("/GS1 gs /CS1 cs /P1 scn BT /F1 12 Tf ET /Im1 Do /Sh1 sh /Span /MC1 BDC EMC");

        var kept = Assert.IsType<PdfReadDictionary>(trimmer.TrimPage(page.Inherited["Resources"], page.Dictionary["Contents"], Array.Empty<byte[]>(), Array.Empty<PdfReadDictionary>()));

        Assert.Equal(new[] { "ProcSet", "Font", "XObject", "ExtGState", "ColorSpace", "Pattern", "Shading", "Properties" }, kept.Keys);
        Assert.Equal(new[] { "F1" }, Names(kept, "Font"));
        Assert.Equal(new[] { "Im1" }, Names(kept, "XObject"));
        Assert.Equal(new[] { "GS1" }, Names(kept, "ExtGState"));
        Assert.Equal(new[] { "CS1", "DefaultRGB" }, Names(kept, "ColorSpace"));
        Assert.Equal(new[] { "P1" }, Names(kept, "Pattern"));
        Assert.Equal(new[] { "Sh1" }, Names(kept, "Shading"));
        Assert.Equal(new[] { "MC1" }, Names(kept, "Properties"));
    }

    [Fact]
    public void An_operator_whose_operands_are_in_the_previous_content_stream_keeps_its_resource()
    {
        // Review R-01: the streams of /Contents form one content stream and may split anywhere between tokens.
        var file = PdfFile.FromBytes(Builder()
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents [5 0 R 6 0 R] >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12"))
            .AddStream(6, "/Filter /FlateDecode", ReaderFixtureBuilder.Deflate(Encoding.ASCII.GetBytes("Tf 72 720 Td (Hi) Tj ET /Im1")))
            .AddStream(7, "", Encoding.ASCII.GetBytes("Do"))
            .ToBytes());
        var page = file.Pages[0];
        var trimmer = new ResourceTrimmer(file.Source.Objects);

        var withTwo = Assert.IsType<PdfReadDictionary>(Trim(trimmer, page));

        Assert.Equal(new[] { "F1" }, Names(withTwo, "Font"));
        Assert.False(withTwo.ContainsKey("XObject"));
    }

    [Fact]
    public void Content_streams_over_the_stream_limit_in_all_keep_the_whole_dictionary()
    {
        // Each stream fits the limit on its own; together they do not, so the page is treated as unreadable.
        var file = PdfFile.FromBytes(Builder()
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents [5 0 R 6 0 R] >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf ET" + new string(' ', 600)))
            .AddStream(6, "", Encoding.ASCII.GetBytes("BT /F2 12 Tf ET" + new string(' ', 600)))
            .ToBytes(), new PdfFileLoadOptions { MaxStreamSize = 1000 });
        var page = file.Pages[0];

        Assert.Same(page.Inherited["Resources"], Trim(new ResourceTrimmer(file.Source.Objects), page));
    }

    [Fact]
    public void A_category_left_empty_is_dropped()
    {
        var (trimmer, page) = Load("BT /F1 12 Tf ET");

        var kept = Assert.IsType<PdfReadDictionary>(Trim(trimmer, page));

        Assert.Equal(new[] { "ProcSet", "Font", "ColorSpace" }, kept.Keys);
        Assert.Equal(new[] { "DefaultRGB" }, Names(kept, "ColorSpace"));
    }

    [Fact]
    public void A_form_without_resources_counts_its_names_for_the_page()
    {
        var (trimmer, page) = Load("/Fm1 Do");

        var kept = Assert.IsType<PdfReadDictionary>(Trim(trimmer, page));

        Assert.Equal(new[] { "Fm1" }, Names(kept, "XObject"));
        Assert.Equal(new[] { "F2" }, Names(kept, "Font"));
        Assert.Empty(trimmer.Overrides);
    }

    [Fact]
    public void A_form_with_its_own_resources_is_trimmed_on_its_own()
    {
        var (trimmer, page) = Load("/Fm2 Do");

        var kept = Assert.IsType<PdfReadDictionary>(Trim(trimmer, page));

        Assert.Equal(new[] { "Fm2" }, Names(kept, "XObject"));
        Assert.False(kept.ContainsKey("Font"));
        var form = Assert.IsType<PdfReadStream>(trimmer.Overrides[14]);
        var formResources = Assert.IsType<PdfReadDictionary>(form.Dictionary["Resources"]);
        Assert.Equal(new[] { "A" }, Names(formResources, "Font"));
        Assert.Equal("Form", Assert.IsType<PdfReadName>(form.Dictionary["Subtype"]).Value);
    }

    [Fact]
    public void Type3_glyphs_without_resources_count_their_names_for_the_page()
    {
        var (trimmer, page) = Load("BT /F3 12 Tf ET");

        var kept = Assert.IsType<PdfReadDictionary>(Trim(trimmer, page));

        Assert.Equal(new[] { "F3" }, Names(kept, "Font"));
        Assert.Equal(new[] { "Im2" }, Names(kept, "XObject"));
    }

    [Fact]
    public void Added_content_counts_too()
    {
        var (trimmer, page) = Load("BT ET");

        var kept = Assert.IsType<PdfReadDictionary>(trimmer.TrimPage(page.Inherited["Resources"], page.Dictionary["Contents"],
            new[] { Encoding.ASCII.GetBytes("q /Im1 Do Q") }, Array.Empty<PdfReadDictionary>()));

        Assert.Equal(new[] { "Im1" }, Names(kept, "XObject"));
    }

    [Fact]
    public void Appearances_with_their_own_resources_are_trimmed()
    {
        var (trimmer, page) = Load("BT ET");
        var annotation = new PdfReadDictionary();
        var appearances = new PdfReadDictionary();
        appearances.Set("N", new PdfReadReference(14, 0));
        annotation.Set("AP", appearances);

        trimmer.TrimPage(page.Inherited["Resources"], page.Dictionary["Contents"], Array.Empty<byte[]>(), new[] { annotation });

        var form = Assert.IsType<PdfReadStream>(trimmer.Overrides[14]);
        Assert.Equal(new[] { "A" }, Names(Assert.IsType<PdfReadDictionary>(form.Dictionary["Resources"]), "Font"));
    }

    [Theory]
    [InlineData("lzw")]
    [InlineData("damaged")]
    [InlineData("oversize")]
    [InlineData("inline")]
    public void Content_that_cannot_be_read_keeps_the_whole_dictionary(string kind)
    {
        var builder = Builder();
        long? maxStreamSize = null;
        switch (kind)
        {
            case "lzw":
                builder.AddStream(5, "/Filter /LZWDecode", new byte[] { 0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01 });
                break;
            case "damaged":
                builder.AddStream(5, "/Filter /FlateDecode", new byte[] { 0x78, 0x9C, 0xFF, 0xFF, 0xFF });
                break;
            case "oversize":
                builder.AddStream(5, "/Filter /FlateDecode", ReaderFixtureBuilder.Deflate(Encoding.ASCII.GetBytes("BT /F1 12 Tf ET" + new string(' ', 4000))));
                maxStreamSize = 1000;
                break;
            default:
                builder.AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf ET BI /W 1 /H 1 /CS /G /BPC 8 ID \u0001 EI"));
                break;
        }
        var file = PdfFile.FromBytes(builder.ToBytes(), new PdfFileLoadOptions { MaxStreamSize = maxStreamSize });
        var trimmer = new ResourceTrimmer(file.Source.Objects);
        var page = file.Pages[0];

        var kept = Trim(trimmer, page);

        Assert.Same(page.Inherited["Resources"], kept);
    }

    [Fact]
    public void A_form_that_draws_itself_ends()
    {
        var builder = Builder()
            .AddStream(5, "", Encoding.ASCII.GetBytes("/Fm9 Do"))
            .AddStream(19, "/Type /XObject /Subtype /Form /BBox [0 0 1 1]", Encoding.ASCII.GetBytes("/Fm9 Do BT /F1 1 Tf ET"));
        var file = PdfFile.FromBytes(builder.ToBytes().ToArray());
        var page = file.Pages[0];
        var resources = new PdfReadDictionary();
        var forms = new PdfReadDictionary();
        forms.Set("Fm9", new PdfReadReference(19, 0));
        resources.Set("XObject", forms);
        resources.Set("Font", Assert.IsType<PdfReadDictionary>(file.Source.Objects.Resolve(Assert.IsType<PdfReadDictionary>(file.Source.Objects.Resolve(page.Inherited["Resources"]))["Font"])));

        var kept = Assert.IsType<PdfReadDictionary>(new ResourceTrimmer(file.Source.Objects).TrimPage(resources, page.Dictionary["Contents"], Array.Empty<byte[]>(), Array.Empty<PdfReadDictionary>()));

        Assert.Equal(new[] { "Fm9" }, Names(kept, "XObject"));
        Assert.Equal(new[] { "F1" }, Names(kept, "Font"));
    }

    [Fact]
    public void Nesting_past_the_depth_limit_keeps_the_whole_dictionary()
    {
        // A chain of forms without resources of their own, each drawing the next, deeper than the parser allows.
        int depth = PdfParser.MaxDepth + 5;
        var builder = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /XObject << " + string.Concat(Enumerable.Range(0, depth).Select(i => $"/X{i} {100 + i} 0 R ")) + ">> >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Resources 3 0 R /Contents 5 0 R >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("/X0 Do"));
        for (int i = 0; i < depth; i++)
            builder.AddStream(100 + i, "/Type /XObject /Subtype /Form /BBox [0 0 1 1]", Encoding.ASCII.GetBytes(i + 1 < depth ? $"/X{i + 1} Do" : "BT ET"));
        var file = PdfFile.FromBytes(builder.ToBytes());
        var page = file.Pages[0];

        var kept = Trim(new ResourceTrimmer(file.Source.Objects), page);

        Assert.Same(page.Inherited["Resources"], kept);
    }

    #endregion

    #region Private Methods

    // One page drawn with AllResources (object 3), whose content is object 5; Fm1 (13) has no resources and uses F2,
    // Fm2 (14) has its own fonts A and B and uses A, F3 (11) is a Type 3 font whose glyph draws Im2.
    private static ReaderFixtureBuilder Builder() => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [4 0 R] /Count 1 /MediaBox [0 0 612 792] /Resources 3 0 R >>")
        .AddObject(3, AllResources)
        .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents 5 0 R >>")
        .AddObject(10, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
        .AddObject(11, "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1 1] /FontMatrix [1 0 0 1 0 0] /CharProcs << /a 17 0 R >> /Encoding << /Differences [97 /a] >> /FirstChar 97 /LastChar 97 /Widths [1] >>")
        .AddStream(12, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", new byte[] { 0 })
        .AddStream(13, "/Type /XObject /Subtype /Form /BBox [0 0 1 1]", Encoding.ASCII.GetBytes("BT /F2 1 Tf ET"))
        .AddStream(14, "/Type /XObject /Subtype /Form /BBox [0 0 1 1] /Resources << /Font << /A 10 0 R /B 10 0 R >> >>", Encoding.ASCII.GetBytes("BT /A 1 Tf ET"))
        .AddStream(15, "/N 3", new byte[] { 0 })
        .AddStream(16, "/PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 1 1] /XStep 1 /YStep 1", Encoding.ASCII.GetBytes("0 0 1 1 re f"))
        .AddStream(17, "", Encoding.ASCII.GetBytes("1 0 0 0 1 1 d1 /Im2 Do"));

    private static (ResourceTrimmer Trimmer, PdfSourcePage Page) Load(string content)
    {
        var file = PdfFile.FromBytes(Builder().AddStream(5, "", Encoding.ASCII.GetBytes(content)).ToBytes());
        return (new ResourceTrimmer(file.Source.Objects), file.Pages[0]);
    }

    private static IEnumerable<string> Names(PdfReadDictionary resources, string category) =>
        Assert.IsType<PdfReadDictionary>(resources[category]).Keys;

    private static PdfReadValue? Trim(ResourceTrimmer trimmer, PdfSourcePage page) =>
        trimmer.TrimPage(page.Inherited["Resources"], page.Dictionary["Contents"], Array.Empty<byte[]>(), Array.Empty<PdfReadDictionary>());

    #endregion

    #endregion
}
