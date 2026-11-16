using System.Text.RegularExpressions;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Caller fonts on the programmatic text entry points (AC-03, AC-04, AC-06, AC-07, AC-11, D-006, D-007, WR-039):
/// page text, header and footer text, flow text, flow block text, and table cell text draw with a caller font, fall
/// back per character to the document's fallback fonts and then the built-in font, read back exactly, are placed with
/// their real shaped widths, and write only hex glyph IDs for caller-font text (A05).
/// </summary>
public class CallerFontTextTests
{
    #region Fields and Constants

    private const string Western = "Ça coûte 5 € – « déjà »";
    private const string Telugu = "ఇన్వాయిస్"; // ఇన్వాయిస్

    #endregion

    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> EntryPoints() => new[] { "page", "header", "flow", "block", "cell" }.Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Each_entry_point_draws_with_a_caller_font_and_reads_back(string entryPoint)
    {
        var font = Font(Western);

        var pdf = Save(entryPoint, Western, font);

        PdfStructureChecker.AssertValid(pdf);
        var embedded = Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.Matches("^[A-Z]{6}\\+LatinSans-Regular$", embedded.BaseFont);
        var errors = new List<string>();
        Assert.Equal(Western, Assert.Single(PdfInspector.Read(pdf).PageText(errors)));
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Caller_font_text_is_written_as_glyph_ids_only(string entryPoint)
    {
        const string hostile = "a) Tj /Evil (b";
        var font = Font(hostile);

        var content = Assert.Single(PdfPageReader.Read(Save(entryPoint, hostile, font))).Content;

        Assert.DoesNotContain("Evil", content);
        Assert.Matches(@"/E1 12 Tf 1 0 0 1 [\d.]+ [\d.]+ Tm \[<[0-9A-F]{4}>", content);
    }

    [Fact]
    public void One_line_mixes_scripts_through_the_fallback_fonts()
    {
        var latin = Font("Invoice #1024 ₹,");
        var telugu = Font(Telugu, family: "Telugu Sans");
        var document = new PdfDocument();
        document.FontFallback.Add(telugu);
        string text = "Invoice " + Telugu + " ₹ 1,024";
        document.AddPage().AddText(text, latin, 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var fonts = PdfInspector.Read(pdf).Fonts();
        Assert.Equal(2, fonts.Count);
        var content = Assert.Single(PdfPageReader.Read(pdf)).Content;
        Assert.Single(Regex.Matches(content, "BT ")); // one line, one text object
        Assert.Contains("/E1 12 Tf", content);
        Assert.Contains("/E2 12 Tf", content);
        Assert.Equal(text, Assert.Single(PdfInspector.Read(pdf).PageText()));
    }

    [Fact]
    public void Text_without_a_font_uses_the_fallback_fonts()
    {
        var latin = Font("Hello");
        var document = new PdfDocument();
        document.FontFallback.Add(latin);
        document.AddPage().AddText("Hello", 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.Contains("/E1 12 Tf", Assert.Single(PdfPageReader.Read(pdf)).Content);
        Assert.Equal("Hello", Assert.Single(PdfInspector.Read(pdf).PageText()));
    }

    [Fact]
    public void Characters_no_font_has_use_the_built_in_font_or_become_question_marks()
    {
        var latin = Font("Hello ");
        var document = new PdfDocument();
        document.AddPage().AddText("Hello é త", latin, 72, 720); // é is WinAnsi; Telugu TA is not

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var content = Assert.Single(PdfPageReader.Read(pdf)).Content;
        Assert.Contains("/HF1 12 Tf", content);
        Assert.Equal("Hello é ?", Assert.Single(PdfInspector.Read(pdf).PageText()));
    }

    [Fact]
    public void Centered_and_right_aligned_text_use_the_shaped_width()
    {
        var font = Font("Hi"); // 600 units per glyph: 2 glyphs at 10 pt are 12 pt wide
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Header.AddText("Hi", font, 0, 0, 10, PdfTextAlignment.Center);
        document.Body.AddText("Hi", font, fontSize: 10, alignment: PdfTextAlignment.Right);

        var content = Assert.Single(PdfPageReader.Save(document)).Content;

        Assert.Contains("1 0 0 1 291.64 ", content); // (595.28 - 12) / 2
        Assert.Contains("1 0 0 1 583.28 ", content); // 595.28 - 12
    }

    [Fact]
    public void Table_cells_place_caller_font_text_with_its_real_width()
    {
        var font = Font("Total");
        var document = new PdfDocument();
        var table = document.AddPage().AddTable(50, 700, 200);
        table.SetText(0, 0, "Total", font);
        table.GetCell(0, 0).HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        var content = Assert.Single(PdfPageReader.Save(document)).Content;

        // Right edge 250, padding 4, width 5 x 600 / 1000 x 12 = 36: x = 210 (the old approximation would give 216).
        Assert.Contains("1 0 0 1 210 ", content);
    }

    [Fact]
    public void Kerning_moves_glyphs_and_shortens_the_line()
    {
        var builder = new TestFontBuilder("Latin Sans");
        builder.MapCharacters("AV");
        var gpos = new L();
        gpos.AddFeature("kern", gpos.AddLookup(2, 0, L.PairPos1(new Dictionary<(int, int), short> { [(1, 2)] = -100 })));
        builder.AddTable("GPOS", gpos.Build("latn"));
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.Body.AddText("AV", font, fontSize: 10, alignment: PdfTextAlignment.Right);

        var content = Assert.Single(PdfPageReader.Save(document)).Content;

        // The second glyph moves 100 units left (a TJ number of 100); the line is 1100 units = 11 pt wide.
        Assert.True(content.Contains("[<0001> 100 <0002>] TJ", StringComparison.Ordinal), content);
        Assert.Contains("1 0 0 1 584.28 ", content);
    }

    [Fact]
    public void A_font_on_every_page_header_is_embedded_once_and_saves_identically()
    {
        var font = Font("Page 0123456789of ");
        var document = new PdfDocument();
        document.Header.Height = 30;
        document.Header.AddText("Page {page} of {pages}", font, 0, 0);
        document.AddPage();
        document.AddPage();

        var first = PdfPageReader.SaveBytes(document);

        Assert.Single(PdfInspector.Read(first).Fonts());
        Assert.Equal(new[] { "Page 1 of 2", "Page 2 of 2" }, PdfInspector.Read(first).PageText());
        Assert.Equal(first, PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void Null_fonts_are_rejected()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        Assert.Equal("font", Assert.Throws<ArgumentNullException>(() => page.AddText("a", (PdfFont)null!, 1, 1)).ParamName);
        Assert.Equal("font", Assert.Throws<ArgumentNullException>(() => document.Header.AddText("a", (PdfFont)null!, 1, 1)).ParamName);
        Assert.Equal("font", Assert.Throws<ArgumentNullException>(() => document.Body.AddText("a", (PdfFont)null!)).ParamName);
        Assert.Equal("font", Assert.Throws<ArgumentNullException>(() => document.Body.AddBlock().AddText("a", (PdfFont)null!)).ParamName);
        Assert.Equal("font", Assert.Throws<ArgumentNullException>(() => new PdfCellText("a", null!)).ParamName);
        Assert.Equal("font", Assert.Throws<ArgumentNullException>(() => page.AddTable(1, 1, 1).SetText(0, 0, "a", null!)).ParamName);
        Assert.Throws<ArgumentNullException>(() => document.FontFallback.Add(null!));
    }

    #endregion

    #region Private Methods

    private static PdfFont Font(string characters, string family = "Latin Sans")
    {
        var builder = new TestFontBuilder(family);
        builder.MapCharacters(characters);
        return PdfFont.FromBytes(builder.Build());
    }

    private static byte[] Save(string entryPoint, string text, PdfFont font)
    {
        var document = new PdfDocument();
        switch (entryPoint)
        {
            case "page":
                document.AddPage().AddText(text, font, 50, 700);
                break;
            case "header":
                document.Header.Height = 40;
                document.Header.AddText(text, font, 0, 0);
                document.AddPage();
                break;
            case "flow":
                document.Body.AddText(text, font);
                break;
            case "block":
                document.Body.AddBlock().AddText(text, font);
                break;
            case "cell":
                document.AddPage().AddTable(50, 700, 400).SetText(0, 0, text, font);
                break;
        }
        return PdfPageReader.SaveBytes(document);
    }

    #endregion

    #endregion
}
