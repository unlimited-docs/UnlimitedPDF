namespace UnlimitedPDF.Models.Internal.Text.Bidi;

/// <summary>
/// One paragraph of text resolved by the Unicode Bidirectional Algorithm (UAX #9, Unicode 17.0.0; WR-041): rules P2
/// and P3 (paragraph level), X1 to X10 (explicit embeddings, overrides, and isolates, at most 125 levels), W1 to W7,
/// N0 (paired brackets, at most 63 open), N1 and N2, I1 and I2 (resolved levels), and per line L1 (trailing
/// whitespace and separators back to the paragraph level) and L2 (reordering). Characters removed by X9 (embedding and
/// override controls, boundary neutrals) keep a level for convenience but draw nothing. Every step is linear in the
/// paragraph length, apart from N0, whose work is bounded by the 63-bracket stack (A06).
/// </summary>
internal sealed class BidiParagraph
{
    #region Fields and Constants

    public const int MaxDepth = 125;
    public const int MaxBracketPairs = 63;

    private readonly BidiClass[] _initial;
    private readonly int[] _codePoints;
    private readonly byte[] _levels;
    private readonly bool[] _removed;
    private readonly int[] _starts;

    #endregion

    #region Constructors and Finalizers

    private BidiParagraph(BidiClass[] initial, int[] codePoints, int[] starts, int? baseLevel)
    {
        _initial = initial;
        _codePoints = codePoints;
        _starts = starts;
        _levels = new byte[initial.Length];
        _removed = new bool[initial.Length];
        BaseLevel = baseLevel ?? FirstStrongLevel(initial, 0, initial.Length) ?? 0;
        Resolve();
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the paragraph embedding level: 0 for left to right, 1 for right to left.
    /// </summary>
    public int BaseLevel { get; }

    /// <summary>
    /// Gets the number of code points in the paragraph.
    /// </summary>
    public int Count => _levels.Length;

    /// <summary>
    /// Gets whether any code point resolves to a right-to-left (odd) level, or the paragraph itself is right to left.
    /// </summary>
    public bool HasRightToLeft => BaseLevel == 1 || _levels.Any(l => (l & 1) == 1);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Resolves the levels of <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The paragraph.</param>
    /// <param name="baseLevel">0 or 1 to set the paragraph's direction; null to take it from the first strong character.</param>
    /// <returns>The resolved paragraph.</returns>
    public static BidiParagraph Create(string text, int? baseLevel)
    {
        var codePoints = new List<int>(text.Length);
        var starts = new List<int>(text.Length);
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            starts.Add(i);
            codePoints.Add(char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i]);
        }
        var types = codePoints.Select(BidiData.ClassOf).ToArray();
        return new BidiParagraph(types, codePoints.ToArray(), starts.ToArray(), baseLevel);
    }

    /// <summary>
    /// Resolves the levels of a sequence of bidirectional classes (the conformance test form); code points, when given,
    /// supply paired brackets.
    /// </summary>
    public static BidiParagraph Create(BidiClass[] types, int[]? codePoints, int? baseLevel) =>
        new((BidiClass[])types.Clone(), codePoints ?? new int[types.Length], Enumerable.Range(0, types.Length).ToArray(), baseLevel);

    /// <summary>
    /// Returns the direction of the first strong character of <paramref name="text"/> outside isolates: 0 for left to
    /// right, 1 for right to left, or null when it has none (P2, P3).
    /// </summary>
    public static int? FirstStrongLevel(string text)
    {
        var types = new List<BidiClass>(text.Length);
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            types.Add(BidiData.ClassOf(char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i]));
        return FirstStrongLevel(types.ToArray(), 0, types.Count);
    }

    /// <summary>
    /// Returns the code point index that starts at UTF-16 index <paramref name="utf16Index"/>, or the one containing it.
    /// </summary>
    public int IndexOf(int utf16Index)
    {
        int index = Array.BinarySearch(_starts, utf16Index);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>
    /// Returns whether rule X9 removed code point <paramref name="index"/> (it draws nothing).
    /// </summary>
    public bool IsRemoved(int index) => _removed[index];

    /// <summary>
    /// Returns whether code point <paramref name="index"/> is a bidirectional format character, which draws nothing: one
    /// X9 removed, an isolate control (U+2066 to U+2069), or a mark (U+200E, U+200F, U+061C).
    /// </summary>
    public bool IsFormat(int index) => _removed[index] || IsFormatCharacter(_codePoints[index]);

    /// <summary>
    /// Returns whether <paramref name="codePoint"/> is a bidirectional format character (AC-04): the marks, embeddings,
    /// overrides, and isolates.
    /// </summary>
    public static bool IsFormatCharacter(int codePoint) =>
        codePoint is 0x200E or 0x200F or 0x061C or >= 0x202A and <= 0x202E or >= 0x2066 and <= 0x2069;

    /// <summary>
    /// Returns the resolved level of code point <paramref name="index"/>, before line rules.
    /// </summary>
    public int LevelAt(int index) => _levels[index];

    /// <summary>
    /// Returns the levels of the line from code point <paramref name="start"/> to <paramref name="end"/> (exclusive)
    /// after rule L1.
    /// </summary>
    public byte[] LineLevels(int start, int end)
    {
        var levels = new byte[end - start];
        Array.Copy(_levels, start, levels, 0, levels.Length);
        // L1: segment and paragraph separators, and whitespace and isolate controls before them or at the line's end,
        // return to the paragraph level. Characters removed by X9 stay in such a sequence.
        bool trailing = true;
        for (int i = end - 1; i >= start; i--)
        {
            var type = _initial[i];
            if (type is BidiClass.S or BidiClass.B)
            {
                levels[i - start] = (byte)BaseLevel;
                trailing = true;
            }
            else if (IsWhitespaceForL1(type) || _removed[i])
            {
                if (trailing) levels[i - start] = (byte)BaseLevel;
            }
            else trailing = false;
        }
        return levels;
    }

    /// <summary>
    /// Returns the code point indices of the line from <paramref name="start"/> to <paramref name="end"/> (exclusive) in
    /// visual order, left to right, without the characters X9 removed (L1, L2).
    /// </summary>
    public int[] VisualOrder(int start, int end)
    {
        var levels = LineLevels(start, end);
        var order = Enumerable.Range(start, end - start).ToArray();
        Reverse(order, levels);
        return order.Where(i => !_removed[i]).ToArray();
    }

    /// <summary>
    /// Returns whether code point <paramref name="index"/> is drawn mirrored: its level is odd and it has a mirrored
    /// character (L4).
    /// </summary>
    public bool IsMirrored(int index) => (_levels[index] & 1) == 1 && BidiData.MirrorOf(_codePoints[index]) >= 0;

    /// <summary>
    /// Reverses <paramref name="order"/> by rule L2: from the highest level down to the lowest odd level, every run at
    /// that level or higher is reversed. <paramref name="levels"/> is permuted with it.
    /// </summary>
    public static void Reverse<T>(T[] order, byte[] levels)
    {
        if (order.Length == 0) return;
        int highest = 0, lowestOdd = int.MaxValue;
        foreach (byte level in levels)
        {
            highest = Math.Max(highest, level);
            if ((level & 1) == 1) lowestOdd = Math.Min(lowestOdd, level);
        }
        for (int level = highest; level >= lowestOdd; level--)
        {
            for (int i = 0; i < order.Length; i++)
            {
                if (levels[i] < level) continue;
                int j = i;
                while (j + 1 < order.Length && levels[j + 1] >= level) j++;
                Array.Reverse(order, i, j - i + 1);
                Array.Reverse(levels, i, j - i + 1);
                i = j;
            }
        }
    }

    #endregion

    #region Private Methods

    private static bool IsWhitespaceForL1(BidiClass type) => type is BidiClass.WS or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;

    private static bool IsIsolateInitiator(BidiClass type) => type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;

    private static bool IsRemovedByX9(BidiClass type) =>
        type is BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO or BidiClass.PDF or BidiClass.BN;

    private static bool IsNeutralOrIsolate(BidiClass type) =>
        type is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;

    // P2, P3: the first L, R, or AL from start, skipping isolates and stopping at a paragraph separator.
    private static int? FirstStrongLevel(BidiClass[] types, int start, int end)
    {
        int depth = 0;
        for (int i = start; i < end; i++)
        {
            var type = types[i];
            if (IsIsolateInitiator(type)) depth++;
            else if (type == BidiClass.PDI) { if (depth > 0) depth--; }
            else if (type == BidiClass.B) return null;
            else if (depth == 0 && type == BidiClass.L) return 0;
            else if (depth == 0 && type is BidiClass.R or BidiClass.AL) return 1;
        }
        return null;
    }

    private void Resolve()
    {
        int n = _initial.Length;
        var types = (BidiClass[])_initial.Clone();
        var matchingPdi = MatchIsolates(out var matchedByInitiator);

        ResolveExplicit(types, matchingPdi);
        for (int i = 0; i < n; i++) _removed[i] = IsRemovedByX9(_initial[i]);

        foreach (var sequence in IsolatingRunSequences(matchingPdi, matchedByInitiator))
            ResolveSequence(sequence, types, matchingPdi);

        // I1, I2
        for (int i = 0; i < n; i++)
        {
            if (_removed[i]) continue;
            int level = _levels[i];
            var type = types[i];
            if ((level & 1) == 0)
            {
                if (type == BidiClass.R) level += 1;
                else if (type is BidiClass.AN or BidiClass.EN) level += 2;
            }
            else if (type is BidiClass.L or BidiClass.EN or BidiClass.AN) level += 1;
            _levels[i] = (byte)level;
        }

        // Removed characters take the level of the character before them (or the paragraph level), so lines and
        // reordering keep them next to their neighbors.
        int previous = BaseLevel;
        for (int i = 0; i < n; i++)
        {
            if (_removed[i]) _levels[i] = (byte)previous;
            else previous = _levels[i];
        }
    }

    // BD9: the matching PDI of each isolate initiator (-1 when none), within one paragraph.
    private int[] MatchIsolates(out bool[] matchedByInitiator)
    {
        int n = _initial.Length;
        var matching = Enumerable.Repeat(-1, n).ToArray();
        matchedByInitiator = new bool[n];
        var open = new Stack<int>();
        for (int i = 0; i < n; i++)
        {
            var type = _initial[i];
            if (IsIsolateInitiator(type)) open.Push(i);
            else if (type == BidiClass.PDI && open.Count > 0)
            {
                int initiator = open.Pop();
                matching[initiator] = i;
                matchedByInitiator[i] = true;
            }
            else if (type == BidiClass.B) open.Clear();
        }
        return matching;
    }

    // X1 to X8: explicit levels and overrides.
    private void ResolveExplicit(BidiClass[] types, int[] matchingPdi)
    {
        int n = types.Length;
        var levels = new int[MaxDepth + 2];
        var overrides = new BidiClass?[MaxDepth + 2];
        var isolates = new bool[MaxDepth + 2];
        int top = 0;
        levels[0] = BaseLevel;
        int overflowIsolates = 0, overflowEmbeddings = 0, validIsolates = 0;

        for (int i = 0; i < n; i++)
        {
            var type = types[i];
            switch (type)
            {
                case BidiClass.RLE:
                case BidiClass.LRE:
                case BidiClass.RLO:
                case BidiClass.LRO:
                {
                    _levels[i] = (byte)levels[top];
                    bool rtl = type is BidiClass.RLE or BidiClass.RLO;
                    int level = rtl ? (levels[top] + 1) | 1 : (levels[top] + 2) & ~1;
                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        top++;
                        levels[top] = level;
                        overrides[top] = type == BidiClass.RLO ? BidiClass.R : type == BidiClass.LRO ? BidiClass.L : null;
                        isolates[top] = false;
                    }
                    else if (overflowIsolates == 0) overflowEmbeddings++;
                    break;
                }
                case BidiClass.RLI:
                case BidiClass.LRI:
                case BidiClass.FSI:
                {
                    _levels[i] = (byte)levels[top];
                    if (overrides[top] is { } forced) types[i] = forced;
                    bool rtl = type == BidiClass.RLI;
                    if (type == BidiClass.FSI)
                    {
                        int end = matchingPdi[i] >= 0 ? matchingPdi[i] : n;
                        rtl = FirstStrongLevel(_initial, i + 1, end) == 1;
                    }
                    int level = rtl ? (levels[top] + 1) | 1 : (levels[top] + 2) & ~1;
                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        validIsolates++;
                        top++;
                        levels[top] = level;
                        overrides[top] = null;
                        isolates[top] = true;
                    }
                    else overflowIsolates++;
                    break;
                }
                case BidiClass.PDI:
                {
                    if (overflowIsolates > 0) overflowIsolates--;
                    else if (validIsolates > 0)
                    {
                        overflowEmbeddings = 0;
                        while (!isolates[top]) top--;
                        top--;
                        validIsolates--;
                    }
                    _levels[i] = (byte)levels[top];
                    if (overrides[top] is { } forced) types[i] = forced;
                    break;
                }
                case BidiClass.PDF:
                {
                    _levels[i] = (byte)levels[top];
                    if (overflowIsolates > 0) { }
                    else if (overflowEmbeddings > 0) overflowEmbeddings--;
                    else if (!isolates[top] && top > 0) top--;
                    break;
                }
                case BidiClass.B:
                {
                    // X8: a paragraph separator ends every embedding, override, and isolate.
                    _levels[i] = (byte)BaseLevel;
                    top = 0;
                    overflowIsolates = overflowEmbeddings = validIsolates = 0;
                    break;
                }
                case BidiClass.BN:
                    _levels[i] = (byte)levels[top];
                    break;
                default:
                    _levels[i] = (byte)levels[top];
                    if (overrides[top] is { } over) types[i] = over;
                    break;
            }
        }
    }

    // X10: level runs of the characters X9 keeps, chained across isolates into isolating run sequences.
    private List<int[]> IsolatingRunSequences(int[] matchingPdi, bool[] matchedByInitiator)
    {
        int n = _initial.Length;
        var runs = new List<List<int>>();
        var runOf = new int[n];
        int lastLevel = -1;
        for (int i = 0; i < n; i++)
        {
            if (_removed[i]) { runOf[i] = -1; continue; }
            if (runs.Count == 0 || _levels[i] != lastLevel) runs.Add(new List<int>());
            runs[^1].Add(i);
            runOf[i] = runs.Count - 1;
            lastLevel = _levels[i];
        }

        var sequences = new List<int[]>();
        foreach (var run in runs)
        {
            int first = run[0];
            // A run that starts with a PDI matched to an initiator continues that initiator's sequence.
            if (_initial[first] == BidiClass.PDI && matchedByInitiator[first]) continue;
            var sequence = new List<int>(run);
            while (true)
            {
                int last = sequence[^1];
                var type = _initial[last];
                if (!IsIsolateInitiator(type) || matchingPdi[last] < 0) break;
                int pdi = matchingPdi[last];
                if (runOf[pdi] < 0) break;
                sequence.AddRange(runs[runOf[pdi]]);
            }
            sequences.Add(sequence.ToArray());
        }
        return sequences;
    }

    private void ResolveSequence(int[] sequence, BidiClass[] types, int[] matchingPdi)
    {
        int count = sequence.Length;
        int level = _levels[sequence[0]];

        // sos and eos from the higher of this level and the neighbors' (or the paragraph's).
        int before = BaseLevel;
        for (int i = sequence[0] - 1; i >= 0; i--) if (!_removed[i]) { before = _levels[i]; break; }
        int lastIndex = sequence[^1];
        int after = BaseLevel;
        if (!IsIsolateInitiator(_initial[lastIndex]))
        {
            for (int i = lastIndex + 1; i < _initial.Length; i++) if (!_removed[i]) { after = _levels[i]; break; }
        }
        var sos = (Math.Max(level, before) & 1) == 1 ? BidiClass.R : BidiClass.L;
        var eos = (Math.Max(level, after) & 1) == 1 ? BidiClass.R : BidiClass.L;

        var t = new BidiClass[count];
        for (int k = 0; k < count; k++) t[k] = types[sequence[k]];

        // W1: nonspacing marks take the type before them (ON after an isolate control, sos at the start).
        for (int k = 0; k < count; k++)
        {
            if (t[k] != BidiClass.NSM) continue;
            if (k == 0) t[k] = sos;
            else t[k] = t[k - 1] is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI ? BidiClass.ON : t[k - 1];
        }
        // W2, W3: European numbers after Arabic letters become Arabic numbers; Arabic letters become R.
        var lastStrong = sos;
        for (int k = 0; k < count; k++)
        {
            var type = t[k];
            if (type is BidiClass.L or BidiClass.R or BidiClass.AL) lastStrong = type;
            else if (type == BidiClass.EN && lastStrong == BidiClass.AL) t[k] = BidiClass.AN;
        }
        for (int k = 0; k < count; k++) if (t[k] == BidiClass.AL) t[k] = BidiClass.R;
        // W4: a single separator between two numbers of the same kind joins them.
        for (int k = 1; k + 1 < count; k++)
        {
            if (t[k] == BidiClass.ES && t[k - 1] == BidiClass.EN && t[k + 1] == BidiClass.EN) t[k] = BidiClass.EN;
            else if (t[k] == BidiClass.CS && t[k - 1] == BidiClass.EN && t[k + 1] == BidiClass.EN) t[k] = BidiClass.EN;
            else if (t[k] == BidiClass.CS && t[k - 1] == BidiClass.AN && t[k + 1] == BidiClass.AN) t[k] = BidiClass.AN;
        }
        // W5: terminators next to European numbers become European numbers.
        for (int k = 0; k < count; k++)
        {
            if (t[k] != BidiClass.ET) continue;
            int end = k;
            while (end < count && t[end] == BidiClass.ET) end++;
            bool number = (k > 0 && t[k - 1] == BidiClass.EN) || (end < count && t[end] == BidiClass.EN);
            if (number) for (int j = k; j < end; j++) t[j] = BidiClass.EN;
            k = end - 1;
        }
        // W6: other separators and terminators become neutral.
        for (int k = 0; k < count; k++) if (t[k] is BidiClass.ES or BidiClass.ET or BidiClass.CS) t[k] = BidiClass.ON;
        // W7: European numbers after left-to-right text become L.
        lastStrong = sos;
        for (int k = 0; k < count; k++)
        {
            if (t[k] is BidiClass.L or BidiClass.R) lastStrong = t[k];
            else if (t[k] == BidiClass.EN && lastStrong == BidiClass.L) t[k] = BidiClass.L;
        }

        var embedding = (level & 1) == 1 ? BidiClass.R : BidiClass.L;
        ResolveBrackets(sequence, t, sos, embedding);

        // N1, N2: neutrals between strong types of one direction take it; the rest take the embedding direction.
        for (int k = 0; k < count; k++)
        {
            if (!IsNeutralOrIsolate(t[k])) continue;
            int end = k;
            while (end < count && IsNeutralOrIsolate(t[end])) end++;
            var leading = k == 0 ? sos : Strength(t[k - 1]);
            var trailing = end == count ? eos : Strength(t[end]);
            var resolved = leading == trailing ? leading : embedding;
            for (int j = k; j < end; j++) t[j] = resolved;
            k = end - 1;
        }

        for (int k = 0; k < count; k++) types[sequence[k]] = t[k];
    }

    // Numbers count as R for neutrals and brackets (N0, N1).
    private static BidiClass Strength(BidiClass type) => type is BidiClass.EN or BidiClass.AN ? BidiClass.R : type;

    // BD16 and N0: paired brackets take the embedding direction, or the opposite one when their content and context
    // are of it.
    private void ResolveBrackets(int[] sequence, BidiClass[] t, BidiClass sos, BidiClass embedding)
    {
        int count = sequence.Length;
        var pairs = new List<(int Open, int Close)>();
        var stack = new List<(int Bracket, int Position)>();
        for (int k = 0; k < count; k++)
        {
            if (t[k] != BidiClass.ON) continue;
            int cp = _codePoints[sequence[k]];
            if (!BidiData.TryGetBracket(cp, out int pair, out bool opening)) continue;
            if (opening)
            {
                if (stack.Count == MaxBracketPairs) break; // BD16: stop at the stack limit
                stack.Add((Canonical(pair), k));
            }
            else
            {
                int canonical = Canonical(cp);
                for (int s = stack.Count - 1; s >= 0; s--)
                {
                    if (stack[s].Bracket != canonical) continue;
                    pairs.Add((stack[s].Position, k));
                    stack.RemoveRange(s, stack.Count - s);
                    break;
                }
            }
        }
        pairs.Sort((a, b) => a.Open.CompareTo(b.Open));

        var opposite = embedding == BidiClass.L ? BidiClass.R : BidiClass.L;
        foreach (var (open, close) in pairs)
        {
            bool sameInside = false, oppositeInside = false;
            for (int k = open + 1; k < close; k++)
            {
                var strength = Strength(t[k]);
                if (strength == embedding) { sameInside = true; break; }
                if (strength == opposite) oppositeInside = true;
            }
            BidiClass? resolved = null;
            if (sameInside) resolved = embedding;
            else if (oppositeInside)
            {
                var context = sos;
                for (int k = open - 1; k >= 0; k--)
                {
                    var strength = Strength(t[k]);
                    if (strength is BidiClass.L or BidiClass.R) { context = strength; break; }
                }
                resolved = context == opposite ? opposite : embedding;
            }
            if (resolved is not { } direction) continue;
            t[open] = t[close] = direction;
            // Nonspacing marks that followed a bracket take its new type.
            for (int k = open + 1; k < count && _initial[sequence[k]] == BidiClass.NSM; k++) t[k] = direction;
            for (int k = close + 1; k < count && _initial[sequence[k]] == BidiClass.NSM; k++) t[k] = direction;
        }
    }

    // Angle brackets U+2329 and U+232A are canonically equivalent to U+3008 and U+3009.
    private static int Canonical(int codePoint) => codePoint switch
    {
        0x2329 => 0x3008,
        0x232A => 0x3009,
        _ => codePoint
    };

    #endregion

    #endregion
}
