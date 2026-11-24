using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Text.Layout;

namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// One glyph of shaped text: its ID, the text cluster it stands for (index of the cluster's first character), its
/// position relative to the run's start, and its advance, all in font units.
/// </summary>
internal sealed record ShapedGlyph(int Glyph, int Cluster, int X, int Y, int Advance);

/// <summary>
/// Shaped text in one font: positioned glyphs and the total advance, in font units.
/// </summary>
internal sealed record ShapedRun(IReadOnlyList<ShapedGlyph> Glyphs, int Advance);

/// <summary>
/// A font's layout tables, read once per font.
/// </summary>
internal sealed class FontLayout
{
    #region Constructors and Finalizers

    public FontLayout(OpenTypeFont font)
    {
        Gsub = LayoutTable.Read(font, "GSUB");
        Gpos = LayoutTable.Read(font, "GPOS");
        Gdef = GlyphDefinitions.Read(font);
    }

    #endregion

    #region Properties, Indexers

    public GlyphDefinitions? Gdef { get; }

    public LayoutTable? Gpos { get; }

    public LayoutTable? Gsub { get; }

    #endregion
}

/// <summary>
/// Shapes text with a font's own OpenType rules (D-003): characters to glyphs through the character map, then GSUB
/// and GPOS lookups in feature stages, then positions. The default shaper used for Latin and other simple scripts
/// applies <c>ccmp</c> and <c>locl</c>, then <c>rlig</c>, <c>liga</c>, <c>clig</c>, and <c>calt</c>, then
/// <c>kern</c>, <c>mark</c>, and <c>mkmk</c>. Script shapers reuse <see cref="Substitute"/> and <see cref="Finish"/>
/// with their own stages and per-glyph feature masks.
/// </summary>
internal static class TextShaper
{
    #region Fields and Constants

    public static readonly IReadOnlyList<string[]> DefaultSubstitutionStages = new[]
    {
        new[] { "ccmp", "locl" },
        new[] { "rlig", "liga", "clig", "calt" }
    };

    public static readonly string[] DefaultPositioningFeatures = { "kern", "mark", "mkmk" };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Shapes <paramref name="text"/> with the default shaper.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="text">The text; every character should have a glyph in the font (fallback happens before).</param>
    /// <param name="scriptTags">OpenType script tags in order of preference.</param>
    /// <returns>The shaped run.</returns>
    public static ShapedRun Shape(PdfFont font, string text, IReadOnlyList<string> scriptTags)
    {
        var buffer = Map(font, text);
        Substitute(font, buffer, scriptTags, DefaultSubstitutionStages);
        return Finish(font, buffer, scriptTags, DefaultPositioningFeatures);
    }

    /// <summary>
    /// Maps text to glyphs: one glyph per code point, with its cluster, code point, and glyph class (from GDEF, else
    /// marks by Unicode category).
    /// </summary>
    public static List<ShapingGlyph> Map(PdfFont font, string text)
    {
        var buffer = new List<ShapingGlyph>(text.Length);
        var gdef = font.Layout.Gdef;
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            int codePoint = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
            var glyph = new ShapingGlyph(font.Font.CharacterMap.GlyphFor(codePoint), i) { CodePoint = codePoint };
            glyph.GlyphClass = gdef is { HasGlyphClasses: true }
                ? Math.Max(1, gdef.GlyphClass(glyph.Glyph))
                : CharUnicodeInfo.GetUnicodeCategory(codePoint) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark ? 3 : 1;
            buffer.Add(glyph);
        }
        return buffer;
    }

    /// <summary>
    /// Applies GSUB features stage by stage. Within a stage, the lookups of all its features run in lookup-list order;
    /// a lookup acts only on glyphs whose mask has the bit of a feature that uses it (bit <c>i</c> for the stage's
    /// feature <c>i</c>, counted across all stages).
    /// </summary>
    public static void Substitute(PdfFont font, List<ShapingGlyph> buffer, IReadOnlyList<string> scriptTags, IReadOnlyList<string[]> stages)
    {
        if (font.Layout.Gsub is not { } gsub) return;
        RunStages(font, gsub, buffer, scriptTags, stages);
    }

    /// <summary>
    /// Sets advances from the font instance, applies GPOS features, and resolves positions: attached marks sit at
    /// their anchor and take no advance; glyphs chained by cursive attachment take their parent's vertical offset. A
    /// right-to-left run is positioned in logical order and returned in visual order, left to right (WR-041).
    /// </summary>
    public static ShapedRun Finish(PdfFont font, List<ShapingGlyph> buffer, IReadOnlyList<string> scriptTags, IReadOnlyList<string> positioningFeatures, bool rightToLeft = false)
    {
        foreach (var glyph in buffer) glyph.XAdvance = font.Instance.Advance(glyph.Glyph);
        if (font.Layout.Gpos is { } gpos) RunStages(font, gpos, buffer, scriptTags, new[] { positioningFeatures.ToArray() }, rightToLeft);

        int count = buffer.Count;
        var x = new int[count];
        var y = new int[count];
        var advance = new int[count];
        var chainY = ChainOffsets(buffer);
        bool Attached(int i) => buffer[i].AttachTo >= 0 && buffer[i].AttachTo < i;

        // Glyphs that are not attached marks take the pen in drawing order.
        int pen = 0;
        for (int k = 0; k < count; k++)
        {
            int i = rightToLeft ? count - 1 - k : k;
            if (Attached(i)) continue;
            var glyph = buffer[i];
            x[i] = pen + glyph.XOffset;
            y[i] = chainY[i];
            advance[i] = glyph.XAdvance;
            pen += glyph.XAdvance;
        }
        // Attached marks sit at their target's anchor; targets come earlier in logical order, so they are placed first.
        for (int i = 0; i < count; i++)
        {
            if (!Attached(i)) continue;
            var glyph = buffer[i];
            int target = glyph.AttachTo;
            x[i] = x[target] + glyph.AttachDx + glyph.XOffset;
            y[i] = y[target] + glyph.AttachDy + glyph.YOffset;
        }

        var shaped = new ShapedGlyph[count];
        for (int k = 0; k < count; k++)
        {
            int i = rightToLeft ? count - 1 - k : k;
            shaped[k] = new ShapedGlyph(buffer[i].Glyph, buffer[i].Cluster, x[i], y[i], advance[i]);
        }
        return new ShapedRun(shaped, pen);
    }

    #endregion

    #region Private Methods

    // The vertical offset of each glyph: its own, or for a glyph chained by cursive attachment its chain offset plus
    // its parent's (parents resolved first; a chain longer than the run is cut, so a malformed font cannot loop).
    private static int[] ChainOffsets(List<ShapingGlyph> buffer)
    {
        int count = buffer.Count;
        var result = new int[count];
        var state = new byte[count]; // 0 unresolved, 1 resolving, 2 resolved
        for (int i = 0; i < count; i++) Resolve(i);
        return result;

        void Resolve(int start)
        {
            var path = new List<int>();
            int at = start;
            while (at >= 0 && at < count && state[at] == 0 && path.Count <= count)
            {
                state[at] = 1;
                path.Add(at);
                at = buffer[at].CursiveTo;
            }
            int above = at >= 0 && at < count && state[at] == 2 ? result[at] : 0;
            for (int k = path.Count - 1; k >= 0; k--)
            {
                var glyph = buffer[path[k]];
                bool chained = glyph.CursiveTo >= 0 && glyph.CursiveTo < count && (k < path.Count - 1 || state[glyph.CursiveTo] == 2);
                result[path[k]] = chained ? glyph.CursiveDy + above : glyph.YOffset;
                above = result[path[k]];
                state[path[k]] = 2;
            }
        }
    }

    private static void RunStages(PdfFont font, LayoutTable table, List<ShapingGlyph> buffer, IReadOnlyList<string> scriptTags, IReadOnlyList<string[]> stages, bool rightToLeft = false)
    {
        var allFeatures = stages.SelectMany(s => s).Distinct(StringComparer.Ordinal).ToList();
        var lookups = table.FeatureLookups(scriptTags, allFeatures, font.Instance.Coordinates, out _);
        var engine = new LayoutEngine(table, font.Layout.Gdef, buffer, font.Font.GlyphCount, rightToLeft);
        int bit = 0;
        foreach (var stage in stages)
        {
            var stageLookups = new SortedDictionary<int, ulong>();
            foreach (var feature in stage)
            {
                ulong mask = FeatureBit(bit++);
                if (!lookups.TryGetValue(feature, out var list)) continue;
                foreach (int lookup in list) stageLookups[lookup] = stageLookups.GetValueOrDefault(lookup) | mask;
            }
            foreach (var (lookup, mask) in stageLookups) engine.Apply(lookup, mask);
        }
    }

    /// <summary>
    /// Returns the mask bit of the <paramref name="index"/>-th feature of a shaping plan.
    /// </summary>
    public static ulong FeatureBit(int index) => index < 64 ? 1UL << index : 0;

    #endregion

    #endregion
}
