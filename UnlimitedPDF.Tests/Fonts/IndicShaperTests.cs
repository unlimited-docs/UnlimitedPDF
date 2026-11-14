using UnlimitedPDF.Models.Internal.Text;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The Indic shaper (AC-05, D-003, V-06): one generated font per rule. Devanagari pre-base vowel signs, conjuncts
/// (<c>akhn</c>), half forms, reph, nukta forms, below-base forms in version-2 and version-1 order, joiners, dotted
/// circles, presentation and mark features; Telugu below-base stacks and split vowel signs; Tamil pre-base and split
/// vowel signs. Every glyph of a syllable shares its cluster.
/// </summary>
public class IndicShaperTests
{
    #region Fields and Constants

    private const char Ka = 'क', Ssa = 'ष', Sa = 'स', Ra = 'र', I = 'ि', Ii = 'ी', Halant = '्', Nukta = '़', Anusvara = 'ं';
    private const char TeKa = 'క', TeI = 'ి', TeHalant = '్', TeE = 'ె', TeAiLength = 'ౖ', TeAi = 'ై', TeRa = 'ర', TeIi = 'ీ';
    private const char TaKa = 'க', TaE = 'ெ', TaAa = 'ா', TaO = 'ொ';
    private const char Zwnj = '‌', Dotted = '◌';

    private const string Devanagari = "कषसरिी़्ं◌‌";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_pre_base_vowel_sign_is_drawn_before_its_consonant()
    {
        var f = new Font(Devanagari);

        var run = f.Shape("" + Ka + I);

        Assert.Equal(new[] { f[I], f[Ka] }, Glyphs(run));
        Assert.All(run.Glyphs, g => Assert.Equal(0, g.Cluster));
    }

    [Fact]
    public void Akhand_conjuncts_form_and_take_the_pre_base_vowel_sign_before_them()
    {
        var f = new Font(Devanagari);
        int kssa = f.AddGlyph();
        f.Gsub.AddFeature("akhn", f.Gsub.AddLookup(4, 0, Ligature(f[Ka], new[] { f[Halant], f[Ssa] }, kssa)));

        Assert.Equal(new[] { kssa }, Glyphs(f.Shape("" + Ka + Halant + Ssa)));
        Assert.Equal(new[] { f[I], kssa }, Glyphs(f.Shape("" + Ka + Halant + Ssa + I)));
    }

    [Fact]
    public void A_half_form_keeps_the_pre_base_vowel_sign_before_the_whole_cluster()
    {
        var f = new Font(Devanagari);
        int saHalf = f.AddGlyph();
        f.Gsub.AddFeature("half", f.Gsub.AddLookup(4, 0, Ligature(f[Sa], new[] { f[Halant] }, saHalf)));

        Assert.Equal(new[] { f[I], saHalf, f[Ka] }, Glyphs(f.Shape("" + Sa + Halant + Ka + I)));
    }

    [Fact]
    public void Without_a_half_form_the_vowel_sign_follows_the_standing_halant()
    {
        var f = new Font(Devanagari);

        Assert.Equal(new[] { f[Sa], f[Halant], f[I], f[Ka] }, Glyphs(f.Shape("" + Sa + Halant + Ka + I)));
    }

    [Fact]
    public void A_non_joiner_after_the_halant_blocks_the_half_form()
    {
        var f = new Font(Devanagari);
        int saHalf = f.AddGlyph();
        f.Gsub.AddFeature("half", f.Gsub.AddLookup(4, 0, Ligature(f[Sa], new[] { f[Halant] }, saHalf)));

        Assert.Equal(new[] { f[Sa], f[Halant], f[Ka] }, Glyphs(f.Shape("" + Sa + Halant + Zwnj + Ka)));
    }

    [Fact]
    public void Ra_and_halant_become_a_reph_after_the_base_and_before_a_post_base_vowel_sign()
    {
        var f = new Font(Devanagari);
        int reph = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));

        Assert.Equal(new[] { f[Ka], reph }, Glyphs(f.Shape("" + Ra + Halant + Ka)));
        Assert.Equal(new[] { f[Ka], reph, f[Ii] }, Glyphs(f.Shape("" + Ra + Halant + Ka + Ii)));
        Assert.Equal(new[] { f[I], f[Ka], reph }, Glyphs(f.Shape("" + Ra + Halant + Ka + I)));
    }

    [Fact]
    public void Without_a_reph_form_ra_stays_a_pre_base_consonant()
    {
        var f = new Font(Devanagari);

        Assert.Equal(new[] { f[Ra], f[Halant], f[Ka] }, Glyphs(f.Shape("" + Ra + Halant + Ka)));
    }

    [Fact]
    public void Nukta_forms_apply()
    {
        var f = new Font(Devanagari);
        int qa = f.AddGlyph();
        f.Gsub.AddFeature("nukt", f.Gsub.AddLookup(4, 0, Ligature(f[Ka], new[] { f[Nukta] }, qa)));

        Assert.Equal(new[] { qa }, Glyphs(f.Shape("" + Ka + Nukta)));
    }

    [Fact]
    public void A_below_base_form_makes_the_first_consonant_the_base_in_version_2_order()
    {
        var f = new Font(Devanagari);
        int raBelow = f.AddGlyph();
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, Ligature(f[Halant], new[] { f[Ra] }, raBelow)));

        Assert.Equal(new[] { f[Ka], raBelow }, Glyphs(f.Shape("" + Ka + Halant + Ra)));
        Assert.Equal(new[] { f[I], f[Ka], raBelow }, Glyphs(f.Shape("" + Ka + Halant + Ra + I)));
    }

    [Fact]
    public void A_version_1_font_gets_the_halant_after_the_below_base_consonant()
    {
        var f = new Font(Devanagari, script: "deva");
        int raBelow = f.AddGlyph();
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, raBelow)));

        Assert.Equal(new[] { f[Ka], raBelow }, Glyphs(f.Shape("" + Ka + Halant + Ra)));
    }

    [Fact]
    public void A_lone_vowel_sign_gets_a_dotted_circle()
    {
        var f = new Font(Devanagari);

        var run = f.Shape("" + I);

        Assert.Equal(new[] { f[I], f[Dotted] }, Glyphs(run));
        Assert.All(run.Glyphs, g => Assert.Equal(0, g.Cluster));
    }

    [Fact]
    public void Presentation_features_run_after_reordering()
    {
        var f = new Font(Devanagari);
        int iWide = f.AddGlyph();
        // pres replaces I when it stands before KA, which is true only after reordering.
        int toWide = f.Gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [f[I]] = iWide }));
        f.Gsub.AddFeature("pres", f.Gsub.AddLookup(6, 0, L.Chain3(Array.Empty<int[]>(), new[] { new[] { f[I] } }, new[] { new[] { f[Ka] } }, (0, toWide))));

        Assert.Equal(new[] { iWide, f[Ka] }, Glyphs(f.Shape("" + Ka + I)));
    }

    [Fact]
    public void Above_base_marks_attach_to_the_base()
    {
        var f = new Font(Devanagari);
        f.Gpos.AddFeature("abvm", f.Gpos.AddLookup(4, 0, L.MarkAttach(new[] { (f[Anusvara], 0, (short)100, (short)0) }, new[] { (f[Ka], new[] { ((short)300, (short)700) }) })));
        f.Gdef = L.Gdef(new Dictionary<int, int> { [f[Anusvara]] = 3, [f[I]] = 3, [f[Halant]] = 3, [f[Nukta]] = 3 });

        var run = f.Shape("" + Ka + Anusvara);

        Assert.Equal((200, 700, 0), (run.Glyphs[1].X, run.Glyphs[1].Y, run.Glyphs[1].Advance));
        Assert.Equal(600, run.Advance);
    }

    [Fact]
    public void Telugu_below_base_stacks_keep_the_first_consonant_as_base_and_top_vowels_in_place()
    {
        var f = new Font("" + TeKa + TeI + TeHalant + TeE + TeAiLength, script: "tel2");
        int kaBelow = f.AddGlyph();
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, Ligature(f[TeHalant], new[] { f[TeKa] }, kaBelow)));

        Assert.Equal(new[] { f[TeKa], kaBelow }, Glyphs(f.Shape("" + TeKa + TeHalant + TeKa)));
        Assert.Equal(new[] { f[TeKa], f[TeI] }, Glyphs(f.Shape("" + TeKa + TeI)));
    }

    [Fact]
    public void Telugu_ra_with_a_pref_form_is_not_the_base_and_its_form_stays_after_the_base()
    {
        // A font that forms the subjoined Ra with pref (not blwf): the consonant before it stays the base, and the formed
        // glyph is drawn where it stands, after the base.
        var f = new Font("" + TeKa + TeHalant + TeRa + TeIi, script: "tel2");
        int raSubjoined = f.AddGlyph();
        f.Gsub.AddFeature("pref", f.Gsub.AddLookup(4, 0, Ligature(f[TeHalant], new[] { f[TeRa] }, raSubjoined)));

        Assert.Equal(new[] { f[TeKa], raSubjoined }, Glyphs(f.Shape("" + TeKa + TeHalant + TeRa)));
        Assert.Equal(new[] { f[TeKa], raSubjoined, f[TeIi] }, Glyphs(f.Shape("" + TeKa + TeHalant + TeRa + TeIi)));
    }

    [Fact]
    public void Telugu_ai_is_split_into_its_two_parts()
    {
        var f = new Font("" + TeKa + TeE + TeAiLength, script: "tel2");

        Assert.Equal(new[] { f[TeKa], f[TeE], f[TeAiLength] }, Glyphs(f.Shape("" + TeKa + TeAi)));
    }

    [Fact]
    public void Tamil_pre_base_and_split_vowel_signs_are_placed_around_the_consonant()
    {
        var f = new Font("" + TaKa + TaE + TaAa, script: "tml2");

        Assert.Equal(new[] { f[TaE], f[TaKa] }, Glyphs(f.Shape("" + TaKa + TaE)));
        Assert.Equal(new[] { f[TaE], f[TaKa], f[TaAa] }, Glyphs(f.Shape("" + TaKa + TaO)));
    }

    [Fact]
    public void Each_syllable_is_one_cluster()
    {
        var f = new Font(Devanagari);

        var run = f.Shape("" + Ka + I + Sa + Ka + I);

        Assert.Equal(new[] { 0, 0, 2, 3, 3 }, run.Glyphs.Select(g => g.Cluster));
    }

    [Fact]
    public void The_pipeline_shapes_devanagari_text_drawn_with_a_caller_font()
    {
        var builder = new TestFontBuilder("Test Deva");
        builder.MapCharacters(Devanagari);
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddText("" + Ka + I, font, 72, 700);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        int ka = font.Font.CharacterMap.GlyphFor(Ka), i = font.Font.CharacterMap.GlyphFor(I);
        Assert.Contains($"[<{i:X4}> <{ka:X4}>] TJ", Assert.Single(PdfPageReader.Read(pdf)).Content);
    }

    #endregion

    #region Private Methods

    private static int[] Glyphs(ShapedRun run) => run.Glyphs.Select(g => g.Glyph).ToArray();

    private static byte[] Ligature(int first, int[] rest, int ligature) =>
        L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [first] = new[] { (rest, ligature) } });

    #endregion

    #endregion

    #region Nested Types

    // A generated font: one box glyph per character, extra glyphs for forms, and layout tables under one script tag.
    private sealed class Font
    {
        private readonly TestFontBuilder _builder;
        private readonly string _script;
        private PdfFont? _font;

        public Font(string characters, string script = "dev2")
        {
            _builder = new TestFontBuilder("Test Indic");
            _builder.MapCharacters(characters);
            _script = script;
            foreach (char c in characters) Map[c] = _builder.GlyphCount - characters.Length + characters.IndexOf(c);
        }

        public byte[]? Gdef { get; set; }

        public L Gpos { get; } = new();

        public L Gsub { get; } = new();

        private Dictionary<char, int> Map { get; } = new();

        public int this[char c] => Map[c];

        public int AddGlyph() => _builder.AddGlyph(500, TestFontBuilder.Box(50, 0, 450, 700));

        public ShapedRun Shape(string text)
        {
            if (_font is null)
            {
                _builder.AddTable("GSUB", Gsub.Build(_script));
                _builder.AddTable("GPOS", Gpos.Build(_script));
                if (Gdef is not null) _builder.AddTable("GDEF", Gdef);
                _font = PdfFont.FromBytes(_builder.Build());
            }
            string script = _script.StartsWith("dev", StringComparison.Ordinal) ? "deva" : _script.StartsWith("tel", StringComparison.Ordinal) ? "telu" : "taml";
            return IndicShaper.Shape(_font, text, script);
        }
    }

    #endregion
}
