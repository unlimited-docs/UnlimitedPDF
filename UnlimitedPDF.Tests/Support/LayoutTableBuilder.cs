namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Builds GSUB, GPOS, and GDEF tables for the test font builder (D-014): lookups from subtable bytes, features that
/// list lookups, and scripts whose default language system lists every feature. The static helpers write the
/// subtables the tests need; offsets inside a subtable are relative to its own start, as the format defines.
/// </summary>
internal sealed class LayoutTableBuilder
{
    #region Fields and Constants

    private readonly List<(int Type, int Flags, byte[][] Subtables, int? MarkSet)> _lookups = new();
    private readonly List<(string Tag, int[] Lookups)> _features = new();
    private readonly List<(double Min, double Max, int Feature, int[] Lookups)> _variations = new();

    #endregion

    #region Methods

    #region Public Methods

    public int AddLookup(int type, int flags, params byte[][] subtables) => AddLookup(type, flags, null, subtables);

    public int AddLookup(int type, int flags, int? markSet, params byte[][] subtables)
    {
        _lookups.Add((type, flags | (markSet is null ? 0 : 0x10), subtables, markSet));
        return _lookups.Count - 1;
    }

    /// <summary>
    /// Adds a feature and returns its index in the feature list.
    /// </summary>
    public int AddFeature(string tag, params int[] lookups)
    {
        _features.Add((tag, lookups));
        return _features.Count - 1;
    }

    /// <summary>
    /// Makes feature <paramref name="feature"/> use <paramref name="lookups"/> instead when the first axis's normalized
    /// coordinate is from <paramref name="min"/> to <paramref name="max"/> (FeatureVariations).
    /// </summary>
    public void AddFeatureVariation(double min, double max, int feature, params int[] lookups) => _variations.Add((min, max, feature, lookups));

    public byte[] Build(params string[] scripts)
    {
        if (scripts.Length == 0) scripts = new[] { "DFLT" };
        bool variations = _variations.Count > 0;
        int header = variations ? 14 : 10;

        var scriptList = new BigEndianWriter();
        scriptList.U16((ushort)scripts.Length);
        int scriptStart = 2 + 6 * scripts.Length;
        int scriptSize = 4 + 6 + 2 * _features.Count; // Script (default LangSys at 4, no others) + LangSys
        for (int i = 0; i < scripts.Length; i++) scriptList.Bytes(Encoding.ASCII.GetBytes(scripts[i])).U16((ushort)(scriptStart + i * scriptSize));
        foreach (var _ in scripts)
        {
            scriptList.U16(4).U16(0).U16(0).U16(0xFFFF).U16((ushort)_features.Count);
            for (int f = 0; f < _features.Count; f++) scriptList.U16((ushort)f);
        }

        var featureList = new BigEndianWriter();
        featureList.U16((ushort)_features.Count);
        int featureStart = 2 + 6 * _features.Count;
        int offset = featureStart;
        foreach (var (tag, lookups) in _features)
        {
            featureList.Bytes(Encoding.ASCII.GetBytes(tag)).U16((ushort)offset);
            offset += 4 + 2 * lookups.Length;
        }
        foreach (var (_, lookups) in _features) featureList.Bytes(Feature(lookups));

        var lookupList = new BigEndianWriter();
        lookupList.U16((ushort)_lookups.Count);
        var lookupTables = _lookups.Select(l =>
        {
            int head = 6 + 2 * l.Subtables.Length + (l.MarkSet is null ? 0 : 2);
            var w = new BigEndianWriter().U16((ushort)l.Type).U16((ushort)l.Flags).U16((ushort)l.Subtables.Length);
            int at = head;
            foreach (var s in l.Subtables) { w.U16((ushort)at); at += s.Length; }
            if (l.MarkSet is int set) w.U16((ushort)set);
            foreach (var s in l.Subtables) w.Bytes(s);
            return w.ToArray();
        }).ToList();
        offset = 2 + 2 * _lookups.Count;
        foreach (var table in lookupTables) { lookupList.U16((ushort)offset); offset += table.Length; }
        foreach (var table in lookupTables) lookupList.Bytes(table);

        var scriptBytes = scriptList.ToArray();
        var featureBytes = featureList.ToArray();
        var lookupBytes = lookupList.ToArray();
        int scriptOffset = header, featureOffset = scriptOffset + scriptBytes.Length, lookupOffset = featureOffset + featureBytes.Length;
        int variationOffset = lookupOffset + lookupBytes.Length;

        var w2 = new BigEndianWriter().U16(1).U16((ushort)(variations ? 1 : 0)).U16((ushort)scriptOffset).U16((ushort)featureOffset).U16((ushort)lookupOffset);
        if (variations) w2.U32((uint)variationOffset);
        w2.Bytes(scriptBytes).Bytes(featureBytes).Bytes(lookupBytes);
        if (variations) w2.Bytes(FeatureVariations());
        return w2.ToArray();
    }

    // GDEF version 1.2: glyph classes, mark attachment classes, and mark filtering sets.
    public static byte[] Gdef(IReadOnlyDictionary<int, int> glyphClasses, IReadOnlyDictionary<int, int>? markAttachClasses = null, IReadOnlyList<int[]>? markSets = null)
    {
        var classes = ClassDef(glyphClasses);
        var attach = markAttachClasses is null ? Array.Empty<byte>() : ClassDef(markAttachClasses);
        var sets = Array.Empty<byte>();
        if (markSets is not null)
        {
            var w = new BigEndianWriter().U16(1).U16((ushort)markSets.Count);
            int at = 4 + 4 * markSets.Count;
            var coverages = markSets.Select(s => Coverage(s)).ToList();
            foreach (var c in coverages) { w.U32((uint)at); at += c.Length; }
            foreach (var c in coverages) w.Bytes(c);
            sets = w.ToArray();
        }
        int classesAt = 14, attachAt = classesAt + classes.Length, setsAt = attachAt + attach.Length;
        return new BigEndianWriter().U16(1).U16(2).U16((ushort)classesAt).U16(0).U16(0)
            .U16((ushort)(attach.Length > 0 ? attachAt : 0)).U16((ushort)(sets.Length > 0 ? setsAt : 0))
            .Bytes(classes).Bytes(attach).Bytes(sets).ToArray();
    }

    public static byte[] Coverage(params int[] glyphs)
    {
        var sorted = glyphs.Distinct().OrderBy(g => g).ToArray();
        var w = new BigEndianWriter().U16(1).U16((ushort)sorted.Length);
        foreach (int g in sorted) w.U16((ushort)g);
        return w.ToArray();
    }

    // Format 2 coverage: one range per run of consecutive glyphs.
    public static byte[] CoverageRanges(params int[] glyphs)
    {
        var sorted = glyphs.Distinct().OrderBy(g => g).ToArray();
        var ranges = new List<(int Start, int End, int Index)>();
        for (int i = 0; i < sorted.Length; i++)
        {
            if (ranges.Count > 0 && ranges[^1].End == sorted[i] - 1) ranges[^1] = (ranges[^1].Start, sorted[i], ranges[^1].Index);
            else ranges.Add((sorted[i], sorted[i], i));
        }
        var w = new BigEndianWriter().U16(2).U16((ushort)ranges.Count);
        foreach (var (s, e, idx) in ranges) w.U16((ushort)s).U16((ushort)e).U16((ushort)idx);
        return w.ToArray();
    }

    // Format 2 class definition: one range per glyph.
    public static byte[] ClassDef(IReadOnlyDictionary<int, int> classes)
    {
        var w = new BigEndianWriter().U16(2).U16((ushort)classes.Count);
        foreach (var (glyph, cls) in classes.OrderBy(c => c.Key)) w.U16((ushort)glyph).U16((ushort)glyph).U16((ushort)cls);
        return w.ToArray();
    }

    public static byte[] SingleSubstDelta(int[] glyphs, int delta) => Compose(w => w.U16(1).U16(Here(6)).I16((short)delta), Coverage(glyphs));

    public static byte[] SingleSubst(IReadOnlyDictionary<int, int> map)
    {
        var keys = map.Keys.OrderBy(k => k).ToArray();
        return Compose(w => { w.U16(2).U16(Here(6 + 2 * keys.Length)).U16((ushort)keys.Length); foreach (int k in keys) w.U16((ushort)map[k]); }, Coverage(keys));
    }

    public static byte[] MultipleSubst(IReadOnlyDictionary<int, int[]> map)
    {
        var keys = map.Keys.OrderBy(k => k).ToArray();
        var sequences = keys.Select(k => { var s = new BigEndianWriter().U16((ushort)map[k].Length); foreach (int g in map[k]) s.U16((ushort)g); return s.ToArray(); }).ToArray();
        int head = 6 + 2 * keys.Length;
        var w = new BigEndianWriter().U16(1);
        int at = head + sequences.Sum(s => s.Length);
        w.U16((ushort)at).U16((ushort)keys.Length);
        int seqAt = head;
        foreach (var s in sequences) { w.U16((ushort)seqAt); seqAt += s.Length; }
        foreach (var s in sequences) w.Bytes(s);
        return w.Bytes(Coverage(keys)).ToArray();
    }

    /// <summary>
    /// A ligature substitution: for each first glyph, its ligatures as (the other components, the ligature glyph).
    /// </summary>
    public static byte[] LigatureSubst(IReadOnlyDictionary<int, (int[] Others, int Ligature)[]> map)
    {
        var keys = map.Keys.OrderBy(k => k).ToArray();
        var sets = keys.Select(k =>
        {
            var ligatures = map[k].Select(l => { var w = new BigEndianWriter().U16((ushort)l.Ligature).U16((ushort)(l.Others.Length + 1)); foreach (int g in l.Others) w.U16((ushort)g); return w.ToArray(); }).ToArray();
            var set = new BigEndianWriter().U16((ushort)ligatures.Length);
            int at = 2 + 2 * ligatures.Length;
            foreach (var l in ligatures) { set.U16((ushort)at); at += l.Length; }
            foreach (var l in ligatures) set.Bytes(l);
            return set.ToArray();
        }).ToArray();
        int head = 6 + 2 * keys.Length;
        var result = new BigEndianWriter().U16(1);
        result.U16((ushort)(head + sets.Sum(s => s.Length))).U16((ushort)keys.Length);
        int setAt = head;
        foreach (var s in sets) { result.U16((ushort)setAt); setAt += s.Length; }
        foreach (var s in sets) result.Bytes(s);
        return result.Bytes(Coverage(keys)).ToArray();
    }

    /// <summary>
    /// A format 3 contextual subtable (GSUB 5 / GPOS 7): one coverage per input glyph, then nested lookups.
    /// </summary>
    public static byte[] Context3(int[][] input, params (int Sequence, int Lookup)[] records)
    {
        var coverages = input.Select(g => Coverage(g)).ToArray();
        int head = 6 + 2 * input.Length + 4 * records.Length;
        var w = new BigEndianWriter().U16(3).U16((ushort)input.Length).U16((ushort)records.Length);
        int at = head;
        foreach (var c in coverages) { w.U16((ushort)at); at += c.Length; }
        foreach (var (s, l) in records) w.U16((ushort)s).U16((ushort)l);
        foreach (var c in coverages) w.Bytes(c);
        return w.ToArray();
    }

    /// <summary>
    /// A format 3 chained contextual subtable (GSUB 6 / GPOS 8). Backtrack coverages are listed nearest first.
    /// </summary>
    public static byte[] Chain3(int[][] backtrack, int[][] input, int[][] lookahead, params (int Sequence, int Lookup)[] records)
    {
        var all = backtrack.Concat(input).Concat(lookahead).Select(g => Coverage(g)).ToArray();
        int head = 2 + 2 + 2 * backtrack.Length + 2 + 2 * input.Length + 2 + 2 * lookahead.Length + 2 + 4 * records.Length;
        var w = new BigEndianWriter().U16(3);
        int at = head, k = 0;
        foreach (var group in new[] { backtrack, input, lookahead })
        {
            w.U16((ushort)group.Length);
            foreach (var _ in group) { w.U16((ushort)at); at += all[k++].Length; }
        }
        w.U16((ushort)records.Length);
        foreach (var (s, l) in records) w.U16((ushort)s).U16((ushort)l);
        foreach (var c in all) w.Bytes(c);
        return w.ToArray();
    }

    /// <summary>
    /// A format 2 chained contextual subtable with one rule: classes for backtrack, input, and lookahead.
    /// </summary>
    public static byte[] Chain2(int[] coverage, IReadOnlyDictionary<int, int> backtrackClasses, IReadOnlyDictionary<int, int> inputClasses, IReadOnlyDictionary<int, int> lookaheadClasses,
        int firstClass, int[] backtrack, int[] inputRest, int[] lookahead, params (int Sequence, int Lookup)[] records)
    {
        var cov = Coverage(coverage);
        var bd = ClassDef(backtrackClasses);
        var id = ClassDef(inputClasses);
        var ld = ClassDef(lookaheadClasses);
        int classCount = firstClass + 1;
        var rule = new BigEndianWriter().U16((ushort)backtrack.Length);
        foreach (int c in backtrack) rule.U16((ushort)c);
        rule.U16((ushort)(inputRest.Length + 1));
        foreach (int c in inputRest) rule.U16((ushort)c);
        rule.U16((ushort)lookahead.Length);
        foreach (int c in lookahead) rule.U16((ushort)c);
        rule.U16((ushort)records.Length);
        foreach (var (s, l) in records) rule.U16((ushort)s).U16((ushort)l);
        var ruleBytes = rule.ToArray();
        var set = new BigEndianWriter().U16(1).U16(4).Bytes(ruleBytes).ToArray();

        int head = 12 + 2 * classCount;
        int covAt = head, bdAt = covAt + cov.Length, idAt = bdAt + bd.Length, ldAt = idAt + id.Length, setAt = ldAt + ld.Length;
        var w = new BigEndianWriter().U16(2).U16((ushort)covAt).U16((ushort)bdAt).U16((ushort)idAt).U16((ushort)ldAt).U16((ushort)classCount);
        for (int c = 0; c < classCount; c++) w.U16((ushort)(c == firstClass ? setAt : 0));
        return w.Bytes(cov).Bytes(bd).Bytes(id).Bytes(ld).Bytes(set).ToArray();
    }

    /// <summary>
    /// A format 1 contextual subtable (GSUB 5 / GPOS 7) with one rule starting at <paramref name="first"/>.
    /// </summary>
    public static byte[] Context1(int first, int[] rest, params (int Sequence, int Lookup)[] records)
    {
        var rule = new BigEndianWriter().U16((ushort)(rest.Length + 1)).U16((ushort)records.Length);
        foreach (int g in rest) rule.U16((ushort)g);
        foreach (var (s, l) in records) rule.U16((ushort)s).U16((ushort)l);
        var set = new BigEndianWriter().U16(1).U16(4).Bytes(rule.ToArray()).ToArray();
        var cov = Coverage(first);
        return new BigEndianWriter().U16(1).U16(8).U16(1).U16((ushort)(8 + cov.Length)).Bytes(cov).Bytes(set).ToArray();
    }

    public static byte[] Extension(int type, byte[] subtable) => new BigEndianWriter().U16(1).U16((ushort)type).U32(8).Bytes(subtable).ToArray();

    public static byte[] ReverseChain(int[] coverage, int[][] backtrack, int[][] lookahead, int[] substitutes)
    {
        var cov = Coverage(coverage);
        var back = backtrack.Select(g => Coverage(g)).ToArray();
        var ahead = lookahead.Select(g => Coverage(g)).ToArray();
        int head = 4 + 2 + 2 * back.Length + 2 + 2 * ahead.Length + 2 + 2 * substitutes.Length;
        var w = new BigEndianWriter().U16(1);
        int at = head;
        w.U16((ushort)at); at += cov.Length;
        w.U16((ushort)back.Length);
        foreach (var b in back) { w.U16((ushort)at); at += b.Length; }
        w.U16((ushort)ahead.Length);
        foreach (var a in ahead) { w.U16((ushort)at); at += a.Length; }
        w.U16((ushort)substitutes.Length);
        foreach (int s in substitutes) w.U16((ushort)s);
        w.Bytes(cov);
        foreach (var b in back) w.Bytes(b);
        foreach (var a in ahead) w.Bytes(a);
        return w.ToArray();
    }

    /// <summary>
    /// A format 1 single positioning subtable adding x advance and x/y placement to every covered glyph.
    /// </summary>
    public static byte[] SinglePos(int[] glyphs, short xPlacement, short yPlacement, short xAdvance) =>
        Compose(w => w.U16(1).U16(Here(12)).U16(0x7).I16(xPlacement).I16(yPlacement).I16(xAdvance), Coverage(glyphs));

    /// <summary>
    /// A format 1 pair positioning subtable: x advance adjustments of the first glyph for (first, second) pairs.
    /// </summary>
    public static byte[] PairPos1(IReadOnlyDictionary<(int First, int Second), short> pairs)
    {
        var firsts = pairs.Keys.Select(k => k.First).Distinct().OrderBy(g => g).ToArray();
        var sets = firsts.Select(f =>
        {
            var seconds = pairs.Where(p => p.Key.First == f).OrderBy(p => p.Key.Second).ToArray();
            var w = new BigEndianWriter().U16((ushort)seconds.Length);
            foreach (var s in seconds) w.U16((ushort)s.Key.Second).I16(s.Value);
            return w.ToArray();
        }).ToArray();
        int head = 10 + 2 * firsts.Length;
        var result = new BigEndianWriter().U16(1).U16((ushort)(head + sets.Sum(s => s.Length))).U16(0x4).U16(0).U16((ushort)firsts.Length);
        int at = head;
        foreach (var s in sets) { result.U16((ushort)at); at += s.Length; }
        foreach (var s in sets) result.Bytes(s);
        return result.Bytes(Coverage(firsts)).ToArray();
    }

    /// <summary>
    /// A format 2 pair positioning subtable: classes for the first and second glyph and an x advance per class pair.
    /// </summary>
    public static byte[] PairPos2(IReadOnlyDictionary<int, int> firstClasses, IReadOnlyDictionary<int, int> secondClasses, short[,] values)
    {
        int class1Count = values.GetLength(0), class2Count = values.GetLength(1);
        var cov = Coverage(firstClasses.Keys.ToArray());
        var cd1 = ClassDef(firstClasses);
        var cd2 = ClassDef(secondClasses);
        int head = 16 + 2 * class1Count * class2Count;
        var w = new BigEndianWriter().U16(2).U16((ushort)head).U16(0x4).U16(0).U16((ushort)(head + cov.Length)).U16((ushort)(head + cov.Length + cd1.Length)).U16((ushort)class1Count).U16((ushort)class2Count);
        for (int a = 0; a < class1Count; a++) for (int b = 0; b < class2Count; b++) w.I16(values[a, b]);
        return w.Bytes(cov).Bytes(cd1).Bytes(cd2).ToArray();
    }

    /// <summary>
    /// A mark attachment subtable (GPOS 4 mark-to-base or 6 mark-to-mark): marks with class and anchor, targets with
    /// one anchor per class.
    /// </summary>
    public static byte[] MarkAttach((int Glyph, int Class, short X, short Y)[] marks, (int Glyph, (short X, short Y)[] Anchors)[] targets)
    {
        int classCount = marks.Max(m => m.Class) + 1;
        var orderedMarks = marks.OrderBy(m => m.Glyph).ToArray();
        var orderedTargets = targets.OrderBy(t => t.Glyph).ToArray();
        var markArray = new BigEndianWriter().U16((ushort)orderedMarks.Length);
        int at = 2 + 4 * orderedMarks.Length;
        foreach (var m in orderedMarks) { markArray.U16((ushort)m.Class).U16((ushort)at); at += 6; }
        foreach (var m in orderedMarks) markArray.U16(1).I16(m.X).I16(m.Y);

        var targetArray = new BigEndianWriter().U16((ushort)orderedTargets.Length);
        at = 2 + 2 * classCount * orderedTargets.Length;
        foreach (var t in orderedTargets) for (int c = 0; c < classCount; c++) { targetArray.U16((ushort)at); at += 6; }
        foreach (var t in orderedTargets) foreach (var (x, y) in t.Anchors) targetArray.U16(1).I16(x).I16(y);

        var markCov = Coverage(orderedMarks.Select(m => m.Glyph).ToArray());
        var targetCov = Coverage(orderedTargets.Select(t => t.Glyph).ToArray());
        var ma = markArray.ToArray();
        var ta = targetArray.ToArray();
        int markCovAt = 12, targetCovAt = markCovAt + markCov.Length, maAt = targetCovAt + targetCov.Length, taAt = maAt + ma.Length;
        return new BigEndianWriter().U16(1).U16((ushort)markCovAt).U16((ushort)targetCovAt).U16((ushort)classCount).U16((ushort)maAt).U16((ushort)taAt)
            .Bytes(markCov).Bytes(targetCov).Bytes(ma).Bytes(ta).ToArray();
    }

    /// <summary>
    /// A mark-to-ligature subtable (GPOS 5): one mark class; a ligature with one anchor per component.
    /// </summary>
    public static byte[] MarkToLigature(int mark, short markX, short markY, int ligature, (short X, short Y)[] componentAnchors)
    {
        var markArray = new BigEndianWriter().U16(1).U16(0).U16(6).U16(1).I16(markX).I16(markY).ToArray();
        var attach = new BigEndianWriter().U16((ushort)componentAnchors.Length);
        int at = 2 + 2 * componentAnchors.Length;
        foreach (var _ in componentAnchors) { attach.U16((ushort)at); at += 6; }
        foreach (var (x, y) in componentAnchors) attach.U16(1).I16(x).I16(y);
        var attachBytes = attach.ToArray();
        var ligatureArray = new BigEndianWriter().U16(1).U16(4).Bytes(attachBytes).ToArray();
        var markCov = Coverage(mark);
        var ligCov = Coverage(ligature);
        int markCovAt = 12, ligCovAt = markCovAt + markCov.Length, maAt = ligCovAt + ligCov.Length, laAt = maAt + markArray.Length;
        return new BigEndianWriter().U16(1).U16((ushort)markCovAt).U16((ushort)ligCovAt).U16(1).U16((ushort)maAt).U16((ushort)laAt)
            .Bytes(markCov).Bytes(ligCov).Bytes(markArray).Bytes(ligatureArray).ToArray();
    }

    #endregion

    #region Private Methods

    // Writes a subtable whose head is produced by writeHead (with Here(n) giving the offset just past an n-byte head)
    // and whose coverage table follows the head.
    private static byte[] Compose(Action<BigEndianWriter> writeHead, byte[] coverage)
    {
        var w = new BigEndianWriter();
        writeHead(w);
        return w.Bytes(coverage).ToArray();
    }

    private static ushort Here(int headLength) => (ushort)headLength;

    private static byte[] Feature(int[] lookups)
    {
        var w = new BigEndianWriter().U16(0).U16((ushort)lookups.Length);
        foreach (int l in lookups) w.U16((ushort)l);
        return w.ToArray();
    }

    // FeatureVariations version 1.0 with one condition on axis 0 per record.
    private byte[] FeatureVariations()
    {
        var records = _variations.Select(v =>
        {
            var condition = new BigEndianWriter().U16(1).U16(0).I16((short)Math.Round(v.Min * 16384)).I16((short)Math.Round(v.Max * 16384)).ToArray();
            var conditionSet = new BigEndianWriter().U16(1).U32(6).Bytes(condition).ToArray();
            var feature = Feature(v.Lookups);
            var substitution = new BigEndianWriter().U16(1).U16(0).U16(1).U16((ushort)v.Feature).U32(12).Bytes(feature).ToArray();
            return (conditionSet, substitution);
        }).ToList();
        var w = new BigEndianWriter().U16(1).U16(0).U32((uint)records.Count);
        int at = 8 + 8 * records.Count;
        foreach (var (c, s) in records) { w.U32((uint)at); at += c.Length; w.U32((uint)at); at += s.Length; }
        foreach (var (c, s) in records) w.Bytes(c).Bytes(s);
        return w.ToArray();
    }

    #endregion

    #endregion
}
