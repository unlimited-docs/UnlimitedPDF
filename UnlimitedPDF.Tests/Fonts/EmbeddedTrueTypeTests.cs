using System.Buffers.Binary;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Embedding TrueType subsets (AC-09, D-010, PR-022, A05, A08): a document embeds each caller font once, keeping only
/// the outlines its pages draw (plus <c>.notdef</c> and composite parts) unless the font forbids subsetting; the font
/// name is sanitized; text reads back through <c>ToUnicode</c>; saving twice gives the same bytes.
/// </summary>
public class EmbeddedTrueTypeTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_subset_keeps_only_the_glyphs_drawn()
    {
        var (font, glyphs) = Font("Hello, world");
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hello"), 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var embedded = Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.Equal(new[] { 0 }.Concat("Helo".Select(c => glyphs[c])).OrderBy(g => g), embedded.GlyphsWithOutlines.OrderBy(g => g));
        Assert.Equal(font.Font.GlyphCount, embedded.GlyphCount); // glyph IDs are kept
        Assert.Equal("CIDFontType2", embedded.CidFontSubtype);
        Assert.Matches("^[A-Z]{6}\\+TestSans-Regular$", embedded.BaseFont);
        Assert.Equal(new HashSet<int>("Helo".Select(c => glyphs[c])), embedded.Widths.Keys.ToHashSet());
        Assert.Equal(600, embedded.Widths[glyphs['H']]);
    }

    [Fact]
    public void Text_reads_back_through_the_to_unicode_map()
    {
        var (font, _) = Font("Hello త");
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hello త"), 72, 720);

        var errors = new List<string>();
        var text = PdfInspector.Read(PdfPageReader.SaveBytes(document)).PageText(errors);

        Assert.Empty(errors);
        Assert.Equal("Hello త", Assert.Single(text));
    }

    [Fact]
    public void The_parts_of_a_composite_glyph_are_kept()
    {
        var builder = new TestFontBuilder();
        builder.MapCharacters("ab");
        int dot = builder.AddGlyph(200, TestFontBuilder.Box(0, 0, 100, 100));
        int pair = builder.AddCompositeGlyph(600, (dot, 0, 0), (dot, 300, 0));
        builder.Map(':', pair);
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, new[] { (pair, ":") }, 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(new[] { 0, dot, pair }, Assert.Single(PdfInspector.Read(pdf).Fonts()).GlyphsWithOutlines.OrderBy(g => g));
    }

    [Fact]
    public void A_font_used_on_several_pages_is_embedded_once_with_every_page_glyph()
    {
        var (font, glyphs) = Font("Hello, world");
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "He"), 72, 720);
        document.AddPage().AddGlyphs(font, Run(font, "wo"), 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var embedded = Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.Equal(new[] { 0 }.Concat("Hewo".Select(c => glyphs[c])).OrderBy(g => g), embedded.GlyphsWithOutlines.OrderBy(g => g));
        Assert.Equal(new[] { "He", "wo" }, PdfInspector.Read(pdf).PageText());
        var resources = PdfPageReader.Read(pdf);
        Assert.All(resources, page => Assert.Contains("/E1 ", page.Content));
    }

    [Fact]
    public void Each_font_gets_its_own_face()
    {
        var (first, _) = Font("ab");
        var (second, _) = Font("cd", family: "Second Sans");
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddGlyphs(first, Run(first, "a"), 72, 720);
        page.AddGlyphs(second, Run(second, "c"), 72, 700);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var fonts = PdfInspector.Read(pdf).Fonts();
        Assert.Equal(2, fonts.Count);
        Assert.Contains(fonts, f => f.BaseFont.EndsWith("+TestSans-Regular", StringComparison.Ordinal));
        Assert.Contains(fonts, f => f.BaseFont.EndsWith("+SecondSans-Regular", StringComparison.Ordinal));
        var content = Assert.Single(PdfPageReader.Read(pdf)).Content;
        Assert.Contains("BT /E1 12 Tf", content);
        Assert.Contains("BT /E2 12 Tf", content);
    }

    [Fact]
    public void A_font_that_forbids_subsetting_is_embedded_whole()
    {
        var (font, _) = Font("Hello, world", fsType: 0x0100);
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "H"), 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(Enumerable.Range(0, font.Font.GlyphCount).ToHashSet(), Assert.Single(PdfInspector.Read(pdf).Fonts()).GlyphsWithOutlines.ToHashSet());
    }

    [Fact]
    public void A_restricted_font_loaded_with_the_override_is_embedded()
    {
        var builder = new TestFontBuilder { FsType = 0x0002 };
        builder.MapCharacters("Hi");
        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { IgnoreEmbeddingRestrictions = true });
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hi"), 72, 720);

        PdfStructureChecker.AssertValid(PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void A_hostile_font_name_is_sanitized()
    {
        var builder = new TestFontBuilder { PostScriptName = "x) Tj /Evil <<>> [" };
        builder.MapCharacters("Hi");
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hi"), 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Matches("^[A-Z]{6}\\+xTjEvil$", Assert.Single(PdfInspector.Read(pdf).Fonts()).BaseFont);
        Assert.DoesNotContain("Evil", Assert.Single(PdfPageReader.Read(pdf)).Content);
    }

    [Fact]
    public void Saving_twice_gives_the_same_bytes()
    {
        var (font, _) = Font("Hello, world");
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hello"), 72, 720);

        Assert.Equal(PdfPageReader.SaveBytes(document), PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void A_later_save_rewrites_the_face_with_the_new_glyphs()
    {
        var (font, glyphs) = Font("Hello, world");
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddGlyphs(font, Run(font, "He"), 72, 720);
        var first = PdfPageReader.SaveBytes(document);
        page.AddGlyphs(font, Run(font, "wo"), 72, 700);

        var second = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(second);
        var before = Assert.Single(PdfInspector.Read(first).Fonts());
        var after = Assert.Single(PdfInspector.Read(second).Fonts());
        Assert.Equal(before.ObjectNumber, after.ObjectNumber); // the face's objects are rewritten, not added
        Assert.NotEqual(before.BaseFont, after.BaseFont); // a different subset gets a different tag
        Assert.Contains(glyphs['w'], after.GlyphsWithOutlines);
        Assert.DoesNotContain(glyphs['w'], before.GlyphsWithOutlines);
    }

    [Fact]
    public void A_variable_font_embeds_its_chosen_weight()
    {
        var builder = new TestFontBuilder { WeightAxis = (100, 400, 900) };
        int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        builder.Map('A', glyph);
        builder.AddWeightDeltas(glyph, new (short, short)[] { (0, 0), (0, 0), (100, 0), (100, 0), (0, 0), (100, 0), (0, 0), (0, 0) });
        var font = PdfFont.FromBytes(builder.Build(), new PdfFontLoadOptions { Weight = 900 });
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, new[] { (glyph, "A") }, 72, 720);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        var embedded = Assert.Single(PdfInspector.Read(pdf).Fonts());
        Assert.Equal(700, embedded.Widths[glyph]);
        var tables = EmbeddedFont.SfntTables(embedded.Program);
        Assert.False(tables.ContainsKey("gvar"));
        int start = tables["glyf"].Offset + EmbeddedFont.TrueTypeGlyphOffsets(embedded.Program)[glyph];
        Assert.Equal(660, BinaryPrimitives.ReadInt16BigEndian(embedded.Program.AsSpan(start + 6))); // xMax
    }

    [Fact]
    public void Glyph_ids_are_written_as_hex_strings()
    {
        var (font, glyphs) = Font("Hi");
        var document = new PdfDocument();
        document.AddPage().AddGlyphs(font, Run(font, "Hi"), 72.5, 720, 14);

        var content = Assert.Single(PdfPageReader.Save(document)).Content;

        Assert.Contains($"BT /E1 14 Tf 72.5 720 Td <{glyphs['H']:X4}{glyphs['i']:X4}> Tj ET", content);
    }

    #endregion

    #region Private Methods

    private static (PdfFont Font, Dictionary<char, int> Glyphs) Font(string characters, ushort fsType = 0, string family = "Test Sans")
    {
        var builder = new TestFontBuilder(family) { FsType = fsType };
        builder.MapCharacters(characters);
        var font = PdfFont.FromBytes(builder.Build());
        var glyphs = characters.Distinct().ToDictionary(c => c, c => font.Font.CharacterMap.GlyphFor(c));
        return (font, glyphs);
    }

    private static (int Glyph, string Text)[] Run(PdfFont font, string text) =>
        text.Select(c => (font.Font.CharacterMap.GlyphFor(c), c.ToString())).ToArray();

    #endregion

    #endregion
}
