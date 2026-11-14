using UnlimitedPDF.Models.Internal.Text.Layout;

namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// Shapes Devanagari, Telugu, and Tamil text with a font's OpenType rules (D-003, AC-05), following the OpenType Indic
/// shaping model: split vowel signs are decomposed, text is cut into syllables (a dotted circle stands in for a missing
/// base), each syllable is reordered (base consonant, reph, pre-base vowel signs) and masked so per-position features
/// act only where they belong, the basic features run in specification order, a final reordering places pre-base
/// vowel signs and the reph, and the presentation and positioning features finish the run. Fonts with only the
/// version-1 script tags get the version-1 halant order for below-base consonants. Every glyph of a syllable shares
/// the syllable's cluster, so the syllable's text stays together for copy and search.
/// </summary>
internal static class IndicShaper
{
    #region Fields and Constants

    private static readonly string[][] BasicStages =
    {
        new[] { "locl", "ccmp" },
        new[] { "nukt" }, new[] { "akhn" }, new[] { "rphf" }, new[] { "rkrf" }, new[] { "pref" }, new[] { "blwf" },
        new[] { "abvf" }, new[] { "half" }, new[] { "pstf" }, new[] { "vatu" }, new[] { "cjct" }
    };

    private static readonly string[][] PresentationStages =
    {
        new[] { "init", "pres", "abvs", "blws", "psts", "haln" },
        new[] { "calt", "clig" }
    };

    private static readonly string[] PositioningFeatures = { "kern", "dist", "abvm", "blwm", "mark", "mkmk" };

    // The bit of each basic feature, in the order RunStages numbers them (see TextShaper.FeatureBit).
    private static readonly ulong RphfBit = TextShaper.FeatureBit(4);
    private static readonly ulong BlwfBit = TextShaper.FeatureBit(7);
    private static readonly ulong AbvfBit = TextShaper.FeatureBit(8);
    private static readonly ulong HalfBit = TextShaper.FeatureBit(9);
    private static readonly ulong PstfBit = TextShaper.FeatureBit(10);
    private static readonly ulong PrefBit = TextShaper.FeatureBit(6);
    private static readonly ulong PerPositionBits = RphfBit | BlwfBit | AbvfBit | HalfBit | PstfBit | PrefBit;

    // Split vowel signs and their parts (canonical decompositions), so each part takes its own position.
    private static readonly Dictionary<int, int[]> SplitVowels = new()
    {
        [0x0BCA] = new[] { 0x0BC6, 0x0BBE }, // TAMIL O
        [0x0BCB] = new[] { 0x0BC7, 0x0BBE }, // TAMIL OO
        [0x0BCC] = new[] { 0x0BC6, 0x0BD7 }, // TAMIL AU
        [0x0C48] = new[] { 0x0C46, 0x0C56 }  // TELUGU AI
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Shapes one run of Indic text.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="text">The run's text.</param>
    /// <param name="script">The run's script: <c>deva</c>, <c>telu</c>, or <c>taml</c>.</param>
    /// <returns>The shaped run.</returns>
    public static ShapedRun Shape(PdfFont font, string text, string script)
    {
        var plan = Plan.For(font, script);
        var buffer = Map(font, text);
        var syllables = Segment(font, buffer);

        foreach (var syllable in syllables) InitialReorder(plan, buffer, syllable);

        if (font.Layout.Gsub is not null) TextShaper.Substitute(font, buffer, plan.ScriptTags, BasicStages);
        FinalReorder(plan, buffer);
        foreach (var glyph in buffer) glyph.Mask = ulong.MaxValue;
        if (font.Layout.Gsub is not null) TextShaper.Substitute(font, buffer, plan.ScriptTags, PresentationStages);

        // Joiners have done their work (blocking or requesting forms); they are not drawn.
        buffer.RemoveAll(g => g.Category is Cat.Zwj or Cat.Zwnj);
        return TextShaper.Finish(font, buffer, plan.ScriptTags, PositioningFeatures);
    }

    #endregion

    #region Private Methods

    // Characters to glyphs, with split vowel signs decomposed; each glyph knows its category and position class.
    private static List<ShapingGlyph> Map(PdfFont font, string text)
    {
        var buffer = new List<ShapingGlyph>(text.Length + 4);
        var gdef = font.Layout.Gdef;
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            int codePoint = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
            var parts = SplitVowels.TryGetValue(codePoint, out var split) && split.All(p => font.Font.CharacterMap.GlyphFor(p) != 0)
                ? split
                : new[] { codePoint };
            foreach (int part in parts) buffer.Add(Glyph(font, gdef, part, i));
        }
        return buffer;
    }

    private static ShapingGlyph Glyph(PdfFont font, GlyphDefinitions? gdef, int codePoint, int cluster)
    {
        var glyph = new ShapingGlyph(font.Font.CharacterMap.GlyphFor(codePoint), cluster) { CodePoint = codePoint, Category = Category(codePoint) };
        glyph.Position = (int)IndicData.Positional(codePoint) << 8; // the positional category, kept in the high byte
        glyph.GlyphClass = gdef is { HasGlyphClasses: true }
            ? Math.Max(1, gdef.GlyphClass(glyph.Glyph))
            : glyph.Category is Cat.Matra or Cat.Nukta or Cat.Halant or Cat.Modifier ? 3 : 1;
        return glyph;
    }

    private static int Category(int codePoint)
    {
        if (codePoint is 0x0930 or 0x0C30 or 0x0BB0) return Cat.Ra;
        return IndicData.Syllabic(codePoint) switch
        {
            IndicSyllabicCategory.Consonant or IndicSyllabicCategory.ConsonantDead or IndicSyllabicCategory.ConsonantWithStacker
                or IndicSyllabicCategory.ConsonantPrefixed or IndicSyllabicCategory.ConsonantHeadLetter => Cat.Consonant,
            IndicSyllabicCategory.ConsonantPlaceholder => Cat.Placeholder,
            IndicSyllabicCategory.VowelIndependent => Cat.Vowel,
            IndicSyllabicCategory.VowelDependent => Cat.Matra,
            IndicSyllabicCategory.Virama or IndicSyllabicCategory.PureKiller or IndicSyllabicCategory.InvisibleStacker => Cat.Halant,
            IndicSyllabicCategory.Nukta => Cat.Nukta,
            IndicSyllabicCategory.Bindu or IndicSyllabicCategory.Visarga or IndicSyllabicCategory.CantillationMark
                or IndicSyllabicCategory.SyllableModifier or IndicSyllabicCategory.ToneMark or IndicSyllabicCategory.GeminationMark
                or IndicSyllabicCategory.RegisterShifter => Cat.Modifier,
            IndicSyllabicCategory.Joiner => Cat.Zwj,
            IndicSyllabicCategory.NonJoiner => Cat.Zwnj,
            _ => Cat.Other
        };
    }

    private static int Cat0(List<ShapingGlyph> buffer, int i) => i < buffer.Count ? buffer[i].Category : Cat.Other;

    private static bool IsConsonant(int category) => category is Cat.Consonant or Cat.Ra;

    // Cuts the buffer into syllables (start, end, consonant syllable or not) and numbers each glyph's syllable. A
    // syllable that starts with a vowel sign, halant, nukta, or modifier gets a dotted circle as its base when the
    // font has one.
    private static List<(int Start, int End, bool Consonant)> Segment(PdfFont font, List<ShapingGlyph> buffer)
    {
        var syllables = new List<(int, int, bool)>();
        int i = 0;
        while (i < buffer.Count)
        {
            int c = buffer[i].Category;
            int start = i;
            bool consonant = false;
            if (c is Cat.Matra or Cat.Halant or Cat.Nukta or Cat.Modifier)
            {
                int dotted = font.Font.CharacterMap.GlyphFor(0x25CC);
                if (dotted != 0)
                {
                    var placeholder = Glyph(font, font.Layout.Gdef, 0x25CC, buffer[i].Cluster);
                    placeholder.CodePoint = 0; // inserted, stands for no character
                    buffer.Insert(i, placeholder);
                    c = Cat.Placeholder;
                }
            }

            if (IsConsonant(c) || c == Cat.Placeholder)
            {
                consonant = true;
                int j = i + 1;
                if (Cat0(buffer, j) == Cat.Nukta) j++;
                while (Cat0(buffer, j) == Cat.Halant)
                {
                    int k = j + 1;
                    if (Cat0(buffer, k) is Cat.Zwj or Cat.Zwnj) k++;
                    if (!IsConsonant(Cat0(buffer, k))) break;
                    j = k + 1;
                    if (Cat0(buffer, j) == Cat.Nukta) j++;
                }
                if (Cat0(buffer, j) == Cat.Halant)
                {
                    j++;
                    if (Cat0(buffer, j) is Cat.Zwj or Cat.Zwnj) j++;
                }
                else
                {
                    while (Cat0(buffer, j) is Cat.Matra or Cat.Nukta) j++;
                    if (Cat0(buffer, j) == Cat.Halant) j++;
                }
                while (Cat0(buffer, j) == Cat.Modifier) j++;
                i = j;
            }
            else if (c == Cat.Vowel)
            {
                int j = i + 1;
                if (Cat0(buffer, j) == Cat.Nukta) j++;
                while (Cat0(buffer, j) is Cat.Matra or Cat.Nukta) j++;
                while (Cat0(buffer, j) == Cat.Modifier) j++;
                i = j;
            }
            else if (c is Cat.Matra or Cat.Halant or Cat.Nukta or Cat.Modifier)
            {
                int j = i + 1;
                while (Cat0(buffer, j) is Cat.Matra or Cat.Nukta or Cat.Halant or Cat.Modifier) j++;
                i = j;
            }
            else
            {
                i++;
            }

            int index = syllables.Count;
            for (int k = start; k < i; k++) buffer[k].Syllable = index;
            syllables.Add((start, i, consonant));
        }
        return syllables;
    }

    // Base consonant, reph, position classes, sort, masks; every glyph of the syllable takes its first cluster.
    private static void InitialReorder(Plan plan, List<ShapingGlyph> buffer, (int Start, int End, bool Consonant) syllable)
    {
        var (start, end, consonant) = syllable;
        int cluster = Enumerable.Range(start, end - start).Min(i => buffer[i].Cluster);
        for (int i = start; i < end; i++)
        {
            buffer[i].Cluster = cluster;
            buffer[i].Mask = ulong.MaxValue & ~PerPositionBits; // per-position features act only in consonant syllables
            buffer[i].Position = (buffer[i].Position & ~0xFF) | 7; // no reordering class
        }
        if (!consonant) return;

        // Reph: Ra + halant at the start of a syllable that goes on, when the font forms a reph from it.
        int limit = start;
        bool reph = plan.HasReph && end - start >= 3 && buffer[start].Category == Cat.Ra && buffer[start + 1].Category == Cat.Halant
            && (!plan.ExplicitReph || Cat0(buffer, start + 2) == Cat.Zwj)
            && plan.WouldSubstitute("rphf", buffer[start].Glyph, buffer[start + 1].Glyph);
        if (reph) limit = start + 2 + (plan.ExplicitReph ? 1 : 0);

        // Base: the last consonant that has no below-base or post-base form after a halant; else the first one.
        int baseIndex = -1;
        for (int i = end - 1; i >= limit; i--)
        {
            if (!IsConsonant(buffer[i].Category) && buffer[i].Category != Cat.Placeholder) continue;
            bool afterHalant = i > limit && (buffer[i - 1].Category == Cat.Halant || (i > limit + 1 && buffer[i - 1].Category is Cat.Zwj or Cat.Zwnj && buffer[i - 2].Category == Cat.Halant));
            if (afterHalant && plan.BelowOrPostBase(buffer[i - (buffer[i - 1].Category == Cat.Halant ? 1 : 2)].Glyph, buffer[i].Glyph)) continue;
            baseIndex = i;
            break;
        }
        if (baseIndex < 0)
        {
            for (int i = limit; i < end; i++)
            {
                if (IsConsonant(buffer[i].Category) || buffer[i].Category == Cat.Placeholder) { baseIndex = i; break; }
            }
        }
        if (baseIndex < 0) return;

        // Position classes: 0 reph, 1 pre-base vowel sign, 2 pre-base consonants, 3 base, 4 post-base consonants,
        // 5 other vowel signs and nuktas after the base, 6 modifiers.
        for (int i = start; i < end; i++)
        {
            var g = buffer[i];
            int cls;
            if (reph && i < limit) cls = 0;
            else if (g.Category == Cat.Matra && PositionalOf(g) is IndicPositionalCategory.Left or IndicPositionalCategory.VisualOrderLeft) cls = 1;
            else if (i < baseIndex) cls = 2;
            else if (i == baseIndex || (i == baseIndex + 1 && g.Category == Cat.Nukta)) cls = 3;
            else if (g.Category is Cat.Modifier) cls = 6;
            else if (g.Category is Cat.Matra) cls = 5;
            else cls = 4;
            g.Position = (g.Position & ~0xFF) | cls;
        }

        // Stable sort of the syllable by class (pre-base vowel signs move to the front, after a reph).
        var sorted = buffer.GetRange(start, end - start).Select((g, k) => (g, k)).OrderBy(t => t.g.Position & 0xFF).ThenBy(t => t.k).Select(t => t.g).ToList();
        for (int i = 0; i < sorted.Count; i++) buffer[start + i] = sorted[i];

        // Version-1 fonts form below-base consonants from consonant + halant: move each such halant after its consonant.
        if (plan.OldSpec)
        {
            for (int i = start; i + 1 < end; i++)
            {
                if ((buffer[i].Position & 0xFF) == 4 && buffer[i].Category == Cat.Halant && IsConsonant(buffer[i + 1].Category))
                {
                    (buffer[i], buffer[i + 1]) = (buffer[i + 1], buffer[i]);
                    i++;
                }
            }
        }

        // Masks: the per-position features act only in their region; everything else applies everywhere.
        for (int i = start; i < end; i++)
        {
            var g = buffer[i];
            int cls = g.Position & 0xFF;
            ulong mask = ulong.MaxValue & ~PerPositionBits;
            if (cls == 0) mask |= RphfBit;
            if (cls == 2) mask |= HalfBit;
            if (cls is 3 or 4) mask |= BlwfBit | PstfBit | AbvfBit;
            g.Mask = mask;
        }

        // The pref form: only the first post-base pair the font's pref forms a glyph from. These scripts draw it where it
        // stands (after the base), as the platform shaper does; there is no pre-base reordering.
        int baseAt = -1;
        for (int i = start; i < end; i++) if ((buffer[i].Position & 0xFF) == 3) { baseAt = i; break; }
        for (int i = baseAt + 1; baseAt >= 0 && i + 1 < end; i++)
        {
            if (plan.WouldSubstitute("pref", buffer[i].Glyph, buffer[i + 1].Glyph))
            {
                buffer[i].Mask |= PrefBit;
                buffer[i + 1].Mask |= PrefBit;
                break;
            }
        }

        // A non-joiner after a pre-base halant keeps the explicit halant: no half form there.
        for (int i = start; i + 1 < end; i++)
        {
            if (buffer[i].Category == Cat.Halant && buffer[i + 1].Category == Cat.Zwnj)
            {
                buffer[i].Mask &= ~HalfBit;
                if (i > start) buffer[i - 1].Mask &= ~HalfBit;
            }
        }
    }

    private static IndicPositionalCategory PositionalOf(ShapingGlyph glyph) => (IndicPositionalCategory)(glyph.Position >> 8);

    // After the basic features: each pre-base vowel sign goes right after the last halant still standing before the
    // base (or stays at the front); the reph goes before the first post-base vowel sign or modifier, else to the end.
    private static void FinalReorder(Plan plan, List<ShapingGlyph> buffer)
    {
        int i = 0;
        while (i < buffer.Count)
        {
            int syllable = buffer[i].Syllable;
            int end = i;
            while (end < buffer.Count && buffer[end].Syllable == syllable) end++;
            ReorderSyllable(buffer, i, end);
            i = end;
        }
    }

    private static void ReorderSyllable(List<ShapingGlyph> buffer, int start, int end)
    {
        int baseIndex = -1;
        for (int i = start; i < end; i++) if ((buffer[i].Position & 0xFF) == 3) { baseIndex = i; break; }
        if (baseIndex < 0) return;

        // Pre-base vowel signs.
        var matras = Enumerable.Range(start, end - start).Where(i => (buffer[i].Position & 0xFF) == 1).ToList();
        if (matras.Count > 0)
        {
            int lastHalant = -1;
            for (int i = start; i < baseIndex; i++) if (buffer[i].Category == Cat.Halant && (buffer[i].Position & 0xFF) == 2) lastHalant = i;
            if (lastHalant > matras[^1])
            {
                var moving = matras.Select(m => buffer[m]).ToList();
                for (int k = matras.Count - 1; k >= 0; k--) buffer.RemoveAt(matras[k]);
                int target = lastHalant - matras.Count + 1;
                buffer.InsertRange(target, moving);
            }
        }

        // Reph.
        int rephIndex = -1;
        for (int i = start; i < end; i++) if ((buffer[i].Position & 0xFF) == 0) { rephIndex = i; break; }
        if (rephIndex < 0) return;
        var rephGlyph = buffer[rephIndex];
        buffer.RemoveAt(rephIndex);
        int stop = end - 1;
        int destination = stop;
        for (int i = rephIndex; i < stop; i++)
        {
            var g = buffer[i];
            int cls = g.Position & 0xFF;
            bool postMatra = cls == 5 && g.Category == Cat.Matra && PositionalOf(g) is IndicPositionalCategory.Right or IndicPositionalCategory.TopAndRight or IndicPositionalCategory.BottomAndRight;
            if (postMatra || cls == 6) { destination = i; break; }
        }
        buffer.Insert(destination, rephGlyph);
    }

    #endregion

    #endregion

    #region Nested Types

    // Shaping categories.
    private static class Cat
    {
        public const int Other = 0;
        public const int Consonant = 1;
        public const int Ra = 2;
        public const int Vowel = 3;
        public const int Matra = 4;
        public const int Halant = 5;
        public const int Nukta = 6;
        public const int Modifier = 7;
        public const int Zwj = 8;
        public const int Zwnj = 9;
        public const int Placeholder = 10;
    }

    // What a script and font need: the script tags (version 2 first), whether the font only has version-1 tags, the
    // reph rules, and cached checks of whether a feature would form a glyph.
    private sealed class Plan
    {
        private readonly PdfFont _font;
        private readonly Dictionary<string, List<int>> _lookups;
        private readonly Dictionary<(string, int, int), bool> _cache = new();

        private Plan(PdfFont font, string[] scriptTags, bool oldSpec, bool hasReph, bool explicitReph, Dictionary<string, List<int>> lookups)
        {
            _font = font;
            ScriptTags = scriptTags;
            OldSpec = oldSpec;
            HasReph = hasReph;
            ExplicitReph = explicitReph;
            _lookups = lookups;
        }

        public bool ExplicitReph { get; }

        public bool HasReph { get; }

        public bool OldSpec { get; }

        public string[] ScriptTags { get; }

        public static Plan For(PdfFont font, string script)
        {
            var (v2, v1) = script switch
            {
                "deva" => ("dev2", "deva"),
                "telu" => ("tel2", "telu"),
                _ => ("tml2", "taml")
            };
            var tags = new[] { v2, v1, "DFLT", "dflt" };
            string? found = null;
            var lookups = font.Layout.Gsub is { } gsub
                ? gsub.FeatureLookups(tags, new[] { "rphf", "blwf", "pstf", "pref", "vatu" }, font.Instance.Coordinates, out found)
                : new Dictionary<string, List<int>>();
            bool oldSpec = found == v1;
            return new Plan(font, tags, oldSpec, hasReph: script != "taml", explicitReph: script == "telu", lookups);
        }

        // Below-base or post-base: the font's blwf, vatu, pstf, or pref forms a glyph from halant + consonant (or
        // consonant + halant, the version-1 order).
        public bool BelowOrPostBase(int halant, int consonant) =>
            new[] { "blwf", "vatu", "pstf", "pref" }.Any(f => WouldSubstitute(f, halant, consonant) || WouldSubstitute(f, consonant, halant));

        public bool WouldSubstitute(string feature, int first, int second)
        {
            if (_font.Layout.Gsub is not { } gsub || !_lookups.TryGetValue(feature, out var lookups) || lookups.Count == 0) return false;
            if (_cache.TryGetValue((feature, first, second), out bool cached)) return cached;
            var gdef = _font.Layout.Gdef;
            var buffer = new List<ShapingGlyph> { new(first, 0), new(second, 1) };
            if (gdef is { HasGlyphClasses: true }) foreach (var g in buffer) g.GlyphClass = Math.Max(1, gdef.GlyphClass(g.Glyph));
            var engine = new LayoutEngine(gsub, _font.Layout.Gdef, buffer, _font.Font.GlyphCount);
            foreach (int lookup in lookups) engine.Apply(lookup, ulong.MaxValue);
            bool changed = buffer.Count != 2 || buffer[0].Glyph != first || buffer[1].Glyph != second;
            _cache[(feature, first, second)] = changed;
            return changed;
        }
    }

    #endregion
}
