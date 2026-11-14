namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Variable-font support for the test font builder (D-013, D-014): one weight axis (<c>fvar</c>), an optional
/// <c>avar</c> map, glyph deltas at the axis maximum (<c>gvar</c>, or <c>blend</c> in a <c>CFF2</c> font), advance
/// deltas (<c>HVAR</c>), and metric deltas (<c>MVAR</c>). Every delta is defined at normalized weight 1 (the axis
/// maximum) and scales linearly toward the default.
/// </summary>
internal sealed partial class TestFontBuilder
{
    #region Fields and Constants

    private readonly Dictionary<int, List<(int[]? Points, (short Dx, short Dy)[] Deltas)>> _weightDeltas = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds deltas for one glyph at the axis maximum. With <paramref name="points"/> null, the deltas cover every
    /// point: the outline points (or one per component) and then the four phantom points, whose second one moves the
    /// advance width. Otherwise they cover only the listed points (a private point list), and the reader interpolates
    /// the rest.
    /// </summary>
    public TestFontBuilder AddWeightDeltas(int glyph, (short Dx, short Dy)[] deltas, int[]? points = null)
    {
        if (!_weightDeltas.TryGetValue(glyph, out var list)) _weightDeltas[glyph] = list = new();
        list.Add((points, deltas));
        return this;
    }

    #endregion

    #region Private Methods

    private void AddVariationTables(SortedDictionary<string, byte[]> tables)
    {
        if (WeightAxis is not { } axis) return;
        tables["fvar"] = new BigEndianWriter().U16(1).U16(0).U16(16).U16(2).U16(1).U16(20).U16(0).U16(8)
            .Bytes(Encoding.ASCII.GetBytes("wght")).U32(Fixed(axis.Minimum)).U32(Fixed(axis.Default)).U32(Fixed(axis.Maximum)).U16(0).U16(256)
            .ToArray();

        if (AvarMap is { } map)
        {
            var avar = new BigEndianWriter().U16(1).U16(0).U16(0).U16(1).U16((ushort)map.Length);
            foreach (var (from, to) in map) avar.I16(F2Dot14(from)).I16(F2Dot14(to));
            tables["avar"] = avar.ToArray();
        }

        if (!Cff2 && _weightDeltas.Count > 0) tables["gvar"] = BuildGvar();
        if (HvarAdvanceDeltas.Count > 0 || Cff2) tables["HVAR"] = BuildHvar();
        if (MvarDeltas.Count > 0) tables["MVAR"] = BuildMvar();
    }

    private static uint Fixed(double value) => unchecked((uint)(int)Math.Round(value * 65536));

    private static short F2Dot14(double value) => (short)Math.Round(value * 16384);

    // gvar with one tuple per delta set, each with an embedded peak tuple (wght = 1) and, when given, private points.
    private byte[] BuildGvar()
    {
        var glyphData = new List<byte[]>();
        for (int g = 0; g < _glyphs.Count; g++)
        {
            if (!_weightDeltas.TryGetValue(g, out var sets)) { glyphData.Add(Array.Empty<byte>()); continue; }
            var headers = new BigEndianWriter();
            var serialized = new BigEndianWriter();
            foreach (var (points, deltas) in sets)
            {
                var tuple = new BigEndianWriter();
                if (points is not null) tuple.Bytes(PackedPoints(points));
                tuple.Bytes(PackedDeltas(deltas.Select(d => (int)d.Dx).ToArray()));
                tuple.Bytes(PackedDeltas(deltas.Select(d => (int)d.Dy).ToArray()));
                var bytes = tuple.ToArray();
                headers.U16((ushort)bytes.Length).U16((ushort)(0x8000 | (points is not null ? 0x2000 : 0))).I16(F2Dot14(1));
                serialized.Bytes(bytes);
            }
            var data = new BigEndianWriter().U16((ushort)sets.Count).U16((ushort)(4 + headers.Length)).Bytes(headers.ToArray()).Bytes(serialized.ToArray());
            glyphData.Add(data.Align(2).ToArray());
        }

        int offsetsSize = 4 * (_glyphs.Count + 1);
        var w = new BigEndianWriter();
        w.U16(1).U16(0).U16(1).U16(0).U32((uint)(20 + offsetsSize)).U16((ushort)_glyphs.Count).U16(1).U32((uint)(20 + offsetsSize));
        uint offset = 0;
        w.U32(offset);
        foreach (var data in glyphData) w.U32(offset += (uint)data.Length);
        foreach (var data in glyphData) w.Bytes(data);
        return w.ToArray();
    }

    private static byte[] PackedPoints(int[] points)
    {
        var w = new BigEndianWriter();
        if (points.Length < 128) w.U8((byte)points.Length);
        else w.U8((byte)(0x80 | (points.Length >> 8))).U8((byte)points.Length);
        int previous = 0;
        for (int i = 0; i < points.Length; i += 128)
        {
            int run = Math.Min(128, points.Length - i);
            w.U8((byte)(0x80 | (run - 1)));
            for (int j = i; j < i + run; j++) { w.U16((ushort)(points[j] - previous)); previous = points[j]; }
        }
        return w.ToArray();
    }

    private static byte[] PackedDeltas(int[] values)
    {
        var w = new BigEndianWriter();
        for (int i = 0; i < values.Length; i += 64)
        {
            int run = Math.Min(64, values.Length - i);
            w.U8((byte)(0x40 | (run - 1)));
            for (int j = i; j < i + run; j++) w.I16((short)values[j]);
        }
        return w.ToArray();
    }

    // An item variation store with one region (wght peaking at 1) and one delta per item, as 16-bit words.
    private static byte[] ItemStore(IReadOnlyList<int> deltas)
    {
        var w = new BigEndianWriter();
        w.U16(1).U32(12).U16(1).U32(22); // format, region list at 12, one data subtable at 22
        w.U16(1).U16(1).I16(0).I16(F2Dot14(1)).I16(F2Dot14(1)); // one axis, one region: 0 to 1, peak 1
        w.U16((ushort)deltas.Count).U16(1).U16(1).U16(0);
        foreach (int d in deltas) w.I16((short)d);
        return w.ToArray();
    }

    private byte[] BuildHvar()
    {
        var deltas = Enumerable.Range(0, _glyphs.Count).Select(g => HvarAdvanceDeltas.TryGetValue(g, out int d) ? d : 0).ToList();
        return new BigEndianWriter().U16(1).U16(0).U32(20).U32(0).U32(0).U32(0).Bytes(ItemStore(deltas)).ToArray();
    }

    private byte[] BuildMvar()
    {
        var records = MvarDeltas.OrderBy(m => m.Key, StringComparer.Ordinal).ToList();
        var w = new BigEndianWriter().U16(1).U16(0).U16(0).U16(8).U16((ushort)records.Count).U16((ushort)(12 + 8 * records.Count));
        for (int i = 0; i < records.Count; i++) w.Bytes(Encoding.ASCII.GetBytes(records[i].Key)).U16(0).U16((ushort)i);
        return w.Bytes(ItemStore(records.Select(r => r.Value).ToList())).ToArray();
    }

    // A CFF2 table: header, Top DICT (CharStrings, FDArray, vstore), an empty Global Subr INDEX, the variation store,
    // one Font DICT with an empty Private DICT, and charstrings whose every move blends its delta.
    private byte[] BuildCff2()
    {
        var charStrings = new List<byte[]>();
        for (int g = 0; g < _glyphs.Count; g++) charStrings.Add(Cff2Charstring(g));

        var store = ItemStore(Array.Empty<int>());
        int topLength = 6 + 7 + 6;
        int globalSubrs = 5 + topLength;
        int vstore = globalSubrs + 4;
        int fdArray = vstore + 2 + store.Length;
        int fdArraySize = 4 + 1 + 8 + 11;
        int charStringsOffset = fdArray + fdArraySize;
        int privateOffset = 0; // an empty Private DICT

        var w = new BigEndianWriter();
        w.U8(2).U8(0).U8(5).U16((ushort)topLength);
        w.Bytes(DictInt(charStringsOffset)).U8(17).Bytes(DictInt(fdArray)).U8(12).U8(36).Bytes(DictInt(vstore)).U8(24);
        w.U32(0); // Global Subr INDEX: no entries
        w.U16((ushort)store.Length).Bytes(store);
        w.Bytes(Cff2Index(new List<byte[]> { DictInt(0).Concat(DictInt(privateOffset)).Concat(new byte[] { 18 }).ToArray() }));
        w.Bytes(Cff2Index(charStrings));
        return w.ToArray();
    }

    private byte[] Cff2Charstring(int glyph)
    {
        var g = _glyphs[glyph];
        if (g.Charstring is { } raw) return raw;
        var deltas = _weightDeltas.TryGetValue(glyph, out var sets) ? sets.Single(s => s.Points is null).Deltas : null;
        var code = new List<byte>();
        short x = 0, y = 0, dx = 0, dy = 0;
        int point = 0;
        foreach (var contour in g.Contours)
        {
            for (int i = 0; i < contour.Length; i++, point++)
            {
                var (px, py) = contour[i];
                var (ex, ey) = deltas is null ? ((short)0, (short)0) : deltas[point];
                code.AddRange(CharstringNumber(px - x));
                code.AddRange(CharstringNumber(py - y));
                code.AddRange(CharstringNumber(ex - dx));
                code.AddRange(CharstringNumber(ey - dy));
                code.AddRange(CharstringNumber(2));
                code.Add(16); // blend
                code.Add((byte)(i == 0 ? 21 : 5)); // rmoveto, then rlineto
                (x, y, dx, dy) = (px, py, ex, ey);
            }
        }
        return code.ToArray();
    }

    private static byte[] Cff2Index(IReadOnlyList<byte[]> entries)
    {
        var w = new BigEndianWriter();
        w.U32((uint)entries.Count);
        if (entries.Count == 0) return w.ToArray();
        w.U8(4);
        uint offset = 1;
        w.U32(offset);
        foreach (var entry in entries) w.U32(offset += (uint)entry.Length);
        foreach (var entry in entries) w.Bytes(entry);
        return w.ToArray();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets an <c>avar</c> segment map for the weight axis: (from, to) pairs in normalized units.
    /// </summary>
    public (double From, double To)[]? AvarMap { get; set; }

    /// <summary>
    /// Gets or sets whether the font has variable CFF2 outlines (with an <c>HVAR</c> table) instead of TrueType.
    /// </summary>
    public bool Cff2 { get; set; }

    /// <summary>
    /// Gets advance-width deltas at the axis maximum, written as <c>HVAR</c>.
    /// </summary>
    public Dictionary<int, int> HvarAdvanceDeltas { get; } = new();

    /// <summary>
    /// Gets metric deltas at the axis maximum by <c>MVAR</c> tag (for example <c>cpht</c>).
    /// </summary>
    public Dictionary<string, int> MvarDeltas { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the weight axis (<c>fvar</c>), or null for a static font.
    /// </summary>
    public (double Minimum, double Default, double Maximum)? WeightAxis { get; set; }

    #endregion
}
