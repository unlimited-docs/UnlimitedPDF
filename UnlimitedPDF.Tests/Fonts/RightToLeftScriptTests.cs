using System.Text;
using UnlimitedPDF.Models.Internal.Text;
using UnlimitedPDF.Tests.Support;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Right-to-left text in the text pipeline (WR-039, WR-041; V-01, V-06, V-07, V-08): Hebrew and Thaana with generated
/// fonts draw right to left on every entry point that is not HTML, with their own script's font rules and their marks
/// on their letters; mixed lines keep left-to-right words and numbers in order; mirrored characters take their mirror
/// glyph; copied text equals the input; left-to-right text keeps its output.
/// </summary>
public class RightToLeftScriptTests
{
    #region Fields and Constants

    private const string Hebrew = "שלום";
    private const string Thaana = "ހެލޯ";

    #endregion

    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> EntryPoints() => new[] { "page", "header", "flow", "block", "cell" }.Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Hebrew_and_thaana_draw_on_every_entry_point_and_copy_as_typed(string entryPoint)
    {
        var font = Font(Hebrew + Thaana + " 12()");
        foreach (string text in new[] { Hebrew, Thaana, Hebrew + " 12 " + Thaana })
        {
            var pdf = Save(entryPoint, text, font);

            PdfStructureChecker.AssertValid(pdf);
            Assert.Equal(text, PageText(pdf));
            Rendering.RenderTestFiles.AssertDrawnWithTheSameText(pdf);
        }
    }

    [Fact]
    public void A_hebrew_word_is_drawn_from_the_right()
    {
        var font = Font(Hebrew);

        var layout = TextPipeline.Layout(Hebrew, font, Array.Empty<PdfFont>(), 12)!;

        var segment = Assert.IsType<FontSegment>(Assert.Single(layout.Segments));
        Assert.Equal(Glyphs(font, "םולש"), segment.Run.Glyphs.Select(g => g.Glyph));
        Assert.Equal(new[] { 0, 600, 1200, 1800 }, segment.Run.Glyphs.Select(g => g.X));
        Assert.True(layout.Reordered);
        Assert.True(layout.RightToLeft);
        Assert.Equal(Hebrew, layout.Text);
    }

    [Fact]
    public void A_mixed_line_orders_its_runs_and_keeps_latin_and_numbers_left_to_right()
    {
        var font = Font("אבגדה abc12() ");

        var layout = TextPipeline.Layout("אבג (abc 12) דה", font, Array.Empty<PdfFont>(), 12)!;

        // Drawn left to right: "הד", then the left-to-right "(abc 12)" with mirrored brackets around it, then "גבא".
        string drawn = string.Concat(layout.Segments.Cast<FontSegment>().SelectMany(s => s.Run.Glyphs).Select(g => CharacterOf(font, g.Glyph)));
        Assert.Equal("הד (abc 12) גבא", drawn);
        Assert.Equal("אבג (abc 12) דה", layout.Text);
    }

    [Fact]
    public void A_mirrored_character_without_a_mirror_glyph_uses_the_fonts_rtlm_feature()
    {
        var builder = new TestFontBuilder("Test Mirror");
        builder.MapCharacters("א<");
        int mirrored = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        var gsub = new L();
        gsub.AddFeature("rtlm", gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [2] = mirrored })));
        builder.AddTable("GSUB", gsub.Build("hebr"));
        var font = PdfFont.FromBytes(builder.Build());

        var rtl = TextPipeline.Layout("א<", font, Array.Empty<PdfFont>(), 12)!;
        var ltr = TextPipeline.Layout("a<", Font("a<"), Array.Empty<PdfFont>(), 12)!;

        Assert.Contains(mirrored, Assert.IsType<FontSegment>(Assert.Single(rtl.Segments)).Run.Glyphs.Select(g => g.Glyph));
        Assert.DoesNotContain(ltr.Segments.Cast<FontSegment>().SelectMany(s => s.Run.Glyphs), g => g.Glyph > 2);
    }

    [Fact]
    public void Hebrew_points_and_thaana_vowel_signs_sit_on_their_letters()
    {
        foreach (var (script, letter, mark) in new[] { ("hebr", "ב", "ָ"), ("thaa", "ހ", "ަ") })
        {
            var builder = new TestFontBuilder("Test " + script);
            builder.MapCharacters(letter + mark);
            var gpos = new L();
            gpos.AddFeature("mark", gpos.AddLookup(4, 0, L.MarkAttach(new[] { (2, 0, (short)100, (short)0) }, new[] { (1, new[] { ((short)300, (short)-150) }) })));
            builder.AddTable("GPOS", gpos.Build(script));
            builder.AddTable("GDEF", L.Gdef(new Dictionary<int, int> { [1] = 1, [2] = 3 }));
            var font = PdfFont.FromBytes(builder.Build());

            var run = Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout(letter + mark, font, Array.Empty<PdfFont>(), 12)!.Segments)).Run;

            // Visual order: the mark comes first in the reversed run but sits at the letter's anchor.
            var letterGlyph = Assert.Single(run.Glyphs, g => g.Glyph == 1);
            var markGlyph = Assert.Single(run.Glyphs, g => g.Glyph == 2);
            Assert.Equal(letterGlyph.X + 300 - 100, markGlyph.X);
            Assert.Equal(-150, markGlyph.Y);
            Assert.Equal(0, markGlyph.Advance);
            Assert.Equal(600, run.Advance);
        }
    }

    [Theory]
    [InlineData("hebr", "שלום")]
    [InlineData("thaa", "ހެލޯ")]
    public void Hebrew_and_thaana_use_their_own_script_tags(string script, string text)
    {
        Assert.Equal(new[] { script, "DFLT", "dflt", "latn" }, TextPipeline.ScriptTags(script));
        var segment = Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout(text, Font(text), Array.Empty<PdfFont>(), 12)!.Segments));
        Assert.Equal(text, segment.Text);
    }

    [Fact]
    public void Bidirectional_controls_draw_nothing_and_stay_in_the_copied_text()
    {
        var font = Font("abc א");
        string text = "‏abc ⁧א⁩";

        var layout = TextPipeline.Layout(text, font, Array.Empty<PdfFont>(), 12)!;

        Assert.Equal(5 * 600 * 12 / 1000.0, layout.Width, 6); // a, b, c, space, alef
        Assert.Equal(text, layout.Text);
    }

    [Fact]
    public void Left_to_right_text_keeps_the_left_to_right_path()
    {
        var font = Font("Hello 12");

        var layout = TextPipeline.Layout("Hello 12", font, Array.Empty<PdfFont>(), 12)!;

        Assert.False(layout.Reordered);
        Assert.False(layout.RightToLeft);
        var content = Assert.Single(PdfPageReader.Read(Save("page", "Hello 12", font))).Content;
        Assert.DoesNotContain("ActualText", content);
    }

    [Fact]
    public void A_reordered_line_is_one_actual_text_span()
    {
        var font = Font(Hebrew);
        var content = Assert.Single(PdfPageReader.Read(Save("page", Hebrew, font))).Content;

        Assert.Contains($"/Span << /ActualText <FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(Hebrew))}> >> BDC", content);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(content, "/ActualText"));
    }

    #endregion

    #region Internal Methods

    internal static PdfFont Font(string characters)
    {
        var builder = new TestFontBuilder("Test RTL");
        builder.MapCharacters(string.Concat(characters.Distinct()));
        return PdfFont.FromBytes(builder.Build());
    }

    internal static byte[] Save(string entryPoint, string text, PdfFont font)
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

    internal static string PageText(byte[] pdf)
    {
        var errors = new List<string>();
        string text = Assert.Single(PdfInspector.Read(pdf).PageText(errors));
        Assert.Empty(errors);
        return text;
    }

    #endregion

    #region Private Methods

    private static int[] Glyphs(PdfFont font, string text) => text.Select(c => font.Font.CharacterMap.GlyphFor(c)).ToArray();

    private static string CharacterOf(PdfFont font, int glyph) => char.ConvertFromUtf32(font.Font.CharacterMap.CodePointFor(glyph));

    #endregion

    #endregion
}
