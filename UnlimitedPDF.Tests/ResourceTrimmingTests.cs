using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// A trimmed import carries only the fonts and images its pages use, even when every page shares one resource
/// dictionary; an untrimmed import carries everything as before; a page whose content cannot be read keeps all its
/// resources and stays complete (WR-040, AC-11, AC-12, D-017).
/// </summary>
public class ResourceTrimmingTests
{
    #region Fields and Constants

    private const int FontCount = 12;
    private const int FormBytes = 100_000;
    private const int ImageCount = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void One_page_of_a_shared_dictionary_carries_only_its_own_font_and_image()
    {
        var output = Import(Shared(), trim: true, 1);

        Assert.Equal(1, Count(output, "Font"));
        Assert.Equal(1, Count(output, "Image"));
        Assert.Equal(new[] { "Page 2" }, ImportedPagesTests.PageTexts(output));
    }

    [Fact]
    public void An_untrimmed_import_carries_every_resource()
    {
        var output = Import(Shared(), trim: false, 1);

        Assert.Equal(FontCount, Count(output, "Font"));
        Assert.Equal(ImageCount, Count(output, "Image"));
    }

    [Fact]
    public void Pages_sharing_a_font_copy_it_once()
    {
        var output = Import(Shared(), trim: true, 0, 2, 0);

        // Pages 1 and 3 use fonts F0 and F2 and images Im0 and Im2.
        Assert.Equal(2, Count(output, "Font"));
        Assert.Equal(2, Count(output, "Image"));
    }

    [Fact]
    public void A_page_whose_content_cannot_be_read_keeps_every_resource()
    {
        var bytes = SharedBuilder()
            .AddStream(23, "/Filter /LZWDecode", new byte[] { 0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01 })
            .ToBytes();

        var output = Import(bytes, trim: true, 2);

        Assert.Equal(FontCount, Count(output, "Font"));
        Assert.Equal(ImageCount, Count(output, "Image"));
    }

    [Fact]
    public void A_trimmed_form_is_copied_once_however_often_its_page_is_listed_or_imported()
    {
        // Review R-02: a trimmed copy depends only on its source object, so repeats and later imports reuse it.
        var file = PdfFile.FromBytes(FormSource());
        var document = new PdfDocument();
        document.AddPagesCore(file, new[] { 0, 0, 0, 0 }, null, trimResources: true);
        document.AddPagesCore(file, new[] { 0 }, null, trimResources: true);
        var pdf = ImportedPagesTests.Save(document);
        PdfStructureChecker.AssertValid(pdf);
        var output = ImportedPagesTests.Read(pdf);
        var objects = output.Source.Objects;

        var forms = objects.ObjectNumbers.Select(n => objects.Get(n)).OfType<PdfReadStream>()
            .Where(s => (s.Dictionary["Subtype"] as PdfReadName)?.Value == "Form")
            .ToList();
        var form = Assert.Single(forms);
        Assert.Equal(new[] { "FA" }, Assert.IsType<PdfReadDictionary>(objects.Resolve(Assert.IsType<PdfReadDictionary>(objects.Resolve(form.Dictionary["Resources"]))["Font"])).Keys);
        Assert.Equal(5, output.PageCount);
        Assert.True(pdf.Length < 2 * FormBytes, $"{pdf.Length} bytes for five copies of a page drawing a {FormBytes}-byte form.");
    }

    [Fact]
    public void A_painted_form_field_keeps_its_appearance_and_its_font()
    {
        var bytes = SharedBuilder()
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents 21 0 R /Annots [60 0 R] >>")
            .AddObject(60, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Alice) /Rect [100 100 200 120] /AP << /N 61 0 R >> >>")
            .AddStream(61, "/Type /XObject /Subtype /Form /BBox [0 0 100 20] /Resources << /Font << /FA 30 0 R /FB 31 0 R >> >>", Encoding.ASCII.GetBytes("BT /FA 10 Tf 2 5 Td (Alice) Tj ET"))
            .ToBytes();

        var output = Import(bytes, trim: true, 0);
        var objects = output.Source.Objects;
        var resources = Assert.IsType<PdfReadDictionary>(objects.Resolve(output.Pages[0].Dictionary["Resources"]));
        var xObjects = Assert.IsType<PdfReadDictionary>(objects.Resolve(resources["XObject"]));
        var field = Assert.IsType<PdfReadStream>(objects.Resolve(xObjects["UnlimitedField1"]));
        var fieldFonts = Assert.IsType<PdfReadDictionary>(objects.Resolve(Assert.IsType<PdfReadDictionary>(objects.Resolve(field.Dictionary["Resources"]))["Font"]));

        Assert.Equal(new[] { "Im0", "UnlimitedField1" }, xObjects.Keys);
        Assert.Equal(new[] { "FA" }, fieldFonts.Keys);
        Assert.Equal(1, Count(output, "Font"));
    }

    #endregion

    #region Private Methods

    // Every font and image object in the output, leaving out the document's own font (object 3, WR-002).
    private static int Count(PdfFile output, string kind)
    {
        var objects = output.Source.Objects;
        return objects.ObjectNumbers
            .Where(number => number != 3)
            .Select(number => objects.Get(number))
            .Count(value => kind == "Font"
                ? value is PdfReadDictionary { } d && (d["Type"] as PdfReadName)?.Value == "Font"
                : value is PdfReadStream s && (s.Dictionary["Subtype"] as PdfReadName)?.Value == "Image");
    }

    // A page that draws one large form; the form has its own fonts FA and FB and uses FA.
    private static byte[] FormSource() => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [4 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
        .AddObject(4, "<< /Type /Page /Parent 2 0 R /Resources << /XObject << /Fm0 6 0 R >> >> /Contents 5 0 R >>")
        .AddStream(5, "", Encoding.ASCII.GetBytes("/Fm0 Do"))
        .AddStream(6, "/Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << /Font << /FA 30 0 R /FB 31 0 R >> >>",
            Encoding.ASCII.GetBytes("BT /FA 12 Tf 72 720 Td (Form) Tj ET " + new string('%', FormBytes)))
        .AddObject(30, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
        .AddObject(31, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>")
        .ToBytes();

    private static PdfFile Import(byte[] source, bool trim, params int[] pageIndexes)
    {
        var document = new PdfDocument();
        document.AddPagesCore(PdfFile.FromBytes(source), pageIndexes, null, trim);
        var pdf = ImportedPagesTests.Save(document);
        PdfStructureChecker.AssertValid(pdf);
        return ImportedPagesTests.Read(pdf);
    }

    private static byte[] Shared() => SharedBuilder().ToBytes();

    // Three pages under one page tree whose resource dictionary (object 3) holds 12 fonts (30 to 41) and 6 images
    // (50 to 55); page n draws font F(n-1) and image Im(n-1).
    private static ReaderFixtureBuilder SharedBuilder()
    {
        string fonts = string.Concat(Enumerable.Range(0, FontCount).Select(i => $"/F{i} {30 + i} 0 R "));
        string images = string.Concat(Enumerable.Range(0, ImageCount).Select(i => $"/Im{i} {50 + i} 0 R "));
        var builder = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R 5 0 R 6 0 R] /Count 3 /MediaBox [0 0 612 792] /Resources 3 0 R >>")
            .AddObject(3, $"<< /ProcSet [/PDF /Text /ImageC] /Font << {fonts}>> /XObject << {images}>> >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents 21 0 R >>")
            .AddObject(5, "<< /Type /Page /Parent 2 0 R /Contents 22 0 R >>")
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /Contents 23 0 R >>");
        for (int page = 1; page <= 3; page++)
            builder.AddStream(20 + page, "", Encoding.ASCII.GetBytes($"q 10 0 0 10 0 0 cm /Im{page - 1} Do Q BT /F{page - 1} 12 Tf 72 720 Td (Page {page}) Tj ET"));
        for (int i = 0; i < FontCount; i++)
            builder.AddObject(30 + i, $"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Name /F{i} >>");
        for (int i = 0; i < ImageCount; i++)
            builder.AddStream(50 + i, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", new byte[] { (byte)i });
        return builder;
    }

    #endregion

    #endregion
}
