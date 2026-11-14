using System.Text;
using System.Text.RegularExpressions;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Copy and search return the original text (AC-06, D-005, V-07): reordered and formed syllables in Devanagari,
/// Telugu, and Tamil, and mixed lines, read back exactly through <c>ActualText</c> spans; text drawn unchanged needs
/// no span; every drawn glyph has a <c>ToUnicode</c> entry, a character glyph always stands for the same character,
/// and the actual text is written as hex (A05).
/// </summary>
public class ActualTextTests
{
    #region Fields and Constants

    private const string Deva = "कषसरिी्◌ ";
    private const string Telu = "కిై్ ";
    private const string Taml = "கொ ";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Reordered_and_formed_devanagari_syllables_read_back_exactly()
    {
        var (font, glyphs) = Devanagari();
        const string text = "क्षि स्कि र्की";

        var (pdf, content) = Save(text, font);

        Assert.Equal(text, PageText(pdf));
        Assert.Contains(Span("क्षि"), content);
        Assert.Contains(Span("स्कि"), content);
        Assert.Contains(Span("र्की"), content);
        var toUnicode = Assert.Single(PdfInspector.Read(pdf).Fonts()).ToUnicode;
        Assert.Equal("क्ष", toUnicode[glyphs["kssa"]]);
        Assert.Equal("स्", toUnicode[glyphs["saHalf"]]);
        Assert.Equal("र्", toUnicode[glyphs["reph"]]);
        Assert.Equal("ि", toUnicode[glyphs["ि"]]);
    }

    [Fact]
    public void Telugu_stacks_and_split_vowel_signs_read_back_exactly()
    {
        var builder = new TestFontBuilder("Test Telugu");
        var g = Map(builder, Telu);
        int kaBelow = builder.AddGlyph(500, TestFontBuilder.Box(50, -300, 450, 0));
        var gsub = new L();
        gsub.AddFeature("blwf", gsub.AddLookup(4, 0, Ligature(g["్"], new[] { g["క"] }, kaBelow)));
        var font = Build(builder, gsub, "tel2");
        const string text = "క్కై కి";

        var (pdf, content) = Save(text, font);

        Assert.Equal(text, PageText(pdf));
        Assert.Contains(Span("క్కై"), content);
        Assert.DoesNotContain(Span("కి"), content); // drawn unchanged: ToUnicode alone reads it back
        var toUnicode = Assert.Single(PdfInspector.Read(pdf).Fonts()).ToUnicode;
        Assert.Equal("ె", toUnicode[g["ె"]]);
        Assert.Equal("ౖ", toUnicode[g["ౖ"]]);
    }

    [Fact]
    public void Tamil_pre_base_and_split_vowel_signs_read_back_exactly()
    {
        var builder = new TestFontBuilder("Test Tamil");
        Map(builder, Taml);
        var font = Build(builder, new L(), "tml2");
        const string text = "கொ கெ கா";

        var (pdf, content) = Save(text, font);

        Assert.Equal(text, PageText(pdf));
        Assert.Empty(new PdfDocument().FindMissingCharacters(text, font)); // the font maps only the parts of O
        Assert.Contains(Span("கொ"), content);
        Assert.Contains(Span("கெ"), content);
        Assert.DoesNotContain(Span("கா"), content);
    }

    [Fact]
    public void A_mixed_line_reads_back_exactly_as_one_line()
    {
        var latin = Latin("Invoice: 42", out _);
        var (deva, _) = Devanagari();
        var telugu = new TestFontBuilder("Test Telugu");
        Map(telugu, Telu);
        var tamil = new TestFontBuilder("Test Tamil");
        Map(tamil, Taml);
        var document = new PdfDocument();
        document.FontFallback.Add(deva);
        document.FontFallback.Add(Build(telugu, new L(), "tel2"));
        document.FontFallback.Add(Build(tamil, new L(), "tml2"));
        const string text = "Invoice: क्षि కై கொ 42";
        document.AddPage().AddText(text, latin, 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(text, PageText(pdf));
        Assert.Equal(4, PdfInspector.Read(pdf).Fonts().Count);
        Assert.Single(Regex.Matches(Assert.Single(PdfPageReader.Read(pdf)).Content, "BT "));
    }

    [Fact]
    public void A_dotted_circle_the_shaper_adds_is_not_part_of_the_text()
    {
        var (font, glyphs) = Devanagari();
        const string text = "ि";

        var (pdf, content) = Save(text, font);

        Assert.Equal(text, PageText(pdf));
        Assert.Contains(Span("ि"), content);
        Assert.Contains($"<{glyphs["◌"]:X4}>", content);
    }

    [Fact]
    public void Every_drawn_glyph_has_a_ToUnicode_entry()
    {
        var (font, _) = Devanagari();

        var (pdf, _) = Save("क्षि स्कि र्की ि कक", font);

        var embedded = Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.All(embedded.Widths.Keys, glyph => Assert.False(string.IsNullOrEmpty(embedded.ToUnicode.GetValueOrDefault(glyph)), $"glyph {glyph}"));
    }

    [Fact]
    public void Text_drawn_unchanged_needs_no_span()
    {
        var font = Latin("Hello", out _);

        var (pdf, content) = Save("Hello", font);

        Assert.Equal("Hello", PageText(pdf));
        Assert.DoesNotContain("BDC", content);
    }

    [Fact]
    public void A_character_that_shares_a_glyph_reads_back_through_a_span()
    {
        // The font draws the no-break space and carriage return with the space glyph; the glyph means the space.
        var font = Latin("ab ", out var glyphs, 0x00A0, 0x000D);
        const string text = "a b a b";

        var (pdf, content) = Save(text, font);

        Assert.Equal(text, PageText(pdf));
        Assert.Contains(Span(" "), content);
        Assert.Equal(" ", Assert.Single(PdfInspector.Read(pdf).Fonts()).ToUnicode[glyphs[' ']]);
    }

    [Fact]
    public void Actual_text_is_written_as_hex()
    {
        var builder = new TestFontBuilder("Test Latin");
        var g = Map(builder, "f()");
        int parens = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        var gsub = new L();
        gsub.AddFeature("liga", gsub.AddLookup(4, 0, Ligature(g["("], new[] { g[")"] }, parens)));
        var font = Build(builder, gsub, "latn");

        var (pdf, content) = Save("f()", font);

        Assert.Equal("f()", PageText(pdf));
        Assert.Contains("/Span << /ActualText <FEFF00280029> >> BDC", content);
        Assert.DoesNotContain("(()", content);
    }

    [Fact]
    public void A_glyph_stands_for_its_lowest_non_control_code_point()
    {
        // Format 12 (the generated fonts' full-repertoire map).
        var builder = new TestFontBuilder();
        builder.MapCharacters("A ");
        int space = builder.GlyphCount - 1, unmapped = builder.AddGlyph(500, TestFontBuilder.Box(0, 0, 10, 10));
        builder.Map(0x000D, space).Map(0x00A0, space);
        var map = PdfFont.FromBytes(builder.Build()).Font.CharacterMap;

        Assert.Equal(0x20, map.CodePointFor(space));
        Assert.Equal('A', map.CodePointFor(space - 1));
        Assert.Equal(-1, map.CodePointFor(unmapped));
        Assert.Equal(-1, map.CodePointFor(0));

        // Format 4 with a delta segment and a glyph-array segment.
        var bmp = new TestFontBuilder();
        bmp.MapCharacters("AB ");
        bmp.AddTable("cmap", Format4Cmap(a: 1, b: 2, space: 3));
        var bmpMap = PdfFont.FromBytes(bmp.Build()).Font.CharacterMap;

        Assert.Equal(0x20, bmpMap.CodePointFor(3));
        Assert.Equal('A', bmpMap.CodePointFor(1));
        Assert.Equal('B', bmpMap.CodePointFor(2));
    }

    #endregion

    #region Private Methods

    private static PdfFont Build(TestFontBuilder builder, L gsub, string script)
    {
        builder.AddTable("GSUB", gsub.Build(script));
        return PdfFont.FromBytes(builder.Build());
    }

    // A Devanagari font with a KSSA conjunct (akhn), a half SA (half), and a reph (rphf).
    private static (PdfFont Font, Dictionary<string, int> Glyphs) Devanagari()
    {
        var builder = new TestFontBuilder("Test Deva");
        var g = Map(builder, Deva);
        g["kssa"] = builder.AddGlyph(700, TestFontBuilder.Box(40, 0, 660, 700));
        g["saHalf"] = builder.AddGlyph(400, TestFontBuilder.Box(40, 0, 360, 700));
        g["reph"] = builder.AddGlyph(0, TestFontBuilder.Box(-200, 750, 0, 900));
        var gsub = new L();
        gsub.AddFeature("akhn", gsub.AddLookup(4, 0, Ligature(g["क"], new[] { g["्"], g["ष"] }, g["kssa"])));
        gsub.AddFeature("rphf", gsub.AddLookup(4, 0, Ligature(g["र"], new[] { g["्"] }, g["reph"])));
        gsub.AddFeature("half", gsub.AddLookup(4, 0, Ligature(g["स"], new[] { g["्"] }, g["saHalf"])));
        return (Build(builder, gsub, "dev2"), g);
    }

    // A cmap with one format 4 subtable: CR and space map to the space glyph by delta, A and B through the glyph array.
    private static byte[] Format4Cmap(int a, int b, int space)
    {
        var w = new BigEndianWriter();
        w.U16(0).U16(1).U16(3).U16(1).U32(12); // version, one record: Windows Unicode BMP at offset 12
        const int segments = 4;
        w.U16(4).U16(16 + 8 * segments + 4).U16(0).U16(2 * segments).U16(8).U16(2).U16(0);
        w.U16(0x000D).U16(0x0020).U16(0x0042).U16(0xFFFF).U16(0); // end codes, pad
        w.U16(0x000D).U16(0x0020).U16(0x0041).U16(0xFFFF); // start codes
        w.U16(unchecked((ushort)(space - 0x0D))).U16(unchecked((ushort)(space - 0x20))).U16(0).U16(1); // deltas
        w.U16(0).U16(0).U16(4).U16(0); // range offsets: segment 2 reads the glyph array 4 bytes on
        w.U16((ushort)a).U16((ushort)b); // glyph array
        return w.ToArray();
    }

    // A font with a box glyph per character; each of spaceAliases maps to the space glyph too.
    private static PdfFont Latin(string characters, out Dictionary<char, int> glyphs, params int[] spaceAliases)
    {
        var builder = new TestFontBuilder("Test Latin");
        glyphs = Map(builder, characters).ToDictionary(m => m.Key[0], m => m.Value);
        foreach (int alias in spaceAliases) builder.Map(alias, glyphs[' ']);
        return PdfFont.FromBytes(builder.Build());
    }

    private static byte[] Ligature(int first, int[] rest, int ligature) =>
        L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [first] = new[] { (rest, ligature) } });

    // Maps each distinct character to a new box glyph and returns the glyph of each character.
    private static Dictionary<string, int> Map(TestFontBuilder builder, string characters)
    {
        var glyphs = new Dictionary<string, int>();
        foreach (char c in characters.Distinct())
        {
            glyphs[c.ToString()] = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
            builder.Map(c, glyphs[c.ToString()]);
        }
        return glyphs;
    }

    private static string PageText(byte[] pdf)
    {
        var errors = new List<string>();
        string text = Assert.Single(PdfInspector.Read(pdf).PageText(errors));
        Assert.Empty(errors);
        return text;
    }

    private static (byte[] Pdf, string Content) Save(string text, PdfFont font)
    {
        var document = new PdfDocument();
        document.AddPage().AddText(text, font, 72, 720);
        var pdf = PdfPageReader.SaveBytes(document);
        PdfStructureChecker.AssertValid(pdf);
        return (pdf, Assert.Single(PdfPageReader.Read(pdf)).Content);
    }

    private static string Span(string text) => $"/Span << /ActualText <FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}> >> BDC";

    #endregion

    #endregion
}
