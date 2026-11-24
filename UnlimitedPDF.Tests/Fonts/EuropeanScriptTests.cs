using UnlimitedPDF.Models.Internal.Text;
using L = UnlimitedPDF.Tests.Support.LayoutTableBuilder;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Greek, Cyrillic, Armenian, and Georgian (QF-26112401, F-3): each is a run of its own, shaped with its own script's
/// font features first, then the defaults, then Latin for fonts that list every feature under Latin only.
/// </summary>
public class EuropeanScriptTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("grek", "Ω")]
    [InlineData("cyrl", "Ж")]
    [InlineData("armn", "Ա")]
    [InlineData("geor", "ა")]
    public void A_feature_listed_under_the_script_applies(string script, string letter)
    {
        var (font, glyph, alternate) = FontWithAlternate(letter, script);

        var segment = Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout(letter, font, Array.Empty<PdfFont>(), 12)!.Segments));

        Assert.Equal(new[] { alternate }, segment.Run.Glyphs.Select(g => g.Glyph));
        Assert.NotEqual(glyph, alternate);
    }

    [Theory]
    [InlineData("Ω")]
    [InlineData("Ж")]
    [InlineData("Ա")]
    [InlineData("ა")]
    public void A_feature_listed_under_latin_only_still_applies(string letter)
    {
        var (font, _, alternate) = FontWithAlternate(letter, "latn");

        var segment = Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout(letter, font, Array.Empty<PdfFont>(), 12)!.Segments));

        Assert.Equal(new[] { alternate }, segment.Run.Glyphs.Select(g => g.Glyph));
    }

    [Fact]
    public void The_own_script_wins_over_latin()
    {
        var builder = new TestFontBuilder("Test Cyrillic");
        builder.MapCharacters("Ж");
        int own = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        var cyrl = new L();
        cyrl.AddFeature("ccmp", cyrl.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [1] = own })));
        builder.AddTable("GSUB", cyrl.Build("cyrl"));
        var font = PdfFont.FromBytes(builder.Build());

        Assert.Equal(new[] { "cyrl", "DFLT", "dflt", "latn" }, TextPipeline.ScriptTags("cyrl"));
        Assert.Equal(new[] { own }, Assert.IsType<FontSegment>(Assert.Single(TextPipeline.Layout("Ж", font, Array.Empty<PdfFont>(), 12)!.Segments)).Run.Glyphs.Select(g => g.Glyph));
    }

    [Fact]
    public void A_mixed_line_splits_into_a_run_per_script_and_keeps_its_text()
    {
        var builder = new TestFontBuilder("Test Mixed");
        builder.MapCharacters("Ivan Иван Γιάννης");
        var font = PdfFont.FromBytes(builder.Build());

        var layout = TextPipeline.Layout("Ivan Иван Γιάννης", font, Array.Empty<PdfFont>(), 12)!;

        Assert.Equal(new[] { "Ivan ", "Иван ", "Γιάννης" }, layout.Segments.Select(s => s.Text));
        Assert.Equal(17 * 600 * 12 / 1000.0, layout.Width, 6);
    }

    [Fact]
    public void Latin_keeps_its_script_tags()
    {
        Assert.Equal(new[] { "latn", "DFLT", "dflt" }, TextPipeline.ScriptTags("latn"));
        Assert.Equal(new[] { "latn", "DFLT", "dflt" }, TextPipeline.ScriptTags(string.Empty));
    }

    #endregion

    #region Private Methods

    // A font with the letter (glyph 1) and an alternate glyph that a ccmp single substitution, listed only under
    // the given script tag, puts in its place.
    private static (PdfFont Font, int Glyph, int Alternate) FontWithAlternate(string letter, string script)
    {
        var builder = new TestFontBuilder("Test " + script);
        builder.MapCharacters(letter);
        int alternate = builder.AddGlyph(600, TestFontBuilder.Box(40, 0, 560, 700));
        var gsub = new L();
        gsub.AddFeature("ccmp", gsub.AddLookup(1, 0, L.SingleSubst(new Dictionary<int, int> { [1] = alternate })));
        builder.AddTable("GSUB", gsub.Build(script));
        return (PdfFont.FromBytes(builder.Build()), 1, alternate);
    }

    #endregion

    #endregion
}
