using System.Buffers.Binary;
using UnlimitedPDF.Models.Internal.Text;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The OpenType shaping engine (D-003, D-008, A06): each GSUB and GPOS lookup type and format with generated fonts,
/// lookup flags and mark filtering sets, FeatureVariations, the default shaper's feature stages, and the nesting,
/// work, and growth limits.
/// </summary>
[Collection(nameof(FontFuzzCollection))]
public class ShapingEngineTests
{
    #region Fields and Constants

    // Glyphs: 0 .notdef, then one per character of Letters in order (a = 1, b = 2, ...).
    private const string Letters = "abcdefghijklmnopqrstuvwxyz";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Without_layout_tables_each_character_gets_its_glyph_and_advance()
    {
        var run = Shape(Font(), "abc");

        Assert.Equal(new[] { G('a'), G('b'), G('c') }, Glyphs(run));
        Assert.Equal(new[] { 0, 600, 1200 }, run.Glyphs.Select(g => g.X));
        Assert.Equal(new[] { 0, 1, 2 }, run.Glyphs.Select(g => g.Cluster));
        Assert.Equal(1800, run.Advance);
    }

    [Fact]
    public void Single_substitution_by_delta_and_by_list()
    {
        var gsub = new L();
        gsub.AddFeature("ccmp", gsub.AddLookup(1, 0, L.SingleSubstDelta(new[] { G('a') }, 1)));
        gsub.AddFeature("liga", gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('c')] = G('z') })));

        Assert.Equal(new[] { G('b'), G('b'), G('z') }, Glyphs(Shape(Font(gsub: gsub), "abc")));
    }

    [Fact]
    public void Multiple_substitution_splits_a_glyph_and_keeps_its_cluster()
    {
        var gsub = new L();
        gsub.AddFeature("ccmp", gsub.AddLookup(2, 0, L.MultipleSubst(new Dictionary<int, int[]> { [G('b')] = new[] { G('x'), G('y') } })));

        var run = Shape(Font(gsub: gsub), "abc");

        Assert.Equal(new[] { G('a'), G('x'), G('y'), G('c') }, Glyphs(run));
        Assert.Equal(new[] { 0, 1, 1, 2 }, run.Glyphs.Select(g => g.Cluster));
    }

    [Fact]
    public void Ligatures_join_components_and_merge_clusters()
    {
        var builder = Builder();
        int fi = builder.AddGlyph(900, TestFontBuilder.Box(0, 0, 800, 700));
        var gsub = new L();
        gsub.AddFeature("liga", gsub.AddLookup(4, 0, L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [G('f')] = new[] { (new[] { G('i') }, fi) } })));

        var run = Shape(Font(builder, gsub: gsub), "afib");

        Assert.Equal(new[] { G('a'), fi, G('b') }, Glyphs(run));
        Assert.Equal(new[] { 0, 1, 3 }, run.Glyphs.Select(g => g.Cluster));
        Assert.Equal(600 + 900 + 600, run.Advance);
    }

    [Fact]
    public void A_ligature_lookup_that_ignores_marks_joins_across_a_mark()
    {
        var builder = Builder();
        int fi = builder.AddGlyph(900, TestFontBuilder.Box(0, 0, 800, 700));
        var gsub = new L();
        gsub.AddFeature("liga", gsub.AddLookup(4, 0x8, L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [G('f')] = new[] { (new[] { G('i') }, fi) } })));
        var gdef = L.Gdef(new Dictionary<int, int> { [G('m')] = 3 });

        var run = Shape(Font(builder, gsub: gsub, gdef: gdef), "fmi");

        Assert.Equal(new[] { fi, G('m') }, Glyphs(run));
        Assert.Equal(new[] { 0, 0 }, run.Glyphs.Select(g => g.Cluster));
    }

    [Fact]
    public void Contextual_substitution_formats_1_and_3_apply_nested_lookups()
    {
        var gsub = new L();
        int toZ = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('b')] = G('z') }));
        gsub.AddFeature("calt", gsub.AddLookup(5, 0, L.Context3(new[] { new[] { G('a') }, new[] { G('b') } }, (1, toZ))));
        int toY = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('d')] = G('y') }));
        gsub.AddFeature("clig", gsub.AddLookup(5, 0, L.Context1(G('c'), new[] { G('d') }, (1, toY))));

        Assert.Equal(new[] { G('a'), G('z'), G('b'), G('c'), G('y') }, Glyphs(Shape(Font(gsub: gsub), "abbcd")));
    }

    [Fact]
    public void Chained_contextual_substitution_format_3_checks_backtrack_and_lookahead()
    {
        var gsub = new L();
        int toZ = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('b')] = G('z') }));
        gsub.AddFeature("calt", gsub.AddLookup(6, 0, L.Chain3(new[] { new[] { G('a') } }, new[] { new[] { G('b') } }, new[] { new[] { G('c') } }, (0, toZ))));

        Assert.Equal(new[] { G('a'), G('z'), G('c'), G('b'), G('c'), G('a'), G('b') }, Glyphs(Shape(Font(gsub: gsub), "abcbcab")));
    }

    [Fact]
    public void Chained_contextual_substitution_format_2_matches_classes()
    {
        var gsub = new L();
        int toZ = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('b')] = G('z') }));
        // Backtrack class 1 = {a, e}; input class 2 = {b}; lookahead class 3 = {c, d}.
        var back = new Dictionary<int, int> { [G('a')] = 1, [G('e')] = 1 };
        var input = new Dictionary<int, int> { [G('b')] = 2 };
        var ahead = new Dictionary<int, int> { [G('c')] = 3, [G('d')] = 3 };
        gsub.AddFeature("calt", gsub.AddLookup(6, 0, L.Chain2(new[] { G('b') }, back, input, ahead, 2, new[] { 1 }, Array.Empty<int>(), new[] { 3 }, (0, toZ))));

        Assert.Equal(new[] { G('e'), G('z'), G('d'), G('f'), G('b'), G('c') }, Glyphs(Shape(Font(gsub: gsub), "ebdfbc")));
    }

    [Fact]
    public void Extension_lookups_are_resolved()
    {
        var gsub = new L();
        gsub.AddFeature("liga", gsub.AddLookup(7, 0, L.Extension(1, L.SingleSubstDelta(new[] { G('a') }, 2))));

        Assert.Equal(new[] { G('c') }, Glyphs(Shape(Font(gsub: gsub), "a")));
    }

    [Fact]
    public void Reverse_chaining_substitution_works_from_the_end()
    {
        var gsub = new L();
        // Each b followed by c becomes c: applied right to left, "bbc" becomes "ccc".
        gsub.AddFeature("calt", gsub.AddLookup(8, 0, L.ReverseChain(new[] { G('b') }, Array.Empty<int[]>(), new[] { new[] { G('c') } }, new[] { G('c') })));

        Assert.Equal(new[] { G('c'), G('c'), G('c') }, Glyphs(Shape(Font(gsub: gsub), "bbc")));
    }

    [Fact]
    public void Single_and_pair_positioning_adjust_placement_and_advance()
    {
        var gpos = new L();
        gpos.AddFeature("kern", gpos.AddLookup(1, 0, L.SinglePos(new[] { G('a') }, 10, 20, 50)), gpos.AddLookup(2, 0, L.PairPos1(new Dictionary<(int, int), short> { [(G('b'), G('c'))] = -80 })));

        var run = Shape(Font(gpos: gpos), "abc");

        Assert.Equal((10, 20), (run.Glyphs[0].X, run.Glyphs[0].Y));
        Assert.Equal(650, run.Glyphs[1].X);
        Assert.Equal(650 + 520, run.Glyphs[2].X);
        Assert.Equal(650 + 520 + 600, run.Advance);
    }

    [Fact]
    public void Class_pair_positioning_and_extension_positioning()
    {
        var gpos = new L();
        var first = new Dictionary<int, int> { [G('a')] = 1, [G('b')] = 1 };
        var second = new Dictionary<int, int> { [G('c')] = 1 };
        var values = new short[2, 2] { { 0, 0 }, { 0, -100 } };
        gpos.AddFeature("kern", gpos.AddLookup(9, 0, L.Extension(2, L.PairPos2(first, second, values))));

        var run = Shape(Font(gpos: gpos), "bca");

        Assert.Equal(new[] { 0, 500, 1100 }, run.Glyphs.Select(g => g.X));
    }

    [Fact]
    public void Marks_attach_to_bases_and_to_other_marks_at_their_anchors()
    {
        var gpos = new L();
        gpos.AddFeature("mark", gpos.AddLookup(4, 0, L.MarkAttach(new[] { (G('m'), 0, (short)100, (short)0) }, new[] { (G('b'), new[] { ((short)300, (short)700) }) })));
        gpos.AddFeature("mkmk", gpos.AddLookup(6, 0, L.MarkAttach(new[] { (G('n'), 0, (short)50, (short)0) }, new[] { (G('m'), new[] { ((short)100, (short)200) }) })));
        var gdef = L.Gdef(new Dictionary<int, int> { [G('m')] = 3, [G('n')] = 3 });

        var run = Shape(Font(gpos: gpos, gdef: gdef), "abmnc");

        Assert.Equal((600 + 200, 700), (run.Glyphs[2].X, run.Glyphs[2].Y)); // m: base anchor (300, 700) minus mark anchor (100, 0)
        Assert.Equal((800 + 50, 900), (run.Glyphs[3].X, run.Glyphs[3].Y)); // n on m: (100, 200) minus (50, 0)
        Assert.Equal(new[] { 600, 600, 0, 0, 600 }, run.Glyphs.Select(g => g.Advance));
        Assert.Equal(1800, run.Advance);
    }

    [Fact]
    public void A_mark_after_a_ligature_component_attaches_to_that_component()
    {
        var builder = Builder();
        int fi = builder.AddGlyph(1000, TestFontBuilder.Box(0, 0, 900, 700));
        var gsub = new L();
        gsub.AddFeature("liga", gsub.AddLookup(4, 0x8, L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [G('f')] = new[] { (new[] { G('i') }, fi) } })));
        var gpos = new L();
        gpos.AddFeature("mark", gpos.AddLookup(5, 0, L.MarkToLigature(G('m'), 0, 0, fi, new[] { ((short)200, (short)700), ((short)700, (short)700) })));
        var gdef = L.Gdef(new Dictionary<int, int> { [G('m')] = 3, [fi] = 2 });

        var first = Shape(Font(builder, gsub: gsub, gpos: gpos, gdef: gdef), "fmi");
        var last = Shape(Font(builder, gsub: gsub, gpos: gpos, gdef: gdef), "fim");

        Assert.Equal(200, first.Glyphs[1].X); // the mark between f and i sits on the first component
        Assert.Equal(700, last.Glyphs[1].X); // a mark after the ligature sits on the last component
    }

    [Fact]
    public void A_mark_filtering_set_limits_which_marks_a_lookup_sees()
    {
        var builder = Builder();
        int fi = builder.AddGlyph(900, TestFontBuilder.Box(0, 0, 800, 700));
        var gsub = new L();
        // The ligature lookup skips only marks outside set 0 ({n}); m is in no set, so f m i still joins, but f n i
        // does not (n is visible and is not i).
        gsub.AddFeature("liga", gsub.AddLookup(4, 0, 0, L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [G('f')] = new[] { (new[] { G('i') }, fi) } })));
        var gdef = L.Gdef(new Dictionary<int, int> { [G('m')] = 3, [G('n')] = 3 }, markSets: new[] { new[] { G('n') } });

        Assert.Equal(new[] { fi, G('m') }, Glyphs(Shape(Font(builder, gsub: gsub, gdef: gdef), "fmi")));
        Assert.Equal(new[] { G('f'), G('n'), G('i') }, Glyphs(Shape(Font(builder, gsub: gsub, gdef: gdef), "fni")));
    }

    [Fact]
    public void Feature_variations_swap_lookups_for_heavier_instances()
    {
        var builder = Builder();
        builder.WeightAxis = (100, 400, 900);
        var gsub = new L();
        int regular = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('a')] = G('b') }));
        int heavy = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('a')] = G('z') }));
        int feature = gsub.AddFeature("liga", regular);
        gsub.AddFeatureVariation(0.5, 1.0, feature, heavy);
        builder.AddTable("GSUB", gsub.Build());
        var bytes = builder.Build();

        Assert.Equal(new[] { G('b') }, Glyphs(TextShaper.Shape(PdfFont.FromBytes(bytes), "a", Scripts)));
        Assert.Equal(new[] { G('z') }, Glyphs(TextShaper.Shape(PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 900 }), "a", Scripts)));
    }

    [Fact]
    public void The_script_is_chosen_from_the_preferred_tags()
    {
        var builder = Builder();
        var gsub = new L();
        gsub.AddFeature("liga", gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('a')] = G('z') })));
        builder.AddTable("GSUB", gsub.Build("latn"));
        var font = PdfFont.FromBytes(builder.Build());

        Assert.Equal(new[] { G('z') }, Glyphs(TextShaper.Shape(font, "a", new[] { "latn", "DFLT" })));
        Assert.Equal(new[] { G('a') }, Glyphs(TextShaper.Shape(font, "a", new[] { "dev2", "DFLT" })));
    }

    [Fact]
    public void A_lookup_that_calls_itself_stops_at_the_nesting_limit()
    {
        var gsub = new L();
        // Lookup 0: context on 'a' whose nested lookup is lookup 0 itself.
        gsub.AddFeature("calt", gsub.AddLookup(5, 0, L.Context3(new[] { new[] { G('a') } }, (0, 0))));

        var error = Assert.Throws<PdfFontException>(() => Shape(Font(gsub: gsub), "a"));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("deeper than 16", error.Message);
    }

    [Fact]
    public void Lookups_that_grow_the_text_without_end_are_stopped()
    {
        var gsub = new L();
        var lookups = Enumerable.Range(0, 12).Select(_ => gsub.AddLookup(2, 0, L.MultipleSubst(new Dictionary<int, int[]> { [G('a')] = new[] { G('a'), G('a') } }))).ToArray();
        gsub.AddFeature("ccmp", lookups);

        var error = Assert.Throws<PdfFontException>(() => Shape(Font(gsub: gsub), "aaaa"));

        Assert.Contains("grow its text too far", error.Message);
    }

    [Fact]
    public void Lookups_that_do_too_much_work_are_stopped()
    {
        var gsub = new L();
        // A context whose nested lookups apply a 16-record chain at every glyph, repeated by 300 lookups.
        int inner = gsub.AddLookup(1, 0, L.SingleSubstDelta(new[] { G('a') }, 0));
        int context = gsub.AddLookup(5, 0, L.Context3(new[] { new[] { G('a') } }, Enumerable.Repeat((0, inner), 60).ToArray()));
        gsub.AddFeature("calt", Enumerable.Repeat(context, 1).Concat(Enumerable.Range(0, 300).Select(_ => gsub.AddLookup(5, 0, L.Context3(new[] { new[] { G('a') } }, Enumerable.Repeat((0, inner), 60).ToArray())))).ToArray());

        var error = Assert.Throws<PdfFontException>(() => Shape(Font(gsub: gsub), "aaaa"));

        Assert.Contains("too much work", error.Message);
    }

    [Fact]
    public void Random_damage_to_layout_tables_never_escapes_as_another_exception()
    {
        var builder = Builder();
        int fi = builder.AddGlyph(900, TestFontBuilder.Box(0, 0, 800, 700));
        var gsub = new L();
        int toZ = gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [G('b')] = G('z') }));
        gsub.AddFeature("liga", gsub.AddLookup(4, 0x8, L.LigatureSubst(new Dictionary<int, (int[], int)[]> { [G('f')] = new[] { (new[] { G('i') }, fi) } })));
        gsub.AddFeature("calt", gsub.AddLookup(6, 0, L.Chain3(new[] { new[] { G('a') } }, new[] { new[] { G('b') } }, new[] { new[] { G('c') } }, (0, toZ))));
        var gpos = new L();
        gpos.AddFeature("kern", gpos.AddLookup(2, 0, L.PairPos1(new Dictionary<(int, int), short> { [(G('b'), G('c'))] = -80 })));
        gpos.AddFeature("mark", gpos.AddLookup(4, 0, L.MarkAttach(new[] { (G('m'), 0, (short)100, (short)0) }, new[] { (G('b'), new[] { ((short)300, (short)700) }) })));
        var original = Font(builder, gsub: gsub, gpos: gpos, gdef: L.Gdef(new Dictionary<int, int> { [G('m')] = 3 }));
        var tables = new[] { "GSUB", "GPOS", "GDEF" }.Select(t => Table(original, t)).ToArray();
        var random = new Random(4512);

        for (int run = 0; run < 3000; run++)
        {
            var font = (byte[])original.Clone();
            var (offset, length) = tables[random.Next(tables.Length)];
            for (int i = random.Next(1, 6); i > 0; i--) font[offset + random.Next(length)] = (byte)random.Next(256);
            try
            {
                TextShaper.Shape(PdfFont.FromBytes(font), "abcfmibmc", Scripts);
            }
            catch (PdfFontException)
            {
            }
        }
    }

    #endregion

    #region Private Methods

    private static readonly string[] Scripts = { "DFLT" };

    private static TestFontBuilder Builder()
    {
        var builder = new TestFontBuilder();
        builder.MapCharacters(Letters);
        return builder;
    }

    private static byte[] Font(TestFontBuilder? builder = null, L? gsub = null, L? gpos = null, byte[]? gdef = null)
    {
        builder ??= Builder();
        if (gsub is not null) builder.AddTable("GSUB", gsub.Build());
        if (gpos is not null) builder.AddTable("GPOS", gpos.Build());
        if (gdef is not null) builder.AddTable("GDEF", gdef);
        return builder.Build();
    }

    private static int G(char c) => Letters.IndexOf(c) + 1;

    private static int[] Glyphs(ShapedRun run) => run.Glyphs.Select(g => g.Glyph).ToArray();

    private static ShapedRun Shape(byte[] font, string text) => TextShaper.Shape(PdfFont.FromBytes(font), text, Scripts);

    private static (int Offset, int Length) Table(byte[] font, string tag)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            if (Encoding.ASCII.GetString(font, 12 + 16 * i, 4) == tag)
                return ((int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(12 + 16 * i + 8)), (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(12 + 16 * i + 12)));
        }
        throw new InvalidOperationException($"No {tag} table.");
    }

    #endregion

    #endregion
}
