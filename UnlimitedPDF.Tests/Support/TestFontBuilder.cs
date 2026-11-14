using System.Buffers.Binary;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Builds small, project-authored TrueType fonts in code for tests (PR-011, D-014). Glyph 0 is always <c>.notdef</c>.
/// The font has the tables a PDF producer reads: <c>cmap</c> (formats 4 and 12), <c>glyf</c>/<c>loca</c> (long
/// offsets), <c>head</c>, <c>hhea</c>, <c>hmtx</c>, <c>maxp</c>, <c>name</c>, <c>OS/2</c>, and <c>post</c>. Later tests
/// add their own tables (for example layout tables) with <see cref="AddTable"/>.
/// </summary>
internal sealed class TestFontBuilder
{
    #region Fields and Constants

    private const uint ChecksumMagic = 0xB1B0AFBA;

    private readonly List<Glyph> _glyphs = new();
    private readonly SortedDictionary<int, int> _characterMap = new();
    private readonly Dictionary<string, byte[]> _extraTables = new(StringComparer.Ordinal);

    #endregion

    #region Constructors and Finalizers

    public TestFontBuilder(string familyName = "Test Sans", ushort unitsPerEm = 1000)
    {
        FamilyName = familyName;
        UnitsPerEm = unitsPerEm;
        AddGlyph(500, Box(50, 0, 450, 700)); // .notdef: a box
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns one rectangular contour, listed clockwise from the bottom-left corner.
    /// </summary>
    public static (short X, short Y)[] Box(short left, short bottom, short right, short top) =>
        new[] { (left, bottom), (left, top), (right, top), (right, bottom) };

    /// <summary>
    /// Adds a simple glyph whose points are all on the curve and returns its glyph ID.
    /// </summary>
    public int AddGlyph(ushort advance, params (short X, short Y)[][] contours)
    {
        _glyphs.Add(new Glyph(advance, contours, Array.Empty<(int, short, short)>()));
        return _glyphs.Count - 1;
    }

    /// <summary>
    /// Adds a composite glyph made of other glyphs moved by an offset, and returns its glyph ID.
    /// </summary>
    public int AddCompositeGlyph(ushort advance, params (int Glyph, short Dx, short Dy)[] components)
    {
        if (components.Length == 0) throw new ArgumentException("A composite glyph needs a component.", nameof(components));
        _glyphs.Add(new Glyph(advance, Array.Empty<(short, short)[]>(), components));
        return _glyphs.Count - 1;
    }

    /// <summary>
    /// Adds a table the builder does not write itself, or replaces one it does.
    /// </summary>
    public TestFontBuilder AddTable(string tag, byte[] data)
    {
        if (tag.Length != 4) throw new ArgumentException("A table tag has four characters.", nameof(tag));
        _extraTables[tag] = data;
        return this;
    }

    /// <summary>
    /// Maps a Unicode code point to a glyph.
    /// </summary>
    public TestFontBuilder Map(int codePoint, int glyph)
    {
        _characterMap[codePoint] = glyph;
        return this;
    }

    /// <summary>
    /// Adds one box glyph for each character of <paramref name="text"/> that has no glyph yet and maps it.
    /// </summary>
    public TestFontBuilder MapCharacters(string text, ushort advance = 600)
    {
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            int codePoint = char.ConvertToUtf32(text, i);
            if (!_characterMap.ContainsKey(codePoint)) Map(codePoint, AddGlyph(advance, Box(40, 0, (short)(advance - 40), 700)));
        }
        return this;
    }

    /// <summary>
    /// Returns the font file bytes.
    /// </summary>
    public byte[] Build()
    {
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var (glyf, loca, bounds) = BuildGlyphs();
        tables["cmap"] = BuildCmap();
        tables["glyf"] = glyf;
        tables["head"] = BuildHead(bounds);
        tables["hhea"] = BuildHhea();
        tables["hmtx"] = BuildHmtx();
        tables["loca"] = loca;
        tables["maxp"] = BuildMaxp();
        tables["name"] = BuildName();
        tables["OS/2"] = BuildOs2();
        tables["post"] = BuildPost();
        foreach (var (tag, data) in _extraTables) tables[tag] = data;
        return Assemble(tables, 0x00010000);
    }

    /// <summary>
    /// Returns the checksum of a table or a whole font: the sum of its big-endian 32-bit words, zero-padded.
    /// </summary>
    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (int j = 0; j < 4; j++) word = (word << 8) | (i + j < data.Length ? data[i + j] : 0u);
            sum += word;
        }
        return sum;
    }

    /// <summary>
    /// Writes an sfnt file from its tables, with the table directory sorted by tag, every table 4-byte aligned, and
    /// <c>head.checkSumAdjustment</c> set so that the whole file sums to the magic number.
    /// </summary>
    public static byte[] Assemble(IReadOnlyDictionary<string, byte[]> tables, uint sfntVersion)
    {
        var tags = tables.Keys.OrderBy(t => t, StringComparer.Ordinal).ToList();
        int count = tags.Count;
        int entrySelector = (int)Math.Floor(Math.Log2(count));
        int searchRange = (1 << entrySelector) * 16;
        int offset = 12 + 16 * count;

        using var output = new MemoryStream();
        var header = new byte[12 + 16 * count];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0), sfntVersion);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10), (ushort)(count * 16 - searchRange));

        int headOffset = -1;
        for (int i = 0; i < count; i++)
        {
            var data = tables[tags[i]];
            var record = header.AsSpan(12 + 16 * i);
            Encoding.ASCII.GetBytes(tags[i]).CopyTo(record);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(4), Checksum(tags[i] == "head" ? WithoutAdjustment(data) : data));
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(8), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(12), (uint)data.Length);
            if (tags[i] == "head") headOffset = offset;
            offset += (data.Length + 3) & ~3;
        }

        output.Write(header);
        foreach (var tag in tags)
        {
            var data = tag == "head" ? WithoutAdjustment(tables[tag]) : tables[tag];
            output.Write(data);
            output.Write(new byte[((data.Length + 3) & ~3) - data.Length]);
        }

        var font = output.ToArray();
        if (headOffset >= 0)
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headOffset + 8), unchecked(ChecksumMagic - Checksum(font)));
        return font;
    }

    #endregion

    #region Private Methods

    private static byte[] WithoutAdjustment(byte[] head)
    {
        var copy = (byte[])head.Clone();
        if (copy.Length >= 12) copy.AsSpan(8, 4).Clear();
        return copy;
    }

    private (byte[] Glyf, byte[] Loca, (short XMin, short YMin, short XMax, short YMax) Bounds) BuildGlyphs()
    {
        var glyf = new BigEndianWriter();
        var loca = new BigEndianWriter();
        short xMin = short.MaxValue, yMin = short.MaxValue, xMax = short.MinValue, yMax = short.MinValue;

        foreach (var glyph in _glyphs)
        {
            loca.U32((uint)glyf.Length);
            var points = glyph.Contours.SelectMany(c => c).ToList();
            if (glyph.Components.Count > 0)
            {
                var b = CompositeBounds(glyph);
                glyf.I16(-1).I16(b.XMin).I16(b.YMin).I16(b.XMax).I16(b.YMax);
                for (int i = 0; i < glyph.Components.Count; i++)
                {
                    var (component, dx, dy) = glyph.Components[i];
                    // ARG_1_AND_2_ARE_WORDS | ARGS_ARE_XY_VALUES, plus MORE_COMPONENTS on all but the last.
                    ushort flags = (ushort)(0x0001 | 0x0002 | (i < glyph.Components.Count - 1 ? 0x0020 : 0));
                    glyf.U16(flags).U16((ushort)component).I16(dx).I16(dy);
                }
                Extend(ref xMin, ref yMin, ref xMax, ref yMax, b);
            }
            else if (points.Count > 0)
            {
                var b = PointBounds(points);
                glyf.I16((short)glyph.Contours.Length).I16(b.XMin).I16(b.YMin).I16(b.XMax).I16(b.YMax);
                int end = -1;
                foreach (var contour in glyph.Contours) glyf.U16((ushort)(end += contour.Length));
                glyf.U16(0); // no instructions
                foreach (var _ in points) glyf.U8(0x01); // on curve, 16-bit coordinates
                short last = 0;
                foreach (var p in points) { glyf.I16((short)(p.X - last)); last = p.X; }
                last = 0;
                foreach (var p in points) { glyf.I16((short)(p.Y - last)); last = p.Y; }
                Extend(ref xMin, ref yMin, ref xMax, ref yMax, b);
            }
            glyf.Align(4);
        }
        loca.U32((uint)glyf.Length);
        if (xMin > xMax) (xMin, yMin, xMax, yMax) = (0, 0, 0, 0);
        return (glyf.ToArray(), loca.ToArray(), (xMin, yMin, xMax, yMax));
    }

    private (short XMin, short YMin, short XMax, short YMax) CompositeBounds(Glyph glyph)
    {
        short xMin = short.MaxValue, yMin = short.MaxValue, xMax = short.MinValue, yMax = short.MinValue;
        foreach (var (component, dx, dy) in glyph.Components)
        {
            var points = _glyphs[component].Contours.SelectMany(c => c).Select(p => ((short)(p.X + dx), (short)(p.Y + dy))).ToList();
            if (points.Count > 0) Extend(ref xMin, ref yMin, ref xMax, ref yMax, PointBounds(points));
        }
        return xMin > xMax ? ((short)0, (short)0, (short)0, (short)0) : (xMin, yMin, xMax, yMax);
    }

    private static (short XMin, short YMin, short XMax, short YMax) PointBounds(IReadOnlyCollection<(short X, short Y)> points) =>
        (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));

    private static void Extend(ref short xMin, ref short yMin, ref short xMax, ref short yMax, (short XMin, short YMin, short XMax, short YMax) b)
    {
        xMin = Math.Min(xMin, b.XMin);
        yMin = Math.Min(yMin, b.YMin);
        xMax = Math.Max(xMax, b.XMax);
        yMax = Math.Max(yMax, b.YMax);
    }

    private byte[] BuildCmap()
    {
        var bmp = _characterMap.Where(m => m.Key <= 0xFFFF).ToList();
        var format4 = new BigEndianWriter();
        int segments = bmp.Count + 1;
        int entrySelector = (int)Math.Floor(Math.Log2(segments));
        int searchRange = 2 * (1 << entrySelector);
        format4.U16(4).U16((ushort)(16 + 8 * segments)).U16(0).U16((ushort)(2 * segments)).U16((ushort)searchRange)
            .U16((ushort)entrySelector).U16((ushort)(2 * segments - searchRange));
        foreach (var m in bmp) format4.U16((ushort)m.Key);
        format4.U16(0xFFFF).U16(0); // last end code, reserved pad
        foreach (var m in bmp) format4.U16((ushort)m.Key);
        format4.U16(0xFFFF);
        foreach (var m in bmp) format4.U16(unchecked((ushort)(m.Value - m.Key)));
        format4.U16(1);
        for (int i = 0; i < segments; i++) format4.U16(0);

        var format12 = new BigEndianWriter();
        format12.U16(12).U16(0).U32((uint)(16 + 12 * _characterMap.Count)).U32(0).U32((uint)_characterMap.Count);
        foreach (var m in _characterMap) format12.U32((uint)m.Key).U32((uint)m.Key).U32((uint)m.Value);

        var cmap = new BigEndianWriter();
        uint first = 4 + 2 * 8;
        cmap.U16(0).U16(2).U16(3).U16(1).U32(first).U16(3).U16(10).U32(first + (uint)format4.Length);
        cmap.Bytes(format4.ToArray()).Bytes(format12.ToArray());
        return cmap.ToArray();
    }

    private byte[] BuildHead((short XMin, short YMin, short XMax, short YMax) bounds)
    {
        var w = new BigEndianWriter();
        ushort macStyle = (ushort)((Weight >= 700 ? 0x01 : 0) | (Italic ? 0x02 : 0));
        w.U32(0x00010000).U32(0x00010000).U32(0).U32(0x5F0F3CF5).U16(0x000B).U16(UnitsPerEm)
            .U32(0).U32(0).U32(0).U32(0) // created, modified
            .I16(bounds.XMin).I16(bounds.YMin).I16(bounds.XMax).I16(bounds.YMax)
            .U16(macStyle).U16(8).I16(2).I16(1).I16(0); // lowestRecPPEM, direction hint, long loca, glyph format
        return w.ToArray();
    }

    private byte[] BuildHhea()
    {
        var w = new BigEndianWriter();
        w.U32(0x00010000).I16(Ascender).I16(Descender).I16(0).U16(_glyphs.Max(g => g.Advance))
            .I16(0).I16(0).I16(0).I16(1).I16(0).I16(0).I16(0).I16(0).I16(0).I16(0).I16(0)
            .U16((ushort)_glyphs.Count);
        return w.ToArray();
    }

    private byte[] BuildHmtx()
    {
        var w = new BigEndianWriter();
        foreach (var glyph in _glyphs)
        {
            short lsb = glyph.Contours.Length > 0 ? glyph.Contours.SelectMany(c => c).Min(p => p.X) : (short)0;
            w.U16(glyph.Advance).I16(lsb);
        }
        return w.ToArray();
    }

    private byte[] BuildMaxp()
    {
        var w = new BigEndianWriter();
        ushort maxPoints = (ushort)_glyphs.Max(g => g.Contours.Sum(c => c.Length));
        ushort maxContours = (ushort)_glyphs.Max(g => g.Contours.Length);
        ushort maxComponents = (ushort)_glyphs.Max(g => g.Components.Count);
        w.U32(0x00010000).U16((ushort)_glyphs.Count).U16(maxPoints).U16(maxContours).U16(maxPoints).U16(maxContours)
            .U16(2).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(maxComponents).U16((ushort)(maxComponents > 0 ? 1 : 0));
        return w.ToArray();
    }

    private byte[] BuildName()
    {
        string subfamily = (Weight >= 700, Italic) switch { (true, true) => "Bold Italic", (true, false) => "Bold", (false, true) => "Italic", _ => "Regular" };
        var records = new (ushort Id, string Value)[]
        {
            (1, FamilyName), (2, subfamily), (4, FamilyName + " " + subfamily), (6, PostScriptName ?? (FamilyName + "-" + subfamily).Replace(" ", string.Empty))
        };
        var strings = new BigEndianWriter();
        var w = new BigEndianWriter();
        w.U16(0).U16((ushort)records.Length).U16((ushort)(6 + 12 * records.Length));
        foreach (var (id, value) in records)
        {
            var bytes = Encoding.BigEndianUnicode.GetBytes(value);
            w.U16(3).U16(1).U16(0x0409).U16(id).U16((ushort)bytes.Length).U16((ushort)strings.Length);
            strings.Bytes(bytes);
        }
        return w.Bytes(strings.ToArray()).ToArray();
    }

    private byte[] BuildOs2()
    {
        var w = new BigEndianWriter();
        ushort fsSelection = (ushort)((Italic ? 0x01 : 0) | (Weight >= 700 ? 0x20 : 0) | (!Italic && Weight < 700 ? 0x40 : 0));
        int first = _characterMap.Count > 0 ? Math.Min(_characterMap.Keys.First(), 0xFFFF) : 0x20;
        int last = _characterMap.Count > 0 ? Math.Min(_characterMap.Keys.Last(), 0xFFFF) : 0x20;
        w.U16(4).I16(500).U16(Weight).U16(5).U16(FsType)
            .I16(650).I16(600).I16(0).I16(75).I16(650).I16(600).I16(0).I16(350).I16(50).I16(250) // sub- and superscripts, strikeout
            .I16(0) // family class
            .Bytes(new byte[10]) // PANOSE
            .U32(0).U32(0).U32(0).U32(0) // Unicode ranges
            .Bytes(Encoding.ASCII.GetBytes("TEST"))
            .U16(fsSelection).U16((ushort)first).U16((ushort)last)
            .I16(Ascender).I16(Descender).I16(0).U16((ushort)Ascender).U16((ushort)-Descender)
            .U32(1).U32(0) // code page ranges
            .I16(500).I16(CapHeight).U16(0).U16(0x20).U16(1);
        return w.ToArray();
    }

    private byte[] BuildPost()
    {
        var w = new BigEndianWriter();
        w.U32(0x00030000).U32(Italic ? unchecked((uint)(-12 << 16)) : 0).I16(-100).I16(50).U32(0).U32(0).U32(0).U32(0).U32(0);
        return w.ToArray();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public short Ascender { get; set; } = 800;

    public short CapHeight { get; set; } = 700;

    public short Descender { get; set; } = -200;

    public string FamilyName { get; }

    /// <summary>
    /// Gets or sets the <c>OS/2</c> embedding permissions (0 = installable).
    /// </summary>
    public ushort FsType { get; set; }

    public int GlyphCount => _glyphs.Count;

    public bool Italic { get; set; }

    /// <summary>
    /// Gets or sets the PostScript name (name ID 6); by default the family and style without spaces.
    /// </summary>
    public string? PostScriptName { get; set; }

    public ushort UnitsPerEm { get; }

    public ushort Weight { get; set; } = 400;

    #endregion

    #region Nested Types

    private sealed record Glyph(ushort Advance, (short X, short Y)[][] Contours, IReadOnlyList<(int Glyph, short Dx, short Dy)> Components);

    #endregion
}

/// <summary>
/// Appends big-endian values, as font tables store them.
/// </summary>
internal sealed class BigEndianWriter
{
    #region Fields and Constants

    private readonly MemoryStream _stream = new();

    #endregion

    #region Methods

    #region Public Methods

    public BigEndianWriter Align(int boundary)
    {
        while (_stream.Length % boundary != 0) _stream.WriteByte(0);
        return this;
    }

    public BigEndianWriter Bytes(byte[] data)
    {
        _stream.Write(data);
        return this;
    }

    public BigEndianWriter I16(short value) => U16(unchecked((ushort)value));

    public byte[] ToArray() => _stream.ToArray();

    public BigEndianWriter U16(ushort value)
    {
        _stream.WriteByte((byte)(value >> 8));
        _stream.WriteByte((byte)value);
        return this;
    }

    public BigEndianWriter U32(uint value) => U16((ushort)(value >> 16)).U16((ushort)value);

    public BigEndianWriter U8(byte value)
    {
        _stream.WriteByte(value);
        return this;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public int Length => (int)_stream.Length;

    #endregion
}
