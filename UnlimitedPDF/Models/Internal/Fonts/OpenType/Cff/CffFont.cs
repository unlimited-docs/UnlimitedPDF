namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// The parsed <c>CFF </c> table of an OpenType font with CFF outlines (D-010): the Top DICT, the glyph charstrings,
/// the charset, the global subroutines, and per-glyph local subroutines (one Private DICT for a name-keyed font, one
/// per Font DICT for a CID-keyed font). Every glyph is flattened once at load, so damaged or hostile charstrings fail
/// here (A06).
/// </summary>
internal sealed class CffFont
{
    #region Fields and Constants

    // Top DICT and Private DICT operators (12 x is 1200 + x).
    private const int OpCharset = 15;
    private const int OpCharStrings = 17;
    private const int OpPrivate = 18;
    private const int OpSubrs = 19;
    private const int OpCharstringType = 1206;
    private const int OpRos = 1230;
    private const int OpFdArray = 1236;
    private const int OpFdSelect = 1237;

    private readonly CffIndex?[] _localSubrsByFont;
    private readonly byte[] _fontOfGlyph;

    #endregion

    #region Constructors and Finalizers

    private CffFont(CffIndex charStrings, CffIndex globalSubrs, CffIndex?[] localSubrsByFont, byte[] fontOfGlyph, bool cidKeyed, string name)
    {
        CharStrings = charStrings;
        GlobalSubrs = globalSubrs;
        _localSubrsByFont = localSubrsByFont;
        _fontOfGlyph = fontOfGlyph;
        IsCidKeyed = cidKeyed;
        Name = name;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses a <c>CFF </c> table and checks every glyph.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The table's offset.</param>
    /// <param name="length">The table's length.</param>
    /// <param name="glyphCount">The font's glyph count (<c>maxp</c>); the CharStrings INDEX must match it.</param>
    /// <returns>The parsed table.</returns>
    public static CffFont Read(FontBytes data, int table, int length, int glyphCount)
    {
        int limit = table + length;
        if (length < 4 || data.U8(table) != 1) throw new PdfFontException(PdfFontErrorReason.UnsupportedFormat, "The font's CFF table is not CFF version 1.");
        int headerSize = data.U8(table + 2);

        var names = CffIndex.Read(data, table + headerSize, limit, "Name INDEX");
        var topDicts = CffIndex.Read(data, names.End, limit, "Top DICT INDEX");
        var strings = CffIndex.Read(data, topDicts.End, limit, "String INDEX");
        var globalSubrs = CffIndex.Read(data, strings.End, limit, "Global Subr INDEX");
        if (names.Count != 1 || topDicts.Count != 1) throw new PdfFontException(PdfFontErrorReason.UnsupportedFormat, "The font's CFF table holds more than one font.");

        var (topOffset, topLength) = topDicts.Entry(0);
        var top = CffDict.Read(data, topOffset, topLength);
        if (top.Int(OpCharstringType, fallback: 2) != 2) throw new PdfFontException(PdfFontErrorReason.UnsupportedFormat, "The font uses Type 1 charstrings inside CFF, which is not supported.");
        if (!top.Has(OpCharStrings)) throw FontBytes.Corrupt("has no CFF CharStrings");

        var charStrings = CffIndex.Read(data, table + top.Int(OpCharStrings), limit, "CharStrings INDEX");
        if (charStrings.Count != glyphCount) throw FontBytes.Corrupt("has a CFF glyph count that differs from maxp");

        bool cidKeyed = top.Has(OpRos);
        CffIndex?[] localSubrs;
        byte[] fontOfGlyph = new byte[glyphCount];
        if (cidKeyed)
        {
            if (!top.Has(OpFdArray) || !top.Has(OpFdSelect)) throw FontBytes.Corrupt("is a CID-keyed CFF font without FDArray or FDSelect");
            var fdArray = CffIndex.Read(data, table + top.Int(OpFdArray), limit, "FDArray");
            if (fdArray.Count is 0 or > 256) throw FontBytes.Corrupt("has an invalid CFF FDArray");
            localSubrs = new CffIndex?[fdArray.Count];
            for (int fd = 0; fd < fdArray.Count; fd++)
            {
                var (fdOffset, fdLength) = fdArray.Entry(fd);
                localSubrs[fd] = ReadPrivateSubrs(data, table, limit, CffDict.Read(data, fdOffset, fdLength));
            }
            ReadFdSelect(data, table + top.Int(OpFdSelect), limit, glyphCount, fdArray.Count, fontOfGlyph);
        }
        else
        {
            localSubrs = new[] { ReadPrivateSubrs(data, table, limit, top) };
        }

        if (top.Has(OpCharset)) CheckCharset(data, table, limit, top.Int(OpCharset), glyphCount);

        var font = new CffFont(charStrings, globalSubrs, localSubrs, fontOfGlyph, cidKeyed, Encoding.ASCII.GetString(names.Bytes(0)));
        for (int g = 0; g < glyphCount; g++) font.Flatten(g);
        return font;
    }

    /// <summary>
    /// Returns a glyph's charstring with every subroutine call expanded.
    /// </summary>
    /// <param name="glyph">The glyph ID.</param>
    /// <returns>The flattened charstring.</returns>
    public byte[] Flatten(int glyph) => Type2Charstring.Flatten(CharStrings.Bytes(glyph), _localSubrsByFont[_fontOfGlyph[glyph]], GlobalSubrs);

    #endregion

    #region Private Methods

    // Reads a Private DICT named by a Top DICT or Font DICT and returns its local subroutines, if any. The Subrs
    // offset is relative to the Private DICT's start.
    private static CffIndex? ReadPrivateSubrs(FontBytes data, int table, int limit, CffDict dict)
    {
        if (!dict.Has(OpPrivate)) return null;
        int size = dict.Int(OpPrivate, 0), offset = dict.Int(OpPrivate, 1);
        if (size < 0 || offset < 0 || (long)table + offset + size > limit) throw FontBytes.Corrupt("has a CFF Private DICT outside the CFF table");
        var privateDict = CffDict.Read(data, table + offset, size);
        if (!privateDict.Has(OpSubrs)) return null;
        return CffIndex.Read(data, table + offset + privateDict.Int(OpSubrs), limit, "Local Subr INDEX");
    }

    private static void ReadFdSelect(FontBytes data, int offset, int limit, int glyphCount, int fontCount, byte[] fontOfGlyph)
    {
        if (offset >= limit) throw FontBytes.Corrupt("has a CFF FDSelect outside the CFF table");
        int format = data.U8(offset);
        if (format == 0)
        {
            if (offset + 1L + glyphCount > limit) throw FontBytes.Corrupt("has a truncated CFF FDSelect");
            for (int g = 0; g < glyphCount; g++) fontOfGlyph[g] = CheckFont(data.U8(offset + 1 + g), fontCount);
            return;
        }
        if (format != 3) throw FontBytes.Corrupt($"has an unknown CFF FDSelect format ({format})");

        int ranges = data.U16(offset + 1);
        if (ranges == 0 || offset + 3L + 3L * ranges + 2 > limit) throw FontBytes.Corrupt("has a truncated CFF FDSelect");
        for (int r = 0; r < ranges; r++)
        {
            int first = data.U16(offset + 3 + 3 * r);
            byte font = CheckFont(data.U8(offset + 5 + 3 * r), fontCount);
            int next = data.U16(offset + 6 + 3 * r); // the next range's first glyph, or the sentinel
            if ((r == 0 && first != 0) || next <= first || next > glyphCount) throw FontBytes.Corrupt("has invalid CFF FDSelect ranges");
            for (int g = first; g < next; g++) fontOfGlyph[g] = font;
        }
        if (data.U16(offset + 3 + 3 * ranges) != glyphCount) throw FontBytes.Corrupt("has CFF FDSelect ranges that do not cover every glyph");
    }

    private static byte CheckFont(int font, int fontCount) =>
        font < fontCount ? (byte)font : throw FontBytes.Corrupt("selects a CFF Font DICT that does not exist");

    // The charset is not needed to draw, but a damaged one marks a damaged font. Offsets 0 to 2 name the predefined
    // charsets.
    private static void CheckCharset(FontBytes data, int table, int limit, int offset, int glyphCount)
    {
        if (offset <= 2) return;
        int p = table + offset;
        if (p >= limit) throw FontBytes.Corrupt("has a CFF charset outside the CFF table");
        int format = data.U8(p);
        if (format == 0)
        {
            if (p + 1L + 2L * (glyphCount - 1) > limit) throw FontBytes.Corrupt("has a truncated CFF charset");
            return;
        }
        if (format is not (1 or 2)) throw FontBytes.Corrupt($"has an unknown CFF charset format ({format})");

        int covered = 1, q = p + 1, countSize = format == 1 ? 1 : 2;
        while (covered < glyphCount)
        {
            if (q + 2 + countSize > limit) throw FontBytes.Corrupt("has a truncated CFF charset");
            int left = countSize == 1 ? data.U8(q + 2) : data.U16(q + 2);
            covered += left + 1;
            q += 2 + countSize;
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public CffIndex CharStrings { get; }

    public CffIndex GlobalSubrs { get; }

    public bool IsCidKeyed { get; }

    public string Name { get; }

    #endregion
}
