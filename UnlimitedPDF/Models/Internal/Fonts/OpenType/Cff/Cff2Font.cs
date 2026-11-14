using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// The parsed <c>CFF2</c> table of a variable font with CFF outlines (D-013): the Top DICT, glyph charstrings, global
/// subroutines, Font DICTs with their local subroutines and default <c>vsindex</c>, FDSelect, and the variation store.
/// <see cref="Instance"/> evaluates every <c>blend</c> at given coordinates and returns a CFF version 1 charstring.
/// </summary>
internal sealed class Cff2Font
{
    #region Fields and Constants

    private const int OpCharStrings = 17;
    private const int OpPrivate = 18;
    private const int OpSubrs = 19;
    private const int OpVsIndex = 22;
    private const int OpVariationStore = 24;
    private const int OpFdArray = 1236;
    private const int OpFdSelect = 1237;

    private readonly CffIndex?[] _localSubrs;
    private readonly int[] _defaultVsIndex;
    private readonly byte[] _fontOfGlyph;

    #endregion

    #region Constructors and Finalizers

    private Cff2Font(CffIndex charStrings, CffIndex globalSubrs, CffIndex?[] localSubrs, int[] defaultVsIndex, byte[] fontOfGlyph, ItemVariationStore? store)
    {
        CharStrings = charStrings;
        GlobalSubrs = globalSubrs;
        _localSubrs = localSubrs;
        _defaultVsIndex = defaultVsIndex;
        _fontOfGlyph = fontOfGlyph;
        Store = store;
    }

    #endregion

    #region Methods

    #region Public Methods

    public static Cff2Font Read(FontBytes data, int table, int length, int glyphCount, int axisCount)
    {
        int limit = table + length;
        if (length < 5 || data.U8(table) != 2) throw FontBytes.Corrupt("has a CFF2 table that is not CFF2 version 2");
        int headerSize = data.U8(table + 2);
        int topLength = data.U16(table + 3);
        if ((long)table + headerSize + topLength > limit) throw FontBytes.Corrupt("has a truncated CFF2 Top DICT");
        var top = CffDict.Read(data, table + headerSize, topLength);
        var globalSubrs = CffIndex.Read(data, table + headerSize + topLength, limit, "Global Subr INDEX", wideCount: true);

        if (!top.Has(OpCharStrings) || !top.Has(OpFdArray)) throw FontBytes.Corrupt("has a CFF2 table without CharStrings or FDArray");
        var charStrings = CffIndex.Read(data, table + top.Int(OpCharStrings), limit, "CharStrings INDEX", wideCount: true);
        if (charStrings.Count != glyphCount) throw FontBytes.Corrupt("has a CFF2 glyph count that differs from maxp");

        ItemVariationStore? store = null;
        if (top.Has(OpVariationStore))
        {
            int vstore = table + top.Int(OpVariationStore);
            int storeLength = data.U16(vstore);
            if ((long)vstore + 2 + storeLength > limit) throw FontBytes.Corrupt("has a truncated CFF2 variation store");
            store = ItemVariationStore.Read(data, vstore + 2, vstore + 2 + storeLength, axisCount);
        }

        var fdArray = CffIndex.Read(data, table + top.Int(OpFdArray), limit, "FDArray", wideCount: true);
        if (fdArray.Count is 0 or > 256) throw FontBytes.Corrupt("has an invalid CFF2 FDArray");
        var localSubrs = new CffIndex?[fdArray.Count];
        var vsIndex = new int[fdArray.Count];
        for (int fd = 0; fd < fdArray.Count; fd++)
        {
            var (offset, size) = fdArray.Entry(fd);
            var fontDict = CffDict.Read(data, offset, size);
            if (!fontDict.Has(OpPrivate)) continue;
            int privateSize = fontDict.Int(OpPrivate, 0), privateOffset = fontDict.Int(OpPrivate, 1);
            if (privateSize < 0 || privateOffset < 0 || (long)table + privateOffset + privateSize > limit) throw FontBytes.Corrupt("has a CFF2 Private DICT outside the table");
            var privateDict = CffDict.Read(data, table + privateOffset, privateSize);
            vsIndex[fd] = privateDict.Int(OpVsIndex);
            if (privateDict.Has(OpSubrs)) localSubrs[fd] = CffIndex.Read(data, table + privateOffset + privateDict.Int(OpSubrs), limit, "Local Subr INDEX", wideCount: true);
        }

        var fontOfGlyph = new byte[glyphCount];
        if (top.Has(OpFdSelect)) ReadFdSelect(data, table + top.Int(OpFdSelect), limit, glyphCount, fdArray.Count, fontOfGlyph);
        else if (fdArray.Count != 1) throw FontBytes.Corrupt("has several CFF2 Font DICTs without FDSelect");

        return new Cff2Font(charStrings, globalSubrs, localSubrs, vsIndex, fontOfGlyph, store);
    }

    /// <summary>
    /// Returns glyph <paramref name="glyph"/> as a CFF version 1 charstring at the given coordinates: blends
    /// evaluated, subroutines expanded, <c>endchar</c> appended.
    /// </summary>
    public byte[] Instance(int glyph, double[] coordinates)
    {
        int fd = _fontOfGlyph[glyph];
        return Cff2Charstring.Instance(CharStrings.Bytes(glyph), _localSubrs[fd], GlobalSubrs, Store, _defaultVsIndex[fd], coordinates);
    }

    #endregion

    #region Private Methods

    private static void ReadFdSelect(FontBytes data, int offset, int limit, int glyphCount, int fontCount, byte[] fontOfGlyph)
    {
        if (offset >= limit) throw FontBytes.Corrupt("has a CFF2 FDSelect outside the table");
        int format = data.U8(offset);
        byte Check(int font) => font < fontCount ? (byte)font : throw FontBytes.Corrupt("selects a CFF2 Font DICT that does not exist");
        switch (format)
        {
            case 0:
                if (offset + 1L + glyphCount > limit) throw FontBytes.Corrupt("has a truncated CFF2 FDSelect");
                for (int g = 0; g < glyphCount; g++) fontOfGlyph[g] = Check(data.U8(offset + 1 + g));
                return;
            case 3:
            case 4:
            {
                bool wide = format == 4;
                long ranges = wide ? data.U32(offset + 1) : data.U16(offset + 1);
                int header = wide ? 5 : 3, rangeSize = wide ? 6 : 3, gidSize = wide ? 4 : 2;
                if (ranges == 0 || offset + header + ranges * rangeSize + gidSize > limit) throw FontBytes.Corrupt("has a truncated CFF2 FDSelect");
                for (int r = 0; r < ranges; r++)
                {
                    int p = offset + header + r * rangeSize;
                    long first = wide ? data.U32(p) : data.U16(p);
                    byte font = Check(wide ? data.U16(p + 4) : data.U8(p + 2));
                    long next = wide ? data.U32(p + rangeSize) : data.U16(p + rangeSize);
                    if ((r == 0 && first != 0) || next <= first || next > glyphCount) throw FontBytes.Corrupt("has invalid CFF2 FDSelect ranges");
                    for (long g = first; g < next; g++) fontOfGlyph[g] = font;
                }
                return;
            }
            default:
                throw FontBytes.Corrupt($"has an unknown CFF2 FDSelect format ({format})");
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public CffIndex CharStrings { get; }

    public CffIndex GlobalSubrs { get; }

    public ItemVariationStore? Store { get; }

    #endregion
}
