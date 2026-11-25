using UnlimitedPDF.Models.Internal.Text;
using UnlimitedPDF.Tests.Support;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Arabic shaping (WR-039; V-01, V-04, V-08) with a generated font whose isol, init, medi, and fina lookups give each
/// letter a glyph per form, whose rlig lookup joins Lam and Alef, and whose mark lookup places a fatha: joining by
/// Unicode joining type with transparent marks skipped, ZWJ and tatweel joining, ZWNJ and non-joining letters breaking;
/// Persian and Urdu letters; every entry point that is not HTML copies the input; Arabic-Indic digits keep their order.
/// </summary>
public class ArabicShaperTests
{
    #region Fields and Constants

    // Letters mapped to glyphs 1 to 12 in this order: beh, alef, lam, hamza, farsi yeh, yeh barree, fatha, tatweel, ZWJ,
    // ZWNJ, space, and the Arabic-Indic digits one to three after them.
    private const string Letters = "بالءیےَـ‍‌ ١٢٣";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Three_dual_joining_letters_take_initial_medial_and_final_forms()
    {
        var (font, forms) = Font();

        var glyphs = Shape(font, "ببب");

        // Drawn left to right: the final form first (it is read last).
        Assert.Equal(new[] { forms[('ب', "fina")], forms[('ب', "medi")], forms[('ب', "init")] }, glyphs);
    }

    [Fact]
    public void A_right_joining_letter_joins_only_to_the_letter_before_it()
    {
        var (font, forms) = Font();

        Assert.Equal(new[] { forms[('ا', "fina")], forms[('ب', "init")] }, Shape(font, "با"));
        Assert.Equal(new[] { forms[('ب', "isol")], forms[('ا', "isol")] }, Shape(font, "اب"));
    }

    [Fact]
    public void A_transparent_mark_does_not_break_joining()
    {
        var (font, forms) = Font();

        var glyphs = Shape(font, "بَب");

        Assert.Equal(forms[('ب', "fina")], glyphs[0]);
        Assert.Equal(forms[('ب', "init")], glyphs[^1]);
    }

    [Fact]
    public void Zwnj_breaks_joining_and_zwj_and_tatweel_cause_it()
    {
        var (font, forms) = Font();

        Assert.Equal(new[] { forms[('ب', "isol")], forms[('ب', "isol")] }, Shape(font, "ب‌ب").Where(g => g != Glyph('‌')));
        Assert.Equal(forms[('ب', "init")], Shape(font, "ب‍").Single(g => g != Glyph('‍')));
        Assert.Equal(forms[('ب', "init")], Shape(font, "بـ")[^1]);
        Assert.Equal(forms[('ب', "fina")], Shape(font, "ـب")[0]);
    }

    [Fact]
    public void A_non_joining_letter_breaks_joining()
    {
        var (font, forms) = Font();

        Assert.Equal(new[] { forms[('ب', "isol")], Glyph('ء'), forms[('ب', "isol")] }, Shape(font, "بءب"));
    }

    [Fact]
    public void Persian_and_urdu_letters_join_by_their_types()
    {
        var (font, forms) = Font();

        Assert.Equal(new[] { forms[('ی', "fina")], forms[('ب', "init")] }, Shape(font, "بی")); // farsi yeh, dual joining
        Assert.Equal(new[] { forms[('ے', "fina")], forms[('ب', "init")] }, Shape(font, "بے")); // yeh barree, right joining
    }

    [Fact]
    public void Lam_and_alef_form_their_required_ligature()
    {
        var (font, forms) = Font();

        var run = Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout("لا", font, Array.Empty<PdfFont>(), 12)!.Segments)).Run;

        Assert.Equal(new[] { forms[('ل', "rlig")] }, run.Glyphs.Select(g => g.Glyph));
    }

    [Fact]
    public void A_fatha_sits_on_its_letter()
    {
        var (font, forms) = Font();

        var run = Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout("بَ", font, Array.Empty<PdfFont>(), 12)!.Segments)).Run;

        var letter = Assert.Single(run.Glyphs, g => g.Glyph == forms[('ب', "isol")]);
        var mark = Assert.Single(run.Glyphs, g => g.Glyph == Glyph('َ'));
        Assert.Equal(letter.X + 250, mark.X);
        Assert.Equal(500, mark.Y);
    }

    [Theory]
    [InlineData(new[] { "D", "D", "D" }, new[] { "init", "medi", "fina" })]
    [InlineData(new[] { "R", "D" }, new[] { "isol", "isol" })]
    [InlineData(new[] { "D", "T", "R" }, new[] { "init", null, "fina" })]
    [InlineData(new[] { "D", "U", "D" }, new[] { "isol", null, "isol" })]
    [InlineData(new[] { "C", "D", "C" }, new[] { null, "medi", null })]
    [InlineData(new[] { "L", "R" }, new[] { "init", "fina" })]
    public void Forms_follow_the_joining_types(string[] types, string?[] expected)
    {
        var map = new Dictionary<string, JoiningType>
        {
            ["D"] = JoiningType.DualJoining, ["R"] = JoiningType.RightJoining, ["L"] = JoiningType.LeftJoining,
            ["U"] = JoiningType.NonJoining, ["C"] = JoiningType.JoinCausing, ["T"] = JoiningType.Transparent
        };
        var names = new Dictionary<int, string> { [3] = "isol", [4] = "fina", [7] = "medi", [9] = "init" };

        var forms = ArabicShaper.Forms(types.Select(t => map[t]).ToArray());

        Assert.Equal(expected, forms.Select(f => f is { } bit ? names[bit] : null));
    }

    [Theory]
    [MemberData(nameof(RightToLeftScriptTests.EntryPoints), MemberType = typeof(RightToLeftScriptTests))]
    public void Arabic_draws_on_every_entry_point_and_copies_as_typed(string entryPoint)
    {
        var (font, _) = Font();
        foreach (string text in new[] { "بباب", "لا بَ", "ب ١٢٣", "بی بے" })
        {
            var pdf = RightToLeftScriptTests.Save(entryPoint, text, font);

            PdfStructureChecker.AssertValid(pdf);
            Assert.Equal(text, RightToLeftScriptTests.PageText(pdf));
            Rendering.RenderTestFiles.AssertDrawnWithTheSameText(pdf);
        }
    }

    [Fact]
    public void Arabic_indic_digits_keep_their_order_inside_arabic_text()
    {
        var (font, _) = Font();

        var layout = TextPipeline.Layout("ب ١٢٣", font, Array.Empty<PdfFont>(), 12)!;

        var drawn = layout.Segments.Cast<FontSegment>().SelectMany(s => s.Run.Glyphs).Select(g => g.Glyph).ToArray();
        Assert.Equal(new[] { Glyph('١'), Glyph('٢'), Glyph('٣') }, drawn.Take(3));
        // The digits resolve to a left-to-right level of their own, so they are a run of their own, drawn first.
        Assert.Equal(new[] { "١٢٣", "ب " }, layout.Segments.Select(s => s.Text));
    }

    #endregion

    #region Private Methods

    private static int Glyph(char c) => Letters.IndexOf(c) + 1;

    private static int[] Shape(PdfFont font, string text) =>
        Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout(text, font, Array.Empty<PdfFont>(), 12)!.Segments)).Run.Glyphs.Select(g => g.Glyph).ToArray();

    // The font: a glyph per letter form for beh, alef, lam, farsi yeh, and yeh barree; lam's initial form and alef's
    // final form ligate to one glyph; a fatha attaches 250 units right of and 500 above the isolated beh's origin.
    private static (PdfFont Font, Dictionary<(char Letter, string Form), int> Forms) Font()
    {
        var builder = new TestFontBuilder("Test Arabic");
        builder.MapCharacters(Letters);
        var forms = new Dictionary<(char, string), int>();
        var byFeature = new Dictionary<string, Dictionary<int, int>> { ["isol"] = new(), ["init"] = new(), ["medi"] = new(), ["fina"] = new() };
        foreach (var (letter, letterForms) in new[] { ('ب', "isol init medi fina"), ('ا', "isol fina"), ('ل', "isol init medi fina"), ('ی', "isol init medi fina"), ('ے', "isol fina") })
        {
            foreach (string form in letterForms.Split(' '))
            {
                int glyph = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
                forms[(letter, form)] = glyph;
                byFeature[form][Glyph(letter)] = glyph;
            }
        }
        int lamAlef = builder.AddGlyph(700, TestFontBuilder.Box(40, 0, 660, 700));
        forms[('ل', "rlig")] = lamAlef;

        var gsub = new L();
        foreach (string feature in new[] { "isol", "init", "medi", "fina" })
            gsub.AddFeature(feature, gsub.AddLookup(1, 0, L.SingleSubst(byFeature[feature])));
        gsub.AddFeature("rlig", gsub.AddLookup(4, 0, L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [forms[('ل', "init")]] = new[] { (new[] { forms[('ا', "fina")] }, lamAlef) } })));
        builder.AddTable("GSUB", gsub.Build("arab"));

        var gpos = new L();
        gpos.AddFeature("mark", gpos.AddLookup(4, 0, L.MarkAttach(new[] { (Glyph('َ'), 0, (short)50, (short)0) }, new[] { (forms[('ب', "isol")], new[] { ((short)300, (short)500) }) })));
        builder.AddTable("GPOS", gpos.Build("arab"));
        var classes = forms.Values.ToDictionary(g => g, _ => 1);
        foreach (char c in Letters) classes[Glyph(c)] = c == 'َ' ? 3 : 1;
        builder.AddTable("GDEF", L.Gdef(classes));
        return (PdfFont.FromBytes(builder.Build()), forms);
    }

    #endregion

    #endregion
}
