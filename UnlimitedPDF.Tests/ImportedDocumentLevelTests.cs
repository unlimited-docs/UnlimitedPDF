using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Layers keep their on or off state, and document-level content of imported files is never copied (WR-036, AC-08,
/// AC-09).
/// </summary>
public class ImportedDocumentLevelTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Document_level_content_of_imported_files_is_not_copied()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(Fixture()));

        var pdf = ImportedPagesTests.Save(document);
        var catalog = PdfFile.FromBytes(pdf).Source.Catalog;
        string text = Encoding.Latin1.GetString(pdf);

        foreach (var key in new[] { "Metadata", "Names", "OpenAction", "AA", "StructTreeRoot", "PageLabels", "MarkInfo", "AcroForm" })
            Assert.False(catalog.ContainsKey(key), key);
        Assert.DoesNotContain("/Info", text, StringComparison.Ordinal);
        Assert.DoesNotContain("xmpmeta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JavaScript", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Hex("file.txt"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Hex("Someone"), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_without_layers_has_no_layer_list()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1)));

        Assert.DoesNotContain("/OCProperties", Encoding.ASCII.GetString(ImportedPagesTests.Save(document)), StringComparison.Ordinal);
    }

    [Fact]
    public void Layers_used_by_imported_pages_keep_their_state()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(Fixture()));

        var (groups, on, off) = Layers(ImportedPagesTests.Save(document));

        Assert.Equal(new[] { "Visible layer", "Hidden layer" }, groups.Values);
        Assert.Equal(new[] { "Visible layer" }, on.Select(n => groups[n]));
        Assert.Equal(new[] { "Hidden layer" }, off.Select(n => groups[n]));
    }

    [Fact]
    public void Layers_from_two_files_are_all_listed_with_their_own_state()
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(Fixture()));
        document.AddPages(PdfFile.FromBytes(Fixture(baseStateOff: true)));

        var pdf = ImportedPagesTests.Save(document);
        var (groups, on, off) = Layers(pdf);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(4, groups.Count);
        Assert.Equal(new[] { "Visible layer", "Visible layer" }, on.Select(n => groups[n]));
        Assert.Equal(new[] { "Hidden layer", "Hidden layer" }, off.Select(n => groups[n]));
    }

    #endregion

    #region Private Methods

    // One page drawing in two layers (one off by default), a third layer no page uses, and document-level content:
    // metadata, an attachment, an opening script, tags, page labels, form data, and an information dictionary.
    private static byte[] Fixture(bool baseStateOff = false)
    {
        string configuration = baseStateOff ? "/BaseState /OFF /ON [60 0 R]" : "/OFF [61 0 R]";
        return new ReaderFixtureBuilder()
            .AddObject(1, $"<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [60 0 R 61 0 R 62 0 R] /D << {configuration} >> >> /Metadata 70 0 R /Names << /EmbeddedFiles 71 0 R >> /OpenAction 72 0 R /AA << /WC 72 0 R >> /StructTreeRoot 73 0 R /PageLabels << /Nums [0 << /S /r >>] >> /MarkInfo << /Marked true >> /AcroForm << /Fields [] >> >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Resources << /Properties << /oc1 60 0 R /oc2 61 0 R >> >> /Contents 5 0 R /StructParents 0 >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("/OC /oc1 BDC 0 0 m 1 1 l S EMC /OC /oc2 BDC 0 0 m 2 2 l S EMC"))
            .AddObject(60, "<< /Type /OCG /Name (Visible layer) >>")
            .AddObject(61, "<< /Type /OCG /Name (Hidden layer) >>")
            .AddObject(62, "<< /Type /OCG /Name (Unused layer) >>")
            .AddStream(70, "/Type /Metadata /Subtype /XML", Encoding.ASCII.GetBytes("<x:xmpmeta/>"))
            .AddObject(71, "<< /Names [(file.txt) 74 0 R] >>")
            .AddObject(72, "<< /S /JavaScript /JS (app.alert(2)) >>")
            .AddObject(73, "<< /Type /StructTreeRoot >>")
            .AddObject(74, "<< /Type /Filespec /F (file.txt) >>")
            .AddObject(75, "<< /Title (Secret title) /Author (Someone) >>")
            .ToBytes(trailerEntries: "/Root 1 0 R /Info 75 0 R");
    }

    private static string Hex(string text) => string.Concat(Encoding.ASCII.GetBytes(text).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

    private static (Dictionary<int, string> Groups, List<int> On, List<int> Off) Layers(byte[] pdf)
    {
        var output = PdfFile.FromBytes(pdf);
        var table = output.Source.Objects;
        var properties = Assert.IsType<PdfReadDictionary>(table.Resolve(output.Source.Catalog["OCProperties"]));
        var configuration = Assert.IsType<PdfReadDictionary>(table.Resolve(properties["D"]));

        List<int> Numbers(PdfReadValue? value) => Assert.IsType<PdfReadArray>(table.Resolve(value)).Items
            .Select(item => Assert.IsType<PdfReadReference>(item).ObjectNumber).ToList();

        var groups = Numbers(properties["OCGs"]).ToDictionary(
            number => number,
            number => PdfTextString.Decode(Assert.IsType<PdfReadString>(Assert.IsType<PdfReadDictionary>(table.Get(number))["Name"]).Bytes));
        return (groups, Numbers(configuration["ON"]), Numbers(configuration["OFF"]));
    }

    #endregion

    #endregion
}
