namespace UnlimitedPDF.Models.Internal.Fonts.OpenType;

/// <summary>
/// Maps Unicode code points to glyph IDs through one <c>cmap</c> subtable (format 4 or 12). Lookups search the
/// subtable's sorted segments directly, so a font that maps huge ranges costs no extra memory (A06).
/// </summary>
internal sealed class CharacterMap
{
    #region Fields and Constants

    private readonly FontBytes _data;
    private readonly int _subtable;
    private readonly int _format;
    private readonly int _count;
    private readonly int _glyphCount;

    #endregion

    #region Constructors and Finalizers

    private CharacterMap(FontBytes data, int subtable, int format, int count, int glyphCount)
    {
        _data = data;
        _subtable = subtable;
        _format = format;
        _count = count;
        _glyphCount = glyphCount;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads the best Unicode subtable of a <c>cmap</c> table: full-repertoire format 12 first, then BMP format 4.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The <c>cmap</c> table's offset.</param>
    /// <param name="length">The <c>cmap</c> table's length.</param>
    /// <param name="glyphCount">The font's glyph count; IDs at or above it map to glyph 0.</param>
    /// <returns>The character map.</returns>
    public static CharacterMap Read(FontBytes data, int table, int length, int glyphCount)
    {
        data.Require(table, length, "the cmap table");
        int records = data.U16(table + 2);
        data.Require(table + 4, 8L * records, "the cmap encoding records");

        int best = -1, bestRank = int.MaxValue;
        for (int i = 0; i < records; i++)
        {
            int record = table + 4 + 8 * i;
            int platform = data.U16(record), encoding = data.U16(record + 2);
            long offset = data.U32(record + 4);
            if (offset >= length) throw FontBytes.Corrupt("has a cmap subtable outside the cmap table");
            int subtable = table + (int)offset;
            int format = data.U16(subtable);
            int rank = (platform, encoding, format) switch
            {
                (3, 10, 12) or (0, 4, 12) or (0, 6, 12) => 0,
                (3, 1, 4) or (0, 3, 4) or (0, 1, 4) or (0, 0, 4) => 1,
                _ => int.MaxValue
            };
            if (rank < bestRank) (best, bestRank) = (subtable, rank);
        }
        if (best < 0) throw new PdfFontException(PdfFontErrorReason.UnsupportedFormat, "The font has no Unicode character map (cmap format 4 or 12).");

        int bestFormat = data.U16(best);
        if (bestFormat == 12)
        {
            long groups = data.U32(best + 12);
            data.Require(best + 16, 12 * groups, "the cmap groups");
            return new CharacterMap(data, best, 12, (int)groups, glyphCount);
        }

        int segments = data.U16(best + 6) / 2;
        data.Require(best + 14, 8L * segments + 2, "the cmap segments");

        // Check every glyph-array range now, so lookups while drawing can never read outside the data.
        int ends = best + 14, starts = ends + 2 * segments + 2, rangeOffsets = starts + 4 * segments;
        for (int s = 0; s < segments; s++)
        {
            int start = data.U16(starts + 2 * s), end = data.U16(ends + 2 * s);
            if (end < start) throw FontBytes.Corrupt("has a cmap segment that ends before it starts");
            int rangeOffset = data.U16(rangeOffsets + 2 * s);
            if (rangeOffset != 0) data.Require(rangeOffsets + 2L * s + rangeOffset, 2L * (end - start + 1), "a cmap glyph range");
        }
        return new CharacterMap(data, best, 4, segments, glyphCount);
    }

    /// <summary>
    /// Returns the glyph ID of a code point, or 0 (<c>.notdef</c>) when the font has no glyph for it.
    /// </summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns>The glyph ID.</returns>
    public int GlyphFor(int codePoint)
    {
        int glyph = _format == 12 ? Format12(codePoint) : Format4(codePoint);
        return glyph < _glyphCount ? glyph : 0;
    }

    #endregion

    #region Private Methods

    private int Format12(int codePoint)
    {
        int low = 0, high = _count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int group = _subtable + 16 + 12 * mid;
            long start = _data.U32(group), end = _data.U32(group + 4);
            if (codePoint < start) high = mid - 1;
            else if (codePoint > end) low = mid + 1;
            else
            {
                long glyph = _data.U32(group + 8) + (codePoint - start);
                return glyph > int.MaxValue ? 0 : (int)glyph;
            }
        }
        return 0;
    }

    private int Format4(int codePoint)
    {
        if (codePoint > 0xFFFF) return 0;
        int ends = _subtable + 14;
        int starts = ends + 2 * _count + 2;
        int deltas = starts + 2 * _count;
        int rangeOffsets = deltas + 2 * _count;

        int low = 0, high = _count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int end = _data.U16(ends + 2 * mid);
            int start = _data.U16(starts + 2 * mid);
            if (codePoint > end) low = mid + 1;
            else if (codePoint < start) high = mid - 1;
            else
            {
                int delta = _data.I16(deltas + 2 * mid);
                int rangeOffset = _data.U16(rangeOffsets + 2 * mid);
                if (rangeOffset == 0) return (codePoint + delta) & 0xFFFF;

                // The offset is relative to its own entry in idRangeOffset (the format's defined addressing).
                int address = rangeOffsets + 2 * mid + rangeOffset + 2 * (codePoint - start);
                int glyph = _data.U16(address);
                return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
            }
        }
        return 0;
    }

    #endregion

    #endregion
}
