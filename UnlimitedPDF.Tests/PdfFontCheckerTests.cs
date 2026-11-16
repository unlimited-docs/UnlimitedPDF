using System.IO.Compression;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The checker's embedded-font checks and text extraction (T-09, PR-010): a well-formed Type0 font passes and its
/// text reads back through <c>ToUnicode</c> and <c>ActualText</c>; each defect the font checks exist for is reported.
/// </summary>
public class PdfFontCheckerTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_well_formed_embedded_font_passes_and_reads_back()
    {
        var pdf = FontPdf().ToBytes();

        Assert.Empty(PdfStructureChecker.Check(pdf));
        var inspector = PdfInspector.Read(pdf);
        var font = Assert.Single(inspector.Fonts());
        Assert.Equal("ABCDEF+TestSans", font.BaseFont);
        Assert.Equal("CIDFontType2", font.CidFontSubtype);
        Assert.Equal(600, font.Widths[1]);
        Assert.Equal("H", font.ToUnicode[1]);
        Assert.Equal(new HashSet<int> { 0, 1, 2 }, font.GlyphsWithOutlines.ToHashSet());

        var errors = new List<string>();
        Assert.Equal(new[] { "Hi\nकि" }, inspector.PageText(errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void A_compressed_font_program_is_inflated()
    {
        var program = Program();
        var pdf = FontPdf(programStream: (Compress(program), "/Filter /FlateDecode")).ToBytes();

        Assert.Empty(PdfStructureChecker.Check(pdf));
        Assert.Equal(program, Assert.Single(PdfInspector.Read(pdf).Fonts()).Program);
    }

    [Fact]
    public void A_font_without_identity_h_is_reported()
    {
        AssertReports(FontPdf(type0: Type0().Replace("/Identity-H", "/WinAnsiEncoding")).ToBytes(), "does not use /Encoding /Identity-H");
    }

    [Fact]
    public void A_missing_to_unicode_map_is_reported()
    {
        AssertReports(FontPdf(type0: Type0().Replace(" /ToUnicode 8 0 R", string.Empty)).ToBytes(), "has no /ToUnicode map");
    }

    [Theory]
    [InlineData("/ABCDEF+Test_Sans")]
    [InlineData("/abcdef+TestSans")]
    [InlineData("/ABCDEF+TestSans#29Tj")]
    public void An_unsafe_font_name_is_reported(string name)
    {
        var pdf = FontPdf(type0: Type0().Replace("/ABCDEF+TestSans", name)).ToBytes();

        AssertReports(pdf, "unsafe or malformed /BaseFont");
    }

    [Fact]
    public void A_missing_descendant_font_is_reported()
    {
        AssertReports(FontPdf(type0: Type0().Replace("[5 0 R]", "[]")).ToBytes(), "exactly one descendant CIDFont");
    }

    [Fact]
    public void A_missing_font_program_is_reported()
    {
        AssertReports(FontPdf(descriptor: Descriptor().Replace(" /FontFile2 7 0 R", string.Empty)).ToBytes(), "has no /FontFile2 font program");
    }

    [Fact]
    public void A_missing_descriptor_entry_is_reported()
    {
        AssertReports(FontPdf(descriptor: Descriptor().Replace(" /CapHeight 700", string.Empty)).ToBytes(), "has no /CapHeight");
    }

    [Fact]
    public void A_width_that_differs_from_the_program_is_reported()
    {
        AssertReports(FontPdf(cidFont: CidFont().Replace("[1 [600 600]]", "[1 [500 600]]")).ToBytes(), "/W width 500 of glyph 1 differs");
    }

    [Fact]
    public void A_damaged_program_table_is_reported()
    {
        var program = Program();
        var (glyf, _) = EmbeddedFont.SfntTables(program)["glyf"];
        program[glyf + 12] ^= 0xFF;

        AssertReports(FontPdf(programStream: (program, string.Empty)).ToBytes(), "wrong checksum for its glyf table");
    }

    [Fact]
    public void A_program_without_required_tables_is_reported()
    {
        var program = Program();
        int count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(program.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            if (Encoding.ASCII.GetString(program, 12 + 16 * i, 4) == "hmtx") Encoding.ASCII.GetBytes("xmtx").CopyTo(program, 12 + 16 * i);
        }

        AssertReports(FontPdf(programStream: (program, string.Empty)).ToBytes(), "has no hmtx table");
    }

    [Fact]
    public void A_cid_keyed_cff_program_passes_and_a_name_keyed_one_is_reported()
    {
        AssertCff(cidKeyed: true, expectedError: null);
        AssertCff(cidKeyed: false, expectedError: "is not a CID-keyed CFF font");
    }

    [Fact]
    public void A_glyph_code_without_a_to_unicode_entry_is_reported_by_text_extraction()
    {
        var pdf = FontPdf(content: "BT /E1 12 Tf 72 720 Td <00010009> Tj ET").ToBytes();
        var errors = new List<string>();

        Assert.Equal(new[] { "H�" }, PdfInspector.Read(pdf).PageText(errors));
        Assert.Contains(errors, e => e.Contains("glyph code 9 of font /E1 has no ToUnicode entry"));
    }

    [Fact]
    public void Unbalanced_marked_content_and_unknown_fonts_are_reported_by_text_extraction()
    {
        var errors = new List<string>();
        PdfInspector.Read(FontPdf(content: "BT /E1 12 Tf <0001> Tj EMC ET BT /X9 12 Tf <0001> Tj ET").ToBytes()).PageText(errors);

        Assert.Contains(errors, e => e.Contains("EMC without a matching BDC"));
        Assert.Contains(errors, e => e.Contains("font resource /X9 is missing"));
    }

    [Fact]
    public void Text_the_library_writes_reads_back()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Invoice (1024)", 72, 720);
        page.AddText("Café – 5 €", 72, 700);
        document.Header.Height = 30;
        document.Header.AddText("Page {page} of {pages}", 0, 0);

        var errors = new List<string>();
        var text = PdfInspector.Read(PdfPageReader.SaveBytes(document)).PageText(errors);

        Assert.Empty(errors);
        Assert.Equal("Invoice (1024)\nCafé – 5 €\nPage 1 of 1", Assert.Single(text));
    }

    [Fact]
    public void Pages_are_read_in_page_order()
    {
        var document = new PdfDocument();
        for (int i = 1; i <= 3; i++) document.AddPage().AddText("page " + i, 72, 720);

        var text = PdfInspector.Read(PdfPageReader.SaveBytes(document)).PageText();

        Assert.Equal(new[] { "page 1", "page 2", "page 3" }, text);
    }

    #endregion

    #region Private Methods

    private static void AssertCff(bool cidKeyed, string? expectedError)
    {
        var builder = new TestFontBuilder { Cff = true, CffCidKeyed = cidKeyed };
        builder.MapCharacters("Hi");
        var font = builder.Build();
        var (offset, length) = EmbeddedFont.SfntTables(font)["CFF "];
        var cff = font.AsSpan(offset, length).ToArray();

        var pdf = FontPdf(
                cidFont: CidFont().Replace("/CIDFontType2", "/CIDFontType0").Replace(" /CIDToGIDMap /Identity", string.Empty).Replace("[1 [600 600]]", "[]"),
                descriptor: Descriptor().Replace("/FontFile2", "/FontFile3"),
                programStream: (cff, "/Subtype /CIDFontType0C"))
            .ToBytes();

        var errors = PdfStructureChecker.Check(pdf);
        if (expectedError is null) Assert.Empty(errors);
        else Assert.Contains(errors, e => e.Contains(expectedError));
    }

    private static void AssertReports(byte[] pdf, string expected)
    {
        var errors = PdfStructureChecker.Check(pdf);
        Assert.True(errors.Any(e => e.Contains(expected, StringComparison.Ordinal)), "Expected an error containing '" + expected + "', got: " + string.Join(" | ", errors));
    }

    private static string CidFont() =>
        "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /ABCDEF+TestSans /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 6 0 R /CIDToGIDMap /Identity /W [1 [600 600]] >>";

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data);
        return output.ToArray();
    }

    private static string Descriptor() =>
        "<< /Type /FontDescriptor /FontName /ABCDEF+TestSans /Flags 4 /FontBBox [0 -200 1000 900] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 7 0 R >>";

    private static RawPdfBuilder FontPdf(string? type0 = null, string? cidFont = null, string? descriptor = null, (byte[] Data, string Entries)? programStream = null, string? content = null)
    {
        var (program, entries) = programStream ?? (Program(), string.Empty);
        const string toUnicode = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Test def 1 begincodespacerange <0000> <FFFF> endcodespacerange "
                                 + "3 beginbfchar <0001> <0048> <0002> <0069> <0003> <0915093F> endbfchar endcmap CMapName currentdict /CMap defineresource pop end end";
        content ??= "BT /E1 12 Tf 72 720 Td <00010002> Tj ET BT /E1 12 Tf 72 700 Td /Span << /ActualText <FEFF0915093F> >> BDC <00020001> Tj EMC ET";
        return new RawPdfBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /E1 4 0 R >> >> /Contents 9 0 R >>")
            .AddObject(4, type0 ?? Type0())
            .AddObject(5, cidFont ?? CidFont())
            .AddObject(6, descriptor ?? Descriptor())
            .AddStream(7, entries, program)
            .AddStream(8, string.Empty, Encoding.ASCII.GetBytes(toUnicode))
            .AddStream(9, string.Empty, Encoding.ASCII.GetBytes(content));
    }

    private static byte[] Program() => new TestFontBuilder().MapCharacters("Hi").Build();

    private static string Type0() =>
        "<< /Type /Font /Subtype /Type0 /BaseFont /ABCDEF+TestSans /Encoding /Identity-H /DescendantFonts [5 0 R] /ToUnicode 8 0 R >>";

    #endregion

    #endregion
}
