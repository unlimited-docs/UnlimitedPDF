using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Imported links, comments, and form fields behave as WR-036 sets: links to imported pages work, scripts and other
/// actions are removed, and filled fields keep their look without staying editable (AC-06, A05).
/// </summary>
public class ImportedAnnotationsTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Comments_keep_their_popup_and_lose_their_actions()
    {
        var output = Import();
        var comment = Annotations(output, 0).Single(a => Subtype(a) == "Text");

        var popup = Assert.IsType<PdfReadDictionary>(output.Source.Objects.Resolve(comment["Popup"]));
        Assert.Equal("Popup", Subtype(popup));
        Assert.False(comment.ContainsKey("AA"));
        Assert.Equal("Note", Encoding.Latin1.GetString(Assert.IsType<PdfReadString>(comment["Contents"]).Bytes));
    }

    [Fact]
    public void Filled_field_is_painted_and_no_longer_editable()
    {
        var output = Import();
        var objects = output.Source.Objects;
        var page = output.Pages[0].Dictionary;

        var contents = Assert.IsType<PdfReadArray>(objects.Resolve(page["Contents"]));
        string last = Encoding.ASCII.GetString(Assert.IsType<PdfReadStream>(objects.Resolve(contents[contents.Count - 1])).Data.ToArray());
        var xObjects = Assert.IsType<PdfReadDictionary>(objects.Resolve(Assert.IsType<PdfReadDictionary>(objects.Resolve(page["Resources"]))["XObject"]));

        Assert.Equal("q\n", Encoding.ASCII.GetString(Assert.IsType<PdfReadStream>(objects.Resolve(contents[0])).Data.ToArray()));
        Assert.Contains("1 0 0 1 100 100 cm /UnlimitedField1 Do", last, StringComparison.Ordinal);
        Assert.DoesNotContain("UnlimitedField2", last, StringComparison.Ordinal);
        Assert.IsType<PdfReadStream>(objects.Resolve(xObjects["UnlimitedField1"]));
        Assert.DoesNotContain(Annotations(output, 0), a => Subtype(a) == "Widget");
        Assert.False(output.Source.Catalog.ContainsKey("AcroForm"));
    }

    [Fact]
    public void Links_to_imported_pages_point_at_their_new_page()
    {
        var output = Import();
        var links = Annotations(output, 0).Where(a => Subtype(a) == "Link").ToList();
        int pageTwo = output.Pages[1].ObjectNumber;
        int pageThree = output.Pages[2].ObjectNumber;

        var targets = links.Select(link => Target(output, link)).ToList();

        Assert.Equal(4, links.Count);
        Assert.Contains(pageThree.ToString(CultureInfo.InvariantCulture), targets);
        Assert.Contains(pageTwo.ToString(CultureInfo.InvariantCulture), targets);
        Assert.Contains("https://example.com", targets);
        Assert.Equal(2, targets.Count(t => t == pageThree.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Links_to_pages_left_out_are_removed()
    {
        var output = Import(firstPage: 1, lastPage: 1);
        var links = Annotations(output, 0).Where(a => Subtype(a) == "Link").ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com", Target(output, links[0]));
    }

    [Fact]
    public void Scripts_launches_and_their_text_are_not_copied()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(Fixture()));
        var pdf = ImportedPagesTests.Save(document);
        string text = Encoding.Latin1.GetString(pdf);

        PdfStructureChecker.AssertValid(pdf);
        Assert.DoesNotContain("JavaScript", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Launch", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Hex("calc.exe"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Hex("app.alert(1)"), text, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static List<PdfReadDictionary> Annotations(PdfFile output, int page) =>
        output.Source.Objects.Resolve(output.Pages[page].Dictionary["Annots"]) is PdfReadArray array
            ? array.Items.Select(item => Assert.IsType<PdfReadDictionary>(output.Source.Objects.Resolve(item))).ToList()
            : new List<PdfReadDictionary>();

    private static byte[] Fixture()
    {
        const string Rect = "/Rect [0 0 10 10]";
        return new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R /Dests 30 0 R /AcroForm << /Fields [17 0 R] >> >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R 6 0 R 8 0 R] /Count 3 /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /Contents 5 0 R /Annots [10 0 R 11 0 R 12 0 R 13 0 R 14 0 R 15 0 R 16 0 R 17 0 R 20 0 R 21 0 R] >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 1) Tj ET"))
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /Contents 7 0 R >>")
            .AddStream(7, "", Encoding.ASCII.GetBytes("BT ET"))
            .AddObject(8, "<< /Type /Page /Parent 2 0 R /Contents 9 0 R >>")
            .AddStream(9, "", Encoding.ASCII.GetBytes("BT ET"))
            .AddObject(10, $"<< /Type /Annot /Subtype /Link {Rect} /Dest [8 0 R /Fit] /P 4 0 R >>")
            .AddObject(11, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /GoTo /D [6 0 R /XYZ 0 0 0] >> >>")
            .AddObject(12, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /URI /URI (https://example.com) /Next 18 0 R >> >>")
            .AddObject(13, $"<< /Type /Annot /Subtype /Link {Rect} /Dest /chapter3 >>")
            .AddObject(14, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /JavaScript /JS (app.alert(1)) >> >>")
            .AddObject(15, $"<< /Type /Annot /Subtype /Text {Rect} /Contents (Note) /Popup 16 0 R /AA << /O 18 0 R >> >>")
            .AddObject(16, $"<< /Type /Annot /Subtype /Popup {Rect} /Parent 15 0 R >>")
            .AddObject(17, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Alice) /Rect [100 100 200 120] /AP << /N 19 0 R >> >>")
            .AddObject(18, "<< /S /JavaScript /JS (app.alert(1)) >>")
            .AddStream(19, "/Type /XObject /Subtype /Form /BBox [0 0 100 20] /Resources << /Font << /F1 3 0 R >> >>", Encoding.ASCII.GetBytes("BT /F1 10 Tf 2 5 Td (Alice) Tj ET"))
            .AddObject(20, "<< /Type /Annot /Subtype /Widget /F 2 /Rect [0 0 50 20] /AP << /N 19 0 R >> >>")
            .AddObject(21, $"<< /Type /Annot /Subtype /Link {Rect} /A << /S /Launch /F (calc.exe) >> >>")
            .AddObject(30, "<< /chapter3 [8 0 R /Fit] >>")
            .ToBytes();
    }

    private static string Hex(string text) => string.Concat(Encoding.ASCII.GetBytes(text).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

    private static PdfFile Import(int? firstPage = null, int? lastPage = null)
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(Fixture()), firstPage, lastPage);
        return ImportedPagesTests.Read(ImportedPagesTests.Save(document));
    }

    private static string? Subtype(PdfReadDictionary annotation) => (annotation["Subtype"] as PdfReadName)?.Value;

    // The output page object a link targets, or its URI.
    private static string Target(PdfFile output, PdfReadDictionary link)
    {
        var objects = output.Source.Objects;
        var destination = objects.Resolve(link["Dest"]);
        if (objects.Resolve(link["A"]) is PdfReadDictionary action)
        {
            if (objects.Resolve(action["URI"]) is PdfReadString uri) return Encoding.ASCII.GetString(uri.Bytes);
            destination = objects.Resolve(action["D"]);
        }
        var page = Assert.IsType<PdfReadReference>(Assert.IsType<PdfReadArray>(destination)[0]);
        return page.ObjectNumber.ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #endregion
}
