using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Text.Layout;

namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// One glyph of shaped text: its ID, the text cluster it stands for (index of the cluster's first character), its
/// position relative to the run's start, and its advance, all in font units. A value, so a run's glyphs are one array.
/// </summary>
internal readonly record struct ShapedGlyph(int Glyph, int Cluster, int X, int Y, int Advance);

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

    // A right-to-left run applies rtlm (right-to-left mirrored forms) first, to the glyphs Mirror leaves it on.
    public static readonly IReadOnlyList<string[]> RightToLeftSubstitutionStages = new[]
    {
        new[] { "rtlm" },
        new[] { "ccmp", "locl" },
        new[] { "rlig", "liga", "clig", "calt" }
    };

    // Each font's lookups per table, script list, and stage list, merged in the order the stages apply them: built
    // once and shared by every document and thread that draws with the font. The keys are the shapers' fixed script
    // and stage lists, so a font holds a few plans at most.
    private static readonly ConditionalWeakTable<PdfFont, ConcurrentDictionary<StagePlanKey, (int Lookup, ulong Mask)[][]>> StagePlans = new();

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
    public static ShapedRun Shape(PdfFont font, string text, IReadOnlyList<string> scriptTags) => Shape(font, text, scriptTags, rightToLeft: false);

    /// <summary>
    /// Shapes <paramref name="text"/> with the default shaper in the given direction. In a right-to-left run a mirrored
    /// character (for example a parenthesis) takes the glyph of its mirror character when the font has it, else the
    /// font's <c>rtlm</c> feature applies to it; the run comes back in visual order (WR-041).
    /// </summary>
    public static ShapedRun Shape(PdfFont font, string text, IReadOnlyList<string> scriptTags, bool rightToLeft)
    {
        var buffer = Map(font, text);
        if (!rightToLeft)
        {
            Substitute(font, buffer, scriptTags, DefaultSubstitutionStages);
            return Finish(font, buffer, scriptTags, DefaultPositioningFeatures);
        }
        Mirror(font, buffer);
        Substitute(font, buffer, scriptTags, RightToLeftSubstitutionStages);
        return Finish(font, buffer, scriptTags, DefaultPositioningFeatures, rightToLeft: true);
    }

    /// <summary>
    /// Swaps mirrored characters of a right-to-left run for their mirror glyph, and leaves the <c>rtlm</c> feature bit
    /// (bit 0 of <see cref="RightToLeftSubstitutionStages"/>) only on mirrored characters the font has no mirror glyph
    /// for.
    /// </summary>
    public static void Mirror(PdfFont font, List<ShapingGlyph> buffer)
    {
        foreach (var glyph in buffer)
        {
            int mirror = BidiData.MirrorOf(glyph.CodePoint);
            int mirrorGlyph = mirror >= 0 ? font.Font.CharacterMap.GlyphFor(mirror) : 0;
            if (mirrorGlyph != 0) glyph.Glyph = mirrorGlyph;
            if (mirror < 0 || mirrorGlyph != 0) glyph.Mask &= ~FeatureBit(0);
        }
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
        if (font.Layout.Gpos is { } gpos) RunStages(font, gpos, buffer, scriptTags, new[] { positioningFeatures as string[] ?? positioningFeatures.ToArray() }, rightToLeft);

        int count = buffer.Count;
        // Working positions come from the shared pool; only the shaped glyphs stay allocated.
        int[] x = ArrayPool<int>.Shared.Rent(count);
        int[] y = ArrayPool<int>.Shared.Rent(count);
        int[] chainY = ArrayPool<int>.Shared.Rent(count);
        try
        {
            ChainOffsets(buffer, chainY);
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
                pen += glyph.XAdvance;
            }
            // Attached marks sit at their target's anchor; targets come earlier in logical order, so they are placed
            // first.
            for (int i = 0; i < count; i++)
            {
                if (!Attached(i)) continue;
                var glyph = buffer[i];
                int target = glyph.AttachTo;
                x[i] = x[target] + glyph.AttachDx + glyph.XOffset;
                y[i] = y[target] + glyph.AttachDy + glyph.YOffset;
            }

            // An attached mark takes no advance.
            var shaped = new ShapedGlyph[count];
            for (int k = 0; k < count; k++)
            {
                int i = rightToLeft ? count - 1 - k : k;
                var glyph = buffer[i];
                shaped[k] = new ShapedGlyph(glyph.Glyph, glyph.Cluster, x[i], y[i], Attached(i) ? 0 : glyph.XAdvance);
            }
            return new ShapedRun(shaped, pen);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(x);
            ArrayPool<int>.Shared.Return(y);
            ArrayPool<int>.Shared.Return(chainY);
        }
    }

    #endregion

    #region Private Methods

    // The vertical offset of each glyph, into result: its own, or for a glyph chained by cursive attachment its chain
    // offset plus its parent's (parents resolved first; a chain longer than the run is cut, so a malformed font cannot
    // loop). A glyph in no chain, as in most text, is resolved without the path walk.
    private static void ChainOffsets(List<ShapingGlyph> buffer, int[] result)
    {
        int count = buffer.Count;
        byte[] state = ArrayPool<byte>.Shared.Rent(count); // 0 unresolved, 1 resolving, 2 resolved
        Array.Clear(state, 0, count);
        List<int>? path = null;
        try
        {
            for (int i = 0; i < count; i++)
            {
                if (state[i] != 0) continue;
                if (buffer[i].CursiveTo < 0)
                {
                    result[i] = buffer[i].YOffset;
                    state[i] = 2;
                    continue;
                }
                Resolve(i);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(state);
        }

        void Resolve(int start)
        {
            path ??= new List<int>();
            path.Clear();
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
        var plan = StagePlan(font, table, scriptTags, stages);
        var engine = new LayoutEngine(table, font.Layout.Gdef, buffer, font.Font.GlyphCount, rightToLeft);
        foreach (var stage in plan)
        {
            foreach (var (lookup, mask) in stage) engine.Apply(lookup, mask);
        }
    }

    // The lookups each stage applies, in lookup-list order, with the mask bits of the stage's features that use them
    // (bit i for the i-th feature counted across all stages). Built once per font, table, script list, and stage list.
    private static (int Lookup, ulong Mask)[][] StagePlan(PdfFont font, LayoutTable table, IReadOnlyList<string> scriptTags, IReadOnlyList<string[]> stages)
    {
        var plans = StagePlans.GetValue(font, _ => new ConcurrentDictionary<StagePlanKey, (int, ulong)[][]>());
        if (plans.TryGetValue(new StagePlanKey(table, scriptTags, stages), out var cached)) return cached;

        var allFeatures = stages.SelectMany(s => s).Distinct(StringComparer.Ordinal).ToList();
        var lookups = table.FeatureLookups(scriptTags, allFeatures, font.Instance.Coordinates, out _);
        var plan = new (int Lookup, ulong Mask)[stages.Count][];
        int bit = 0;
        for (int s = 0; s < stages.Count; s++)
        {
            var stageLookups = new SortedDictionary<int, ulong>();
            foreach (var feature in stages[s])
            {
                ulong mask = FeatureBit(bit++);
                if (!lookups.TryGetValue(feature, out var list)) continue;
                foreach (int lookup in list) stageLookups[lookup] = stageLookups.GetValueOrDefault(lookup) | mask;
            }
            plan[s] = stageLookups.Select(p => (p.Key, p.Value)).ToArray();
        }
        // The stored key owns copies, so a caller's lists never change a cached plan (equal plans may race; either is kept).
        plans.TryAdd(new StagePlanKey(table, scriptTags.ToArray(), stages.Select(stage => stage.ToArray()).ToArray()), plan);
        return plan;
    }

    /// <summary>
    /// Returns the mask bit of the <paramref name="index"/>-th feature of a shaping plan.
    /// </summary>
    public static ulong FeatureBit(int index) => index < 64 ? 1UL << index : 0;

    #endregion

    #endregion

    #region Nested Types

    // A stage plan's key: the table, and the script tags and stage features by value (ordinal), so equal lists from
    // any caller share one plan.
    private readonly struct StagePlanKey : IEquatable<StagePlanKey>
    {
        private readonly LayoutTable _table;
        private readonly IReadOnlyList<string> _scriptTags;
        private readonly IReadOnlyList<string[]> _stages;

        public StagePlanKey(LayoutTable table, IReadOnlyList<string> scriptTags, IReadOnlyList<string[]> stages)
        {
            _table = table;
            _scriptTags = scriptTags;
            _stages = stages;
        }

        public bool Equals(StagePlanKey other)
        {
            if (!ReferenceEquals(_table, other._table) || _scriptTags.Count != other._scriptTags.Count || _stages.Count != other._stages.Count) return false;
            for (int i = 0; i < _scriptTags.Count; i++)
                if (!string.Equals(_scriptTags[i], other._scriptTags[i], StringComparison.Ordinal)) return false;
            for (int s = 0; s < _stages.Count; s++)
                if (!_stages[s].AsSpan().SequenceEqual(other._stages[s])) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is StagePlanKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            // Index loops: enumerating the lists through their interfaces would allocate on every lookup.
            hash.Add(RuntimeHelpers.GetHashCode(_table));
            for (int i = 0; i < _scriptTags.Count; i++) hash.Add(_scriptTags[i], StringComparer.Ordinal);
            for (int s = 0; s < _stages.Count; s++)
            {
                var stage = _stages[s];
                hash.Add(stage.Length);
                for (int i = 0; i < stage.Length; i++) hash.Add(stage[i], StringComparer.Ordinal);
            }
            return hash.ToHashCode();
        }
    }

    #endregion
}
