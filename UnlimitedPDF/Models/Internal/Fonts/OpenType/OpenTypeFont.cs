using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

namespace UnlimitedPDF.Models.Internal.Fonts.OpenType;

/// <summary>
/// The kind of glyph outlines a font holds.
/// </summary>
internal enum FontOutlines
{
    TrueType,
    Cff,
    Cff2
}

/// <summary>
/// A parsed OpenType font (D-008, D-010): the table directory, global metrics, horizontal metrics, the character map,
/// names, <c>OS/2</c> style and embedding values, and validated TrueType glyph locations. Parsing checks every offset
/// and length against the data and caps composite glyph depth (A06), so damaged or hostile fonts fail here with
/// <see cref="PdfFontException"/>.
/// </summary>
internal sealed class OpenTypeFont
{
    #region Fields and Constants

    // Real fonts have a few dozen tables; a directory far beyond that is damaged or hostile.
    public const int MaxTables = 256;

    // Composite glyphs may nest; deeper nesting than this is treated as damaged (A06).
    public const int MaxCompositeDepth = 16;

    private readonly Dictionary<string, (int Offset, int Length)> _tables;
    private readonly ushort[] _advances;
    private readonly int[] _glyphOffsets;
    private readonly int[][] _components;

    #endregion

    #region Constructors and Finalizers

    private OpenTypeFont(FontBytes data, Dictionary<string, (int Offset, int Length)> tables)
    {
        Data = data;
        _tables = tables;

        var head = Table("head", 54);
        UnitsPerEm = data.U16(head + 18);
        if (UnitsPerEm < 16 || UnitsPerEm > 16384) throw FontBytes.Corrupt($"has an invalid unitsPerEm ({UnitsPerEm})");
        Bounds = (data.I16(head + 36), data.I16(head + 38), data.I16(head + 40), data.I16(head + 42));
        MacStyle = data.U16(head + 44);
        int locaFormat = data.I16(head + 50);

        var maxp = Table("maxp", 6);
        GlyphCount = data.U16(maxp + 4);
        if (GlyphCount == 0) throw FontBytes.Corrupt("has no glyphs");

        var hhea = Table("hhea", 36);
        Ascender = data.I16(hhea + 4);
        Descender = data.I16(hhea + 6);
        LineGap = data.I16(hhea + 8);
        int metrics = data.U16(hhea + 34);
        if (metrics == 0 || metrics > GlyphCount) throw FontBytes.Corrupt("has an invalid numberOfHMetrics");
        _advances = ReadAdvances(metrics);

        var (cmapOffset, cmapLength) = TableRange("cmap");
        CharacterMap = CharacterMap.Read(data, cmapOffset, cmapLength, GlyphCount);

        ReadNames();
        ReadOs2();
        ReadPost();

        if (_tables.ContainsKey("CFF2"))
        {
            Outlines = FontOutlines.Cff2;
            _glyphOffsets = Array.Empty<int>();
            _components = Array.Empty<int[]>();
        }
        else if (_tables.TryGetValue("CFF ", out var cff))
        {
            Outlines = FontOutlines.Cff;
            Cff = CffFont.Read(data, cff.Offset, cff.Length, GlyphCount);
            _glyphOffsets = Array.Empty<int>();
            _components = Array.Empty<int[]>();
        }
        else
        {
            Outlines = FontOutlines.TrueType;
            if (locaFormat is not (0 or 1)) throw FontBytes.Corrupt($"has an invalid indexToLocFormat ({locaFormat})");
            _glyphOffsets = ReadGlyphOffsets(locaFormat == 1);
            _components = ReadComponents();
            CheckCompositeDepth();
        }
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses font data, rejecting font collections, web fonts, and anything that is not an sfnt font.
    /// </summary>
    /// <param name="bytes">The font file bytes.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="PdfFontException">The data is not a supported font or is damaged.</exception>
    public static OpenTypeFont Parse(byte[] bytes)
    {
        var data = new FontBytes(bytes);
        if (bytes.Length < 12) throw Unsupported("The data is too short to be a font.");

        uint version = data.U32(0);
        switch (version)
        {
            case 0x00010000: // TrueType outlines
            case 0x74727565: // 'true' (TrueType outlines)
            case 0x4F54544F: // 'OTTO' (CFF outlines)
                break;
            case 0x74746366: // 'ttcf'
                throw Unsupported("The data is a font collection (.ttc), which is not supported. Load a single font file (.ttf or .otf).");
            case 0x774F4646: // 'wOFF'
            case 0x774F4632: // 'wOF2'
                throw Unsupported("The data is a web font (WOFF or WOFF2), which is not supported. Load a .ttf or .otf file.");
            default:
                throw Unsupported("The data is not a TrueType or OpenType font (.ttf or .otf).");
        }

        int count = data.U16(4);
        if (count == 0 || count > MaxTables) throw FontBytes.Corrupt($"has an invalid table count ({count})");
        data.Require(12, 16L * count, "the table directory");

        var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            int record = 12 + 16 * i;
            string tag = data.Tag(record);
            long offset = data.U32(record + 8), length = data.U32(record + 12);
            data.Require(offset, length, $"the {tag.TrimEnd()} table");
            tables[tag] = ((int)offset, (int)length);
        }

        var font = new OpenTypeFont(data, tables);
        if (version == 0x4F54544F && font.Outlines == FontOutlines.TrueType) throw FontBytes.Corrupt("is marked as CFF but has no CFF table");
        return font;
    }

    /// <summary>
    /// Returns the advance width of a glyph, in font units.
    /// </summary>
    /// <param name="glyph">The glyph ID.</param>
    /// <returns>The advance width.</returns>
    public int Advance(int glyph) => _advances[glyph < _advances.Length ? glyph : _advances.Length - 1];

    /// <summary>
    /// Returns the glyph IDs a composite TrueType glyph is built from (empty for a simple glyph).
    /// </summary>
    /// <param name="glyph">The glyph ID.</param>
    /// <returns>The component glyph IDs.</returns>
    public IReadOnlyList<int> Components(int glyph) => Outlines == FontOutlines.TrueType ? _components[glyph] : Array.Empty<int>();

    /// <summary>
    /// Returns where a TrueType glyph's data lies inside the <c>glyf</c> table, relative to the table's start.
    /// </summary>
    /// <param name="glyph">The glyph ID.</param>
    /// <returns>The offset and length; length 0 for an empty glyph.</returns>
    public (int Offset, int Length) GlyphRange(int glyph) => (_glyphOffsets[glyph], _glyphOffsets[glyph + 1] - _glyphOffsets[glyph]);

    /// <summary>
    /// Returns whether the font has a table.
    /// </summary>
    /// <param name="tag">The four-character tag.</param>
    /// <returns>True when the table exists.</returns>
    public bool HasTable(string tag) => _tables.ContainsKey(tag);

    /// <summary>
    /// Returns a table's offset and length in the font data.
    /// </summary>
    /// <param name="tag">The four-character tag.</param>
    /// <returns>The offset and length.</returns>
    public (int Offset, int Length) TableRange(string tag) =>
        _tables.TryGetValue(tag, out var range) ? range : throw FontBytes.Corrupt($"has no {tag.TrimEnd()} table");

    #endregion

    #region Private Methods

    private static PdfFontException Unsupported(string message) => new(PdfFontErrorReason.UnsupportedFormat, message);

    // Fails unless the table exists and is at least minimumLength bytes; returns its offset.
    private int Table(string tag, int minimumLength)
    {
        var (offset, length) = TableRange(tag);
        if (length < minimumLength) throw FontBytes.Corrupt($"has a {tag.TrimEnd()} table that is too short");
        return offset;
    }

    private ushort[] ReadAdvances(int metrics)
    {
        var (hmtx, length) = TableRange("hmtx");
        if (length < 4L * metrics) throw FontBytes.Corrupt("has an hmtx table that is too short");
        var advances = new ushort[metrics];
        for (int i = 0; i < metrics; i++) advances[i] = Data.U16(hmtx + 4 * i);
        return advances;
    }

    private void ReadNames()
    {
        if (!_tables.TryGetValue("name", out var name)) return;
        int count = Data.U16(name.Offset + 2);
        int strings = name.Offset + Data.U16(name.Offset + 4);
        Data.Require(name.Offset + 6, 12L * count, "the name records");

        var found = new Dictionary<int, (int Rank, string Value)>();
        for (int i = 0; i < count; i++)
        {
            int record = name.Offset + 6 + 12 * i;
            int platform = Data.U16(record), encoding = Data.U16(record + 2), language = Data.U16(record + 4);
            int id = Data.U16(record + 6), length = Data.U16(record + 8), offset = Data.U16(record + 10);
            if (id is not (1 or 2 or 4 or 6 or 16 or 17)) continue;

            // Windows Unicode English first, then any Windows or Unicode-platform record, then Macintosh Roman.
            int rank = (platform, encoding) switch
            {
                (3, 1 or 10) when language == 0x0409 => 0,
                (3, 1 or 10) or (0, _) => 1,
                (1, 0) => 2,
                _ => -1
            };
            if (rank < 0 || (found.TryGetValue(id, out var existing) && existing.Rank <= rank)) continue;

            var bytes = Data.Slice(strings + offset, length);
            string value = platform == 1 ? Encoding.ASCII.GetString(bytes) : Encoding.BigEndianUnicode.GetString(bytes);
            found[id] = (rank, value);
        }

        string? Get(int id) => found.TryGetValue(id, out var v) && v.Value.Length > 0 ? v.Value : null;
        FamilyName = Get(16) ?? Get(1) ?? string.Empty;
        SubfamilyName = Get(17) ?? Get(2) ?? string.Empty;
        FullName = Get(4) ?? string.Empty;
        PostScriptName = Get(6) ?? string.Empty;
    }

    private void ReadOs2()
    {
        if (!_tables.TryGetValue("OS/2", out var os2)) return;
        if (os2.Length < 78) throw FontBytes.Corrupt("has an OS/2 table that is too short");
        int t = os2.Offset;
        int version = Data.U16(t);
        AverageWidth = Data.I16(t + 2);
        WeightClass = Data.U16(t + 4);
        FsType = Data.U16(t + 8);
        FsSelection = Data.U16(t + 62);
        TypoAscender = Data.I16(t + 68);
        TypoDescender = Data.I16(t + 70);
        if (version >= 2 && os2.Length >= 96)
        {
            XHeight = Data.I16(t + 86);
            CapHeight = Data.I16(t + 88);
        }
    }

    private void ReadPost()
    {
        if (!_tables.TryGetValue("post", out var post) || post.Length < 16) return;
        ItalicAngle = Data.I32(post.Offset + 4) / 65536.0;
        IsFixedPitch = Data.U32(post.Offset + 12) != 0;
    }

    private int[] ReadGlyphOffsets(bool longOffsets)
    {
        var (loca, locaLength) = TableRange("loca");
        var (_, glyfLength) = TableRange("glyf");
        int entry = longOffsets ? 4 : 2;
        if (locaLength < (long)entry * (GlyphCount + 1)) throw FontBytes.Corrupt("has a loca table that is too short");

        var offsets = new int[GlyphCount + 1];
        for (int i = 0; i <= GlyphCount; i++)
        {
            long value = longOffsets ? Data.U32(loca + 4 * i) : 2L * Data.U16(loca + 2 * i);
            if (value > glyfLength || (i > 0 && value < offsets[i - 1])) throw FontBytes.Corrupt("has invalid glyph offsets (loca)");
            offsets[i] = (int)value;
        }
        return offsets;
    }

    private int[][] ReadComponents()
    {
        var (glyf, _) = TableRange("glyf");
        var components = new int[GlyphCount][];
        for (int g = 0; g < GlyphCount; g++)
        {
            var (offset, length) = GlyphRange(g);
            if (length == 0) { components[g] = Array.Empty<int>(); continue; }
            if (length < 10) throw FontBytes.Corrupt($"has a damaged glyph {g}");

            int start = glyf + offset;
            if (Data.I16(start) >= 0) { components[g] = Array.Empty<int>(); continue; }

            var list = new List<int>();
            int p = start + 10, end = start + length;
            while (true)
            {
                if (p + 4 > end) throw FontBytes.Corrupt($"has a damaged composite glyph {g}");
                int flags = Data.U16(p);
                int component = Data.U16(p + 2);
                if (component >= GlyphCount) throw FontBytes.Corrupt($"has a composite glyph {g} that uses a missing glyph");
                list.Add(component);
                p += 4 + ((flags & 0x0001) != 0 ? 4 : 2); // arguments: words or bytes
                if ((flags & 0x0008) != 0) p += 2; // WE_HAVE_A_SCALE
                else if ((flags & 0x0040) != 0) p += 4; // WE_HAVE_AN_X_AND_Y_SCALE
                else if ((flags & 0x0080) != 0) p += 8; // WE_HAVE_A_TWO_BY_TWO
                if ((flags & 0x0020) == 0) break; // no MORE_COMPONENTS
            }
            if (p > end) throw FontBytes.Corrupt($"has a damaged composite glyph {g}");
            components[g] = list.ToArray();
        }
        return components;
    }

    // Fails on a composite cycle or nesting deeper than MaxCompositeDepth; each glyph is visited once.
    private void CheckCompositeDepth()
    {
        var depth = new int[GlyphCount]; // 0 = not visited, -1 = on the current path, n = depth below and including it
        for (int g = 0; g < GlyphCount; g++)
        {
            if (depth[g] != 0 || _components[g].Length == 0) continue;
            var stack = new Stack<(int Glyph, int Next)>();
            stack.Push((g, 0));
            depth[g] = -1;
            while (stack.Count > 0)
            {
                var (glyph, next) = stack.Pop();
                var parts = _components[glyph];
                if (next < parts.Length)
                {
                    stack.Push((glyph, next + 1));
                    int child = parts[next];
                    if (depth[child] == -1) throw FontBytes.Corrupt($"has a composite glyph cycle at glyph {child}");
                    if (depth[child] == 0)
                    {
                        if (stack.Count >= MaxCompositeDepth) throw FontBytes.Corrupt($"nests composite glyphs deeper than {MaxCompositeDepth} levels");
                        depth[child] = -1;
                        stack.Push((child, 0));
                    }
                    else if (depth[child] + stack.Count > MaxCompositeDepth)
                    {
                        throw FontBytes.Corrupt($"nests composite glyphs deeper than {MaxCompositeDepth} levels");
                    }
                    continue;
                }

                int below = 0;
                foreach (int child in parts) below = Math.Max(below, depth[child]);
                depth[glyph] = below + 1;
            }
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public short Ascender { get; }

    public short AverageWidth { get; private set; }

    public (short XMin, short YMin, short XMax, short YMax) Bounds { get; }

    public short CapHeight { get; private set; }

    public CharacterMap CharacterMap { get; }

    /// <summary>
    /// Gets the parsed <c>CFF </c> table, or null when the font has TrueType or CFF2 outlines.
    /// </summary>
    public CffFont? Cff { get; }

    public FontBytes Data { get; }

    public short Descender { get; }

    public string FamilyName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the <c>OS/2</c> embedding permissions; 0 (installable) when the font has no <c>OS/2</c> table.
    /// </summary>
    public ushort FsType { get; private set; }

    public ushort FsSelection { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public int GlyphCount { get; }

    public bool IsFixedPitch { get; private set; }

    public double ItalicAngle { get; private set; }

    public short LineGap { get; }

    public ushort MacStyle { get; }

    public FontOutlines Outlines { get; }

    public string PostScriptName { get; private set; } = string.Empty;

    public string SubfamilyName { get; private set; } = string.Empty;

    public short TypoAscender { get; private set; }

    public short TypoDescender { get; private set; }

    public ushort UnitsPerEm { get; }

    /// <summary>
    /// Gets the <c>OS/2</c> weight class; 400 when the font has no <c>OS/2</c> table.
    /// </summary>
    public ushort WeightClass { get; private set; } = 400;

    public short XHeight { get; private set; }

    #endregion
}
