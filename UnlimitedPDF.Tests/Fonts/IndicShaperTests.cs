using UnlimitedPDF.Models.Internal.Text;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The Indic shaper (AC-05, D-003, V-06): one generated font per rule. Devanagari pre-base vowel signs, conjuncts
/// (<c>akhn</c>), half forms, reph, nukta forms, below-base forms in version-2 and version-1 order, joiners, dotted
/// circles, presentation and mark features; Telugu below-base stacks and split vowel signs; Tamil pre-base and split
/// vowel signs. Every glyph of a syllable shares its cluster. Bengali-Assamese, Gujarati, and Gurmukhi (WR-039): pre-base
/// and split vowel signs, reph before a post-base form, khanda ta and the explicit half form, the Assamese Ra, a joiner
/// before a halant, word-initial init, below-base forms in both tag versions, and marks. Odia and Kannada: split vowel
/// signs (Kannada's in three parts), the reph after the base (Odia) or last before the modifiers (Kannada), and Kannada
/// vowel signs before below-base forms. Malayalam: the pre-base Ra form moves before its base (or the conjunct that took
/// the base in), chillus in both spellings, the dot reph, and split vowel signs.
/// </summary>
public class IndicShaperTests
{
    #region Fields and Constants

    private const char Ka = 'क', Ssa = 'ष', Sa = 'स', Ra = 'र', I = 'ि', Ii = 'ी', Halant = '्', Nukta = '़', Anusvara = 'ं';
    private const char TeKa = 'క', TeI = 'ి', TeHalant = '్', TeE = 'ె', TeAiLength = 'ౖ', TeAi = 'ై', TeRa = 'ర', TeIi = 'ీ';
    private const char TaKa = 'க', TaE = 'ெ', TaAa = 'ா', TaO = 'ொ';
    private const char Zwnj = '‌', Zwj = '‍', Dotted = '◌';

    private const char BnKa = 'ক', BnRa = 'র', BnAsRa = 'ৰ', BnYa = 'য', BnTa = 'ত', BnKhandaTa = 'ৎ';
    private const char BnI = 'ি', BnE = 'ে', BnAa = 'া', BnO = 'ো', BnAuLength = 'ৗ', BnAu = 'ৌ';
    private const char BnHalant = '্', BnA = 'অ';
    private const char GuKa = 'ક', GuRa = 'ર', GuI = 'િ', GuIi = 'ી', GuHalant = '્';
    private const char PaKa = 'ਕ', PaRa = 'ਰ', PaI = 'ਿ', PaHalant = '੍', PaTippi = 'ੰ', PaAddak = 'ੱ';

    private const char OrKa = 'କ', OrRa = 'ର', OrYa = 'ଯ', OrE = 'େ', OrAa = 'ା', OrAiLength = 'ୖ';
    private const char OrAuLength = 'ୗ', OrAi = 'ୈ', OrO = 'ୋ', OrAu = 'ୌ', OrHalant = '୍';
    private const char KnKa = 'ಕ', KnRa = 'ರ', KnTta = 'ಟ', KnU = 'ು', KnAa = 'ಾ', KnE = 'ೆ', KnUu = 'ೂ';
    private const char KnLength = 'ೕ', KnAiLength = 'ೖ', KnI = 'ಿ', KnOo = 'ೋ', KnIi = 'ೀ', KnAi = 'ೈ';
    private const char KnHalant = '್', KnAnusvara = 'ಂ';

    private const char MlKa = 'ക', MlNa = 'ന', MlTa = 'ത', MlRa = 'ര', MlLa = 'ല', MlChilluL = 'ൽ', MlE = 'െ', MlAa = 'ാ', MlO = 'ൊ', MlHalant = '്', MlDotReph = 'ൎ';

    private const string Bengali = "কৰরযতৎিোৗ্অ‍‌◌";
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
    public void Ra_and_halant_become_a_reph_after_the_base_and_its_vowel_signs()
    {
        // WR-039: the Devanagari reph goes after the vowel signs that precede the post-base forms, so a font's reph +
        // vowel sign ligatures form (1.1.0 placed it before a post-base vowel sign).
        var f = new Font(Devanagari);
        int reph = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));

        Assert.Equal(new[] { f[Ka], reph }, Glyphs(f.Shape("" + Ra + Halant + Ka)));
        Assert.Equal(new[] { f[Ka], f[Ii], reph }, Glyphs(f.Shape("" + Ra + Halant + Ka + Ii)));
        Assert.Equal(new[] { f[I], f[Ka], reph }, Glyphs(f.Shape("" + Ra + Halant + Ka + I)));
        Assert.Equal(new[] { f[Ka], f[Ii], reph, f[Anusvara] }, Glyphs(f.Shape("" + Ra + Halant + Ka + Ii + Anusvara)));
    }

    [Fact]
    public void A_devanagari_reph_goes_before_a_post_base_form()
    {
        var f = new Font(Devanagari + "\u092F");
        int reph = f.AddGlyph(), yaPost = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));
        f.Gsub.AddFeature("pstf", f.Gsub.AddLookup(4, 0, Ligature(f[Halant], new[] { f['\u092F'] }, yaPost)));

        Assert.Equal(new[] { f[Ka], reph, yaPost }, Glyphs(f.Shape("" + Ra + Halant + Ka + Halant + '\u092F')));
        Assert.Equal(new[] { f[Ka], f[Ii], reph, yaPost }, Glyphs(f.Shape("" + Ra + Halant + Ka + Halant + '\u092F' + Ii)));
    }

    [Fact]
    public void Devanagari_takes_the_explicit_half_form_and_a_joiner_before_a_halant()
    {
        // WR-039: halant + ZWJ at the end of a syllable asks for the half form; a joiner before a halant stays in the
        // syllable (no dotted circle) and asks for no reph.
        var f = new Font(Devanagari + Zwj + "\u092F");
        int kaHalf = f.AddGlyph(), reph = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));
        f.Gsub.AddFeature("half", f.Gsub.AddLookup(4, 0, Ligature(f[Ka], new[] { f[Halant] }, kaHalf)));

        Assert.Equal(new[] { kaHalf }, Glyphs(f.Shape("" + Ka + Halant + Zwj)));
        var run = f.Shape("" + Ra + Zwj + Halant + '\u092F');
        Assert.Equal(new[] { f[Ra], f[Halant], f['\u092F'] }, Glyphs(run));
        Assert.All(run.Glyphs, g => Assert.Equal(0, g.Cluster));
    }

    [Theory]
    [InlineData(Zwj)]
    [InlineData(Zwnj)]
    public void A_joiner_after_ra_and_halant_asks_for_no_reph(char joiner)
    {
        // RV-06: ZWJ asks for an eyelash Ra, ZWNJ for a visible halant; neither is a reph.
        var f = new Font(Devanagari + Zwj);
        int reph = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));

        var glyphs = Glyphs(f.Shape("" + Ra + Halant + joiner + Ka));

        Assert.DoesNotContain(reph, glyphs);
        Assert.Equal(new[] { f[Ra], f[Halant] }, glyphs.Take(2));
        Assert.Equal(f[Ka], glyphs[^1]);
    }

    [Fact]
    public void A_reph_moves_after_a_conjunct_that_took_in_the_base()
    {
        // RV-07: akhn forms KSSA from the base, so no glyph keeps the base class; the reph still goes after it.
        var f = new Font(Devanagari);
        int reph = f.AddGlyph(), kssa = f.AddGlyph();
        f.Gsub.AddFeature("akhn", f.Gsub.AddLookup(4, 0, Ligature(f[Ka], new[] { f[Halant], f[Ssa] }, kssa)));
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));

        Assert.Equal(new[] { kssa, reph }, Glyphs(f.Shape("" + Ra + Halant + Ka + Halant + Ssa)));
        Assert.Equal(new[] { kssa, f[Ii], reph }, Glyphs(f.Shape("" + Ra + Halant + Ka + Halant + Ssa + Ii)));
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
    public void Threads_that_share_a_new_font_shape_as_one_thread_does()
    {
        // QF-26112701: a font keeps its shaping plans and lookup caches, filled on first use; threads that start on a
        // new font together must each get what one thread gets.
        static Font Build()
        {
            var f = new Font(Devanagari);
            int reph = f.AddGlyph(), raBelow = f.AddGlyph(), kssa = f.AddGlyph();
            f.Gsub.AddFeature("akhn", f.Gsub.AddLookup(4, 0, Ligature(f[Ka], new[] { f[Halant], f[Ssa] }, kssa)));
            f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[Ra], new[] { f[Halant] }, reph)));
            f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, Ligature(f[Halant], new[] { f[Ra] }, raBelow)));
            return f;
        }
        string text = "" + Ra + Halant + Ka + I + Ka + Halant + Ra + I + Ka + Halant + Ssa + I + Sa + Ii + Anusvara;
        var expected = Glyphs(Build().Shape(text));
        var shared = Build();
        var results = new int[64][];

        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => results[i] = Glyphs(shared.Shape(text)));

        Assert.All(results, glyphs => Assert.Equal(expected, glyphs));
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
        // glyph is drawn where it stands, after the base. A top vowel sign goes before it (WR-039; 1.1.0 put it after).
        var f = new Font("" + TeKa + TeHalant + TeRa + TeIi, script: "tel2");
        int raSubjoined = f.AddGlyph();
        f.Gsub.AddFeature("pref", f.Gsub.AddLookup(4, 0, Ligature(f[TeHalant], new[] { f[TeRa] }, raSubjoined)));

        Assert.Equal(new[] { f[TeKa], raSubjoined }, Glyphs(f.Shape("" + TeKa + TeHalant + TeRa)));
        Assert.Equal(new[] { f[TeKa], f[TeIi], raSubjoined }, Glyphs(f.Shape("" + TeKa + TeHalant + TeRa + TeIi)));
    }

    [Theory]
    [InlineData("tel2")]
    [InlineData("telu")]
    public void Telugu_vowel_signs_go_before_or_after_a_below_base_form_by_the_model(string tag)
    {
        // WR-039: vowel signs up to U+0C42 and every top or bottom sign go before the below-base form, so a font's
        // consonant + vowel sign forms can join; U+0C43 and later right signs go after it.
        const char u = 'ు', r = 'ృ';
        var f = new Font("" + TeKa + TeI + TeHalant + u + r, script: tag);
        int kaBelow = f.AddGlyph();
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, tag == "tel2"
            ? Ligature(f[TeHalant], new[] { f[TeKa] }, kaBelow)
            : Ligature(f[TeKa], new[] { f[TeHalant] }, kaBelow)));

        Assert.Equal(new[] { f[TeKa], f[TeI], kaBelow }, Glyphs(f.Shape("" + TeKa + TeHalant + TeKa + TeI)));
        Assert.Equal(new[] { f[TeKa], f[u], kaBelow }, Glyphs(f.Shape("" + TeKa + TeHalant + TeKa + u)));
        Assert.Equal(new[] { f[TeKa], kaBelow, f[r] }, Glyphs(f.Shape("" + TeKa + TeHalant + TeKa + r)));
    }

    [Fact]
    public void The_explicit_telugu_reph_forms_through_its_joiner_and_goes_last()
    {
        // A font may form the Telugu reph only from Ra + halant + ZWJ; it goes last, before the anusvara.
        const char anusvara = 'ం';
        var f = new Font("" + TeKa + TeRa + TeI + TeHalant + anusvara + Zwj, script: "tel2");
        int reph = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[TeRa], new[] { f[TeHalant], f[Zwj] }, reph)));

        Assert.Equal(new[] { f[TeKa], reph }, Glyphs(f.Shape("" + TeRa + TeHalant + Zwj + TeKa)));
        Assert.Equal(new[] { f[TeKa], f[TeI], reph, f[anusvara] }, Glyphs(f.Shape("" + TeRa + TeHalant + Zwj + TeKa + TeI + anusvara)));
        Assert.DoesNotContain(reph, Glyphs(f.Shape("" + TeRa + TeHalant + TeKa)));
    }

    [Fact]
    public void Telugu_ai_is_split_into_its_two_parts()
    {
        var f = new Font("" + TeKa + TeE + TeAiLength, script: "tel2");

        Assert.Equal(new[] { f[TeKa], f[TeE], f[TeAiLength] }, Glyphs(f.Shape("" + TeKa + TeAi)));
    }

    [Fact]
    public void Tamil_takes_the_model_order_the_explicit_half_form_and_a_joiner_before_a_halant()
    {
        // WR-039: right vowel signs after the post-base forms; halant + ZWJ asks for the half form; a joiner before a
        // halant stays in the syllable.
        const char u = 'ு', ssa = 'ஷ';
        var f = new Font("" + TaKa + TaE + u + ssa + '\u0BCD' + Zwj + Zwnj, script: "tml2");
        int kaHalf = f.AddGlyph();
        f.Gsub.AddFeature("half", f.Gsub.AddLookup(4, 0, Ligature(f[TaKa], new[] { f['\u0BCD'] }, kaHalf)));

        Assert.Equal(new[] { f[TaKa], f[u] }, Glyphs(f.Shape("" + TaKa + u)));
        Assert.Equal(new[] { kaHalf }, Glyphs(f.Shape("" + TaKa + '\u0BCD' + Zwj)));
        var run = f.Shape("" + TaKa + Zwnj + '\u0BCD' + ssa);
        Assert.Equal(new[] { f[TaKa], f['்'], f[ssa] }, Glyphs(run));
        Assert.All(run.Glyphs, g => Assert.Equal(0, g.Cluster));
    }

    [Fact]
    public void Tamil_pre_base_and_split_vowel_signs_are_placed_around_the_consonant()
    {
        var f = new Font("" + TaKa + TaE + TaAa, script: "tml2");

        Assert.Equal(new[] { f[TaE], f[TaKa] }, Glyphs(f.Shape("" + TaKa + TaE)));
        Assert.Equal(new[] { f[TaE], f[TaKa], f[TaAa] }, Glyphs(f.Shape("" + TaKa + TaO)));
    }

    [Fact]
    public void Bengali_pre_base_and_split_vowel_signs_are_placed_around_the_consonant()
    {
        var f = new Font(Bengali, script: "bng2");

        Assert.Equal(new[] { f[BnI], f[BnKa] }, Glyphs(f.Shape("" + BnKa + BnI)));
        Assert.Equal(new[] { f[BnE], f[BnKa], f[BnAa] }, Glyphs(f.Shape("" + BnKa + BnO)));
        Assert.Equal(new[] { f[BnE], f[BnKa], f[BnAuLength] }, Glyphs(f.Shape("" + BnKa + BnAu)));
    }

    [Fact]
    public void A_bengali_reph_goes_before_a_post_base_form()
    {
        // Bengali places the reph after below-base forms and before post-base forms such as ya-phala.
        var f = new Font(Bengali, script: "bng2");
        int reph = f.AddGlyph(), yaPhala = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[BnRa], new[] { f[BnHalant] }, reph)));
        f.Gsub.AddFeature("pstf", f.Gsub.AddLookup(4, 0, Ligature(f[BnHalant], new[] { f[BnYa] }, yaPhala)));

        Assert.Equal(new[] { f[BnKa], reph, yaPhala }, Glyphs(f.Shape("" + BnRa + BnHalant + BnKa + BnHalant + BnYa)));
        Assert.Equal(new[] { f[BnKa], reph, yaPhala, f[BnAa] }, Glyphs(f.Shape("" + BnRa + BnHalant + BnKa + BnHalant + BnYa + BnAa)));
    }

    [Fact]
    public void The_assamese_ra_forms_a_reph()
    {
        var f = new Font(Bengali, script: "bng2");
        int reph = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[BnAsRa], new[] { f[BnHalant] }, reph)));

        Assert.Equal(new[] { f[BnKa], reph }, Glyphs(f.Shape("" + BnAsRa + BnHalant + BnKa)));
    }

    [Fact]
    public void Khanda_ta_is_drawn_for_both_spellings()
    {
        // Halant + ZWJ at the end of a syllable asks for the half form, which Bengali fonts draw as khanda ta.
        var f = new Font(Bengali, script: "bng2");
        int khanda = f[BnKhandaTa];
        f.Gsub.AddFeature("half", f.Gsub.AddLookup(4, 0, Ligature(f[BnTa], new[] { f[BnHalant] }, khanda)));

        Assert.Equal(new[] { khanda }, Glyphs(f.Shape("" + BnTa + BnHalant + Zwj)));
        Assert.Equal(new[] { khanda }, Glyphs(f.Shape("" + BnKhandaTa)));
        Assert.Equal(new[] { f[BnTa], f[BnHalant] }, Glyphs(f.Shape("" + BnTa + BnHalant)));
    }

    [Fact]
    public void A_joiner_before_the_halant_keeps_the_syllable_and_asks_for_no_reph()
    {
        // Ra + ZWJ + halant + Ya writes Ra with ya-phala: one syllable, no dotted circle, no reph.
        var f = new Font(Bengali, script: "bng2");
        int reph = f.AddGlyph(), yaPhala = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[BnRa], new[] { f[BnHalant] }, reph)));
        f.Gsub.AddFeature("pstf", f.Gsub.AddLookup(4, 0, Ligature(f[BnHalant], new[] { f[BnYa] }, yaPhala)));

        var run = f.Shape("" + BnRa + Zwj + BnHalant + BnYa);

        Assert.Equal(new[] { f[BnRa], yaPhala }, Glyphs(run));
        Assert.All(run.Glyphs, g => Assert.Equal(0, g.Cluster));
    }

    [Fact]
    public void Init_applies_only_to_a_word_initial_pre_base_vowel_sign()
    {
        var f = new Font(Bengali, script: "bng2");
        int eInit = f.AddGlyph();
        f.Gsub.AddFeature("init", f.Gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [f[BnE]] = eInit })));

        Assert.Equal(new[] { eInit, f[BnKa] }, Glyphs(f.Shape("" + BnKa + BnE)));
        Assert.Equal(new[] { f[BnA], f[BnE], f[BnKa] }, Glyphs(f.Shape("" + BnA + BnKa + BnE)));
    }

    [Fact]
    public void A_bengali_version_1_font_gets_the_halant_after_the_below_base_consonant()
    {
        var f = new Font(Bengali, script: "beng");
        int raBelow = f.AddGlyph();
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, Ligature(f[BnRa], new[] { f[BnHalant] }, raBelow)));

        Assert.Equal(new[] { f[BnKa], raBelow }, Glyphs(f.Shape("" + BnKa + BnHalant + BnRa)));
        Assert.Equal(new[] { f[BnI], f[BnKa], raBelow }, Glyphs(f.Shape("" + BnKa + BnHalant + BnRa + BnI)));
    }

    [Theory]
    [InlineData("gjr2")]
    [InlineData("gujr")]
    public void Gujarati_shapes_pre_base_vowel_signs_reph_and_below_base_forms(string tag)
    {
        var f = new Font("" + GuKa + GuRa + GuI + GuIi + GuHalant, script: tag);
        int reph = f.AddGlyph(), raBelow = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[GuRa], new[] { f[GuHalant] }, reph)));
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, tag == "gjr2"
            ? Ligature(f[GuHalant], new[] { f[GuRa] }, raBelow)
            : Ligature(f[GuRa], new[] { f[GuHalant] }, raBelow)));

        Assert.Equal(new[] { f[GuI], f[GuKa] }, Glyphs(f.Shape("" + GuKa + GuI)));
        Assert.Equal(new[] { f[GuKa], reph, f[GuIi] }, Glyphs(f.Shape("" + GuRa + GuHalant + GuKa + GuIi)));
        Assert.Equal(new[] { f[GuI], f[GuKa], raBelow }, Glyphs(f.Shape("" + GuKa + GuHalant + GuRa + GuI)));
    }

    [Theory]
    [InlineData("gur2")]
    [InlineData("guru")]
    public void Gurmukhi_shapes_pre_base_vowel_signs_below_base_forms_and_marks(string tag)
    {
        var f = new Font("" + PaKa + PaRa + PaI + PaHalant + PaTippi + PaAddak, script: tag);
        int raBelow = f.AddGlyph();
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, tag == "gur2"
            ? Ligature(f[PaHalant], new[] { f[PaRa] }, raBelow)
            : Ligature(f[PaRa], new[] { f[PaHalant] }, raBelow)));
        f.Gpos.AddFeature("abvm", f.Gpos.AddLookup(4, 0, L.MarkAttach(new[] { (f[PaTippi], 0, (short)100, (short)0) }, new[] { (f[PaKa], new[] { ((short)300, (short)700) }) })));
        f.Gdef = L.Gdef(new Dictionary<int, int> { [f[PaTippi]] = 3, [f[PaAddak]] = 3, [f[PaI]] = 3, [f[PaHalant]] = 3 });

        Assert.Equal(new[] { f[PaI], f[PaKa], raBelow }, Glyphs(f.Shape("" + PaKa + PaHalant + PaRa + PaI)));
        var tippi = f.Shape("" + PaKa + PaTippi);
        Assert.Equal((200, 700, 0), (tippi.Glyphs[1].X, tippi.Glyphs[1].Y, tippi.Glyphs[1].Advance));
        var addak = f.Shape("" + PaKa + PaAddak + PaKa);
        Assert.Equal(new[] { f[PaKa], f[PaAddak], f[PaKa] }, Glyphs(addak));
        Assert.Equal(new[] { 0, 0, 2 }, addak.Glyphs.Select(g => g.Cluster));
    }

    [Theory]
    [InlineData("ory2")]
    [InlineData("orya")]
    public void Odia_split_vowel_signs_and_a_reph_right_after_the_base(string tag)
    {
        // Odia draws the reph right after the base, before a post-base form such as ya-phala and before the vowel signs.
        var f = new Font("" + OrKa + OrRa + OrYa + OrE + OrAa + OrAiLength + OrAuLength + OrHalant, script: tag);
        int reph = f.AddGlyph(), yaPhala = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[OrRa], new[] { f[OrHalant] }, reph)));
        f.Gsub.AddFeature("pstf", f.Gsub.AddLookup(4, 0, tag == "ory2"
            ? Ligature(f[OrHalant], new[] { f[OrYa] }, yaPhala)
            : Ligature(f[OrYa], new[] { f[OrHalant] }, yaPhala)));

        Assert.Equal(new[] { f[OrE], f[OrKa] }, Glyphs(f.Shape("" + OrKa + OrE)));
        Assert.Equal(new[] { f[OrE], f[OrKa], f[OrAiLength] }, Glyphs(f.Shape("" + OrKa + OrAi)));
        Assert.Equal(new[] { f[OrE], f[OrKa], f[OrAa] }, Glyphs(f.Shape("" + OrKa + OrO)));
        Assert.Equal(new[] { f[OrE], f[OrKa], f[OrAuLength] }, Glyphs(f.Shape("" + OrKa + OrAu)));
        Assert.Equal(new[] { f[OrKa], reph, yaPhala }, Glyphs(f.Shape("" + OrRa + OrHalant + OrKa + OrHalant + OrYa)));
        Assert.Equal(new[] { f[OrKa], reph, f[OrAa] }, Glyphs(f.Shape("" + OrRa + OrHalant + OrKa + OrAa)));
    }

    [Fact]
    public void Kannada_split_vowel_signs_take_two_or_three_parts()
    {
        var f = new Font("" + KnKa + KnE + KnUu + KnLength + KnAiLength + KnI, script: "knd2");

        Assert.Equal(new[] { f[KnKa], f[KnE], f[KnUu], f[KnLength] }, Glyphs(f.Shape("" + KnKa + KnOo)));
        Assert.Equal(new[] { f[KnKa], f[KnI], f[KnLength] }, Glyphs(f.Shape("" + KnKa + KnIi)));
        Assert.Equal(new[] { f[KnKa], f[KnE], f[KnAiLength] }, Glyphs(f.Shape("" + KnKa + KnAi)));
    }

    [Theory]
    [InlineData("knd2")]
    [InlineData("knda")]
    public void Kannada_draws_the_reph_last_and_vowel_signs_before_below_base_forms(string tag)
    {
        var f = new Font("" + KnKa + KnRa + KnTta + KnU + KnAa + KnHalant + KnAnusvara, script: tag);
        int reph = f.AddGlyph(), ttaBelow = f.AddGlyph();
        f.Gsub.AddFeature("rphf", f.Gsub.AddLookup(4, 0, Ligature(f[KnRa], new[] { f[KnHalant] }, reph)));
        f.Gsub.AddFeature("blwf", f.Gsub.AddLookup(4, 0, tag == "knd2"
            ? Ligature(f[KnHalant], new[] { f[KnTta] }, ttaBelow)
            : Ligature(f[KnTta], new[] { f[KnHalant] }, ttaBelow)));

        Assert.Equal(new[] { f[KnKa], f[KnAa], reph }, Glyphs(f.Shape("" + KnRa + KnHalant + KnKa + KnAa)));
        Assert.Equal(new[] { f[KnKa], f[KnAa], reph, f[KnAnusvara] }, Glyphs(f.Shape("" + KnRa + KnHalant + KnKa + KnAa + KnAnusvara)));
        Assert.Equal(new[] { f[KnTta], f[KnU], ttaBelow }, Glyphs(f.Shape("" + KnTta + KnHalant + KnTta + KnU)));
    }

    [Theory]
    [InlineData("mlm2")]
    [InlineData("mlym")]
    public void The_malayalam_pre_base_ra_form_moves_before_its_base(string tag)
    {
        var f = new Font("" + MlKa + MlNa + MlTa + MlRa + MlE + MlHalant, script: tag);
        int raPre = f.AddGlyph(), nata = f.AddGlyph();
        f.Gsub.AddFeature("pref", f.Gsub.AddLookup(4, 0, tag == "mlm2"
            ? Ligature(f[MlHalant], new[] { f[MlRa] }, raPre)
            : Ligature(f[MlRa], new[] { f[MlHalant] }, raPre)));
        f.Gsub.AddFeature("cjct", f.Gsub.AddLookup(4, 0, Ligature(f[MlNa], new[] { f[MlHalant], f[MlTa] }, nata)));

        Assert.Equal(new[] { raPre, f[MlKa] }, Glyphs(f.Shape("" + MlKa + MlHalant + MlRa)));
        Assert.Equal(new[] { f[MlE], raPre, f[MlKa] }, Glyphs(f.Shape("" + MlKa + MlHalant + MlRa + MlE)));
        Assert.Equal(new[] { raPre, nata }, Glyphs(f.Shape("" + MlNa + MlHalant + MlTa + MlHalant + MlRa)));
    }

    [Fact]
    public void Without_a_pref_form_the_malayalam_ra_is_not_moved()
    {
        var f = new Font("" + MlKa + MlRa + MlHalant, script: "mlm2");

        Assert.Equal(new[] { f[MlKa], f[MlHalant], f[MlRa] }, Glyphs(f.Shape("" + MlKa + MlHalant + MlRa)));
    }

    [Fact]
    public void Malayalam_chillus_dot_reph_and_split_vowel_signs()
    {
        var f = new Font("" + MlKa + MlLa + MlChilluL + MlE + MlAa + MlHalant + MlDotReph + Zwj, script: "mlm2");
        int chillu = f[MlChilluL];
        f.Gsub.AddFeature("half", f.Gsub.AddLookup(4, 0, Ligature(f[MlLa], new[] { f[MlHalant] }, chillu)));

        Assert.Equal(new[] { chillu }, Glyphs(f.Shape("" + MlLa + MlHalant + Zwj)));
        Assert.Equal(new[] { chillu }, Glyphs(f.Shape("" + MlChilluL)));
        Assert.Equal(new[] { f[MlKa], f[MlDotReph] }, Glyphs(f.Shape("" + MlDotReph + MlKa)));
        Assert.Equal(new[] { f[MlKa], f[MlDotReph], f[MlAa] }, Glyphs(f.Shape("" + MlDotReph + MlKa + MlAa)));
        Assert.Equal(new[] { f[MlE], f[MlKa], f[MlAa] }, Glyphs(f.Shape("" + MlKa + MlO)));
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

    [Theory]
    [InlineData('ক', 'ি')] // Bengali KA + I
    [InlineData('ક', 'િ')] // Gujarati KA + I
    [InlineData('ਕ', 'ਿ')] // Gurmukhi KA + I
    [InlineData('\u0B15', '\u0B47')] // Oriya KA + E
    [InlineData('ക', 'െ')] // Malayalam KA + E
    public void The_pipeline_shapes_each_added_script_drawn_with_a_caller_font(char consonant, char preBaseVowelSign)
    {
        var builder = new TestFontBuilder("Test Indic");
        builder.MapCharacters("" + consonant + preBaseVowelSign);
        var font = PdfFont.FromBytes(builder.Build());
        var document = new PdfDocument();
        document.AddPage().AddText("" + consonant + preBaseVowelSign, font, 72, 700);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        int c = font.Font.CharacterMap.GlyphFor(consonant), v = font.Font.CharacterMap.GlyphFor(preBaseVowelSign);
        Assert.Contains($"[<{v:X4}> <{c:X4}>] TJ", Assert.Single(PdfPageReader.Read(pdf)).Content);
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
            // The font is built once, so threads that shape at the same time share one PdfFont.
            PdfFont font;
            lock (_builder)
            {
                if (_font is null)
                {
                    _builder.AddTable("GSUB", Gsub.Build(_script));
                    _builder.AddTable("GPOS", Gpos.Build(_script));
                    if (Gdef is not null) _builder.AddTable("GDEF", Gdef);
                    _font = PdfFont.FromBytes(_builder.Build());
                }
                font = _font;
            }
            string script = IndicScript.All.First(s => s.Version2Tag == _script || s.Version1Tag == _script).Tag;
            return IndicShaper.Shape(font, text, script);
        }
    }

    #endregion
}
