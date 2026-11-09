using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Each split part keeps its pages as merge imports them (size, rotation, links inside the part, form look, layers),
/// carries no document-level content, declares the source version (at least 1.7), and copies page resources as they
/// are (WR-036, WR-037, AC-08, AC-11).
/// </summary>
public class SplitPartContentTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Part_pages_keep_size_rotation_links_form_look_and_layers()
    {
        var part = PdfFile.FromBytes(FirstPart(Fixture("%PDF-1.4")));
        var objects = part.Source.Objects;
        var first = part.Pages[0].Dictionary;
        var second = part.Pages[1].Dictionary;

        Assert.Equal(new[] { 0.0, 0, 300, 400 }, Assert.IsType<PdfReadArray>(objects.Resolve(first["MediaBox"])).Items
            .Select(item => Assert.IsType<PdfReadNumber>(item).Value));
        Assert.Equal(90, Assert.IsType<PdfReadNumber>(objects.Resolve(first["Rotate"])).Value);

        // The link to page 2 now targets the part's page 2; the link to source page 4 is gone.
        var links = Assert.IsType<PdfReadArray>(objects.Resolve(first["Annots"])).Items
            .Select(item => Assert.IsType<PdfReadDictionary>(objects.Resolve(item))).ToList();
        var link = Assert.Single(links);
        var target = Assert.IsType<PdfReadReference>(Assert.IsType<PdfReadArray>(objects.Resolve(link["Dest"]))[0]);
        Assert.Equal(part.Pages[1].ObjectNumber, target.ObjectNumber);

        // The filled field is painted into the page and is no longer a field.
        Assert.False(part.Source.Catalog.ContainsKey("AcroForm"));
        Assert.False(second.ContainsKey("Annots") && objects.Resolve(second["Annots"]) is PdfReadArray { Count: > 0 });
        Assert.Contains("UnlimitedField1", Encoding.Latin1.GetString(FirstPart(Fixture("%PDF-1.4"))), StringComparison.Ordinal);

        // The hidden layer keeps its off state.
        var properties = Assert.IsType<PdfReadDictionary>(objects.Resolve(part.Source.Catalog["OCProperties"]));
        var off = Assert.IsType<PdfReadArray>(Assert.IsType<PdfReadDictionary>(objects.Resolve(properties["D"]))["OFF"]);
        var layer = Assert.IsType<PdfReadDictionary>(objects.Resolve(Assert.Single(off.Items)));
        Assert.Equal("Hidden layer", Encoding.Latin1.GetString(Assert.IsType<PdfReadString>(layer["Name"]).Bytes));
    }

    [Fact]
    public void Part_has_no_document_level_content()
    {
        var pdf = FirstPart(Fixture("%PDF-1.4"));
        var catalog = PdfFile.FromBytes(pdf).Source.Catalog;
        string text = Encoding.Latin1.GetString(pdf);

        PdfStructureChecker.AssertValid(pdf);
        foreach (var key in new[] { "Metadata", "Names", "OpenAction", "StructTreeRoot", "PageLabels", "AcroForm" })
            Assert.False(catalog.ContainsKey(key), key);
        Assert.DoesNotContain("/Info", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret title", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("%PDF-1.4", "%PDF-1.7")]
    [InlineData("%PDF-2.0", "%PDF-2.0")]
    public void Part_declares_the_source_version_and_at_least_1_7(string header, string expected)
    {
        var pdf = FirstPart(Fixture(header));

        Assert.Equal(expected, Encoding.ASCII.GetString(pdf, 0, 8));
        Assert.Contains(Encoding.ASCII.GetString(pdf, 8, 1), "\r\n", StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_resource_list_is_copied_into_every_part()
    {
        var parts = PdfOperationsSplitTests.SplitToMemory(PdfFile.FromBytes(SharedResources()), 1);

        Assert.Equal(3, parts.Count);
        Assert.All(parts, p => Assert.Contains("/BaseFont /Courier", Encoding.Latin1.GetString(p.Bytes), StringComparison.Ordinal));
    }

    [Fact]
    public void Per_page_resources_stay_with_their_page()
    {
        var data = ReaderFixtureBuilder.Document(2)
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R /F2 50 0 R >> >> /Contents 7 0 R >>")
            .AddObject(50, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>")
            .ToBytes();

        var parts = PdfOperationsSplitTests.SplitToMemory(PdfFile.FromBytes(data), 1);

        Assert.DoesNotContain("/BaseFont /Courier", Encoding.Latin1.GetString(parts[0].Bytes), StringComparison.Ordinal);
        Assert.Contains("/BaseFont /Courier", Encoding.Latin1.GetString(parts[1].Bytes), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    // Part 1 (source pages 1-2) of a split every 2 pages.
    private static byte[] FirstPart(byte[] source) => PdfOperationsSplitTests.SplitToMemory(PdfFile.FromBytes(source), 2)[0].Bytes;

    // Four pages: page 1 is 300 x 400, rotated, with links to page 2 and page 4; page 2 has a filled form field and
    // draws in a hidden layer; plus page labels, tags, a script, and an information dictionary.
    private static byte[] Fixture(string header) => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [20 0 R] >> /OCProperties << /OCGs [30 0 R] /D << /OFF [30 0 R] >> >> /PageLabels << /Nums [0 << /S /r >>] >> /StructTreeRoot 31 0 R /OpenAction << /S /JavaScript /JS (app.alert(1)) >> >>")
        .AddObject(2, "<< /Type /Pages /Kids [4 0 R 6 0 R 8 0 R 10 0 R] /Count 4 /MediaBox [0 0 612 792] >>")
        .AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
        .AddObject(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Rotate 90 /Resources << /Font << /F1 3 0 R >> >> /Contents 5 0 R /Annots [12 0 R 13 0 R] >>")
        .AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 300 Td (Page 1) Tj ET"))
        .AddObject(6, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> /Properties << /oc1 30 0 R >> >> /Contents 7 0 R /Annots [20 0 R] >>")
        .AddStream(7, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 2) Tj ET /OC /oc1 BDC 0 0 m 1 1 l S EMC"))
        .AddObject(8, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /Contents 9 0 R >>")
        .AddStream(9, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 3) Tj ET"))
        .AddObject(10, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /Contents 11 0 R >>")
        .AddStream(11, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 4) Tj ET"))
        .AddObject(12, "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /Dest [6 0 R /Fit] >>")
        .AddObject(13, "<< /Type /Annot /Subtype /Link /Rect [0 20 10 30] /Dest [10 0 R /Fit] >>")
        .AddObject(20, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Alice) /Rect [100 100 200 120] /AP << /N 21 0 R >> >>")
        .AddStream(21, "/Type /XObject /Subtype /Form /BBox [0 0 100 20] /Resources << /Font << /F1 3 0 R >> >>", Encoding.ASCII.GetBytes("BT /F1 10 Tf 2 5 Td (Alice) Tj ET"))
        .AddObject(30, "<< /Type /OCG /Name (Hidden layer) >>")
        .AddObject(31, "<< /Type /StructTreeRoot >>")
        .AddObject(40, "<< /Title (Secret title) >>")
        .ToBytes(header: header, trailerEntries: "/Root 1 0 R /Info 40 0 R");

    // Three pages that share one resource list on the page tree, holding two fonts.
    private static byte[] SharedResources()
    {
        var builder = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R 6 0 R 8 0 R] /Count 3 /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R /F2 50 0 R >> >> >>")
            .AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
            .AddObject(50, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        for (int n = 1; n <= 3; n++)
        {
            builder.AddObject(2 * n + 2, $"<< /Type /Page /Parent 2 0 R /Contents {2 * n + 3} 0 R >>");
            builder.AddStream(2 * n + 3, "", Encoding.ASCII.GetBytes($"BT /F1 12 Tf 72 720 Td (Page {n}) Tj ET"));
        }
        return builder.ToBytes();
    }

    #endregion

    #endregion
}
