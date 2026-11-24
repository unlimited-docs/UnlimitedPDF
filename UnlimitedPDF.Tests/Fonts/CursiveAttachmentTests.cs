using UnlimitedPDF.Models.Internal.Text;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Cursive attachment (GPOS type 3; WR-039, V-05) with generated fonts: the previous glyph's exit anchor meets the next
/// glyph's entry anchor in left-to-right and right-to-left runs, with and without the lookup's right-to-left flag;
/// marks skipped by the lookup flags; a right-to-left run comes back in visual order; malformed chains never loop.
/// </summary>
public class CursiveAttachmentTests
{
    #region Fields and Constants

    // Glyphs: 0 .notdef, a = 1, b = 2, c = 3, m = 4 (a mark), each 600 units wide.
    private const string Letters = "abcm";
    private static readonly string[] Scripts = { "DFLT" };

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void In_a_left_to_right_run_the_next_glyph_hangs_from_the_previous_exit()
    {
        var font = Font(0, (G('a'), null, (500, 100)), (G('b'), (50, 300), null));

        var run = Shape(font, "ab", rightToLeft: false);

        Assert.Equal(new[] { G('a'), G('b') }, run.Glyphs.Select(g => g.Glyph));
        Assert.Equal(new[] { 0, 450 }, run.Glyphs.Select(g => g.X));
        Assert.Equal(new[] { 0, -200 }, run.Glyphs.Select(g => g.Y));
        // The anchors meet: a's exit (0 + 500, 0 + 100) is b's entry (450 + 50, -200 + 300).
        Assert.Equal(500 + 550, run.Advance);
    }

    [Fact]
    public void In_a_right_to_left_run_with_the_flag_the_earlier_glyph_is_chained_to_the_later_one()
    {
        // Right to left: a is read first and drawn on the right; its exit is on its left side, b's entry on its right.
        var font = Font(0x0001, (G('a'), null, (100, 100)), (G('b'), (600, 300), null));

        var run = Shape(font, "ab", rightToLeft: true);

        Assert.Equal(new[] { G('b'), G('a') }, run.Glyphs.Select(g => g.Glyph)); // visual order
        Assert.Equal(new[] { 1, 0 }, run.Glyphs.Select(g => g.Cluster));
        Assert.Equal(new[] { 0, 500 }, run.Glyphs.Select(g => g.X));
        // b's entry (0 + 600, 0 + 300) is a's exit (500 + 100, 200 + 100).
        Assert.Equal(new[] { 0, 200 }, run.Glyphs.Select(g => g.Y));
    }

    [Fact]
    public void In_a_right_to_left_run_without_the_flag_the_later_glyph_is_chained()
    {
        var font = Font(0, (G('a'), null, (100, 100)), (G('b'), (600, 300), null));

        var run = Shape(font, "ab", rightToLeft: true);

        Assert.Equal(new[] { 0, 500 }, run.Glyphs.Select(g => g.X));
        Assert.Equal(new[] { -200, 0 }, run.Glyphs.Select(g => g.Y)); // b (drawn first) moves down to a's exit
    }

    [Fact]
    public void A_chain_of_three_glyphs_accumulates_its_offsets()
    {
        var font = Font(0, (G('a'), null, (600, 100)), (G('b'), (0, 0), (600, 100)), (G('c'), (0, 0), null));

        var run = Shape(font, "abc", rightToLeft: false);

        Assert.Equal(new[] { 0, 600, 1200 }, run.Glyphs.Select(g => g.X));
        Assert.Equal(new[] { 0, 100, 200 }, run.Glyphs.Select(g => g.Y));
    }

    [Fact]
    public void A_lookup_that_ignores_marks_attaches_across_a_mark()
    {
        var font = Font(0x0008, (G('a'), null, (500, 100)), (G('b'), (50, 300), null));

        var run = Shape(font, "amb", rightToLeft: false);

        Assert.Equal(-200, run.Glyphs[2].Y);
    }

    [Fact]
    public void Glyphs_without_both_anchors_are_not_attached()
    {
        var font = Font(0, (G('a'), (0, 0), null), (G('b'), null, (600, 0)));

        var run = Shape(font, "ab", rightToLeft: false);

        Assert.Equal(new[] { 0, 600 }, run.Glyphs.Select(g => g.X));
        Assert.Equal(new[] { 0, 0 }, run.Glyphs.Select(g => g.Y));
    }

    [Fact]
    public void Every_glyph_with_both_anchors_in_a_long_run_never_loops()
    {
        var font = Font(0x0001, (G('a'), (600, 50), (0, 0)), (G('b'), (600, 50), (0, 0)));
        string text = string.Concat(Enumerable.Repeat("ab", 5_000));

        var run = Shape(font, text, rightToLeft: true);

        Assert.Equal(10_000, run.Glyphs.Count);
    }

    #endregion

    #region Private Methods

    private static int G(char c) => Letters.IndexOf(c) + 1;

    private static PdfFont Font(int flags, params (int Glyph, (short X, short Y)? Entry, (short X, short Y)? Exit)[] records)
    {
        var builder = new TestFontBuilder();
        builder.MapCharacters(Letters);
        var gpos = new L();
        gpos.AddFeature("curs", gpos.AddLookup(3, flags, L.Cursive(records)));
        builder.AddTable("GPOS", gpos.Build());
        builder.AddTable("GDEF", L.Gdef(new Dictionary<int, int> { [G('a')] = 1, [G('b')] = 1, [G('c')] = 1, [G('m')] = 3 }));
        return PdfFont.FromBytes(builder.Build());
    }

    private static ShapedRun Shape(PdfFont font, string text, bool rightToLeft)
    {
        var buffer = TextShaper.Map(font, text);
        return TextShaper.Finish(font, buffer, Scripts, new[] { "curs" }, rightToLeft);
    }

    #endregion

    #endregion
}
