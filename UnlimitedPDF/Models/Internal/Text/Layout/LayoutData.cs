using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Models.Internal.Text.Layout;

/// <summary>
/// The header of a coverage-based (format 3) contextual subtable: where its input coverage offsets start and how many
/// there are, its backtrack and lookahead coverage arrays (chained subtables only), and its sequence lookup records.
/// </summary>
internal readonly record struct CoverageRule(int InputAt, int InputCount, (int Count, int At) Backtrack, (int Count, int At) Lookahead, int Records, int LookupCount);

/// <summary>
/// Shared OpenType layout structures (Coverage and ClassDef tables) read from font data with every offset checked
/// (A06). Values are read in place on demand, so a large font costs no extra memory.
/// </summary>
internal static class LayoutData
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads a count of 16-bit entries at <paramref name="p"/>, checks they are in the data, and moves
    /// <paramref name="p"/> past them; returns the count and where the entries start.
    /// </summary>
    public static (int Count, int At) Array16(FontBytes data, ref int p, string what)
    {
        int count = data.U16(p);
        data.Require(p + 2, 2L * count, what);
        var array = (count, p + 2);
        p += 2 + 2 * count;
        return array;
    }

    /// <summary>
    /// Reads the header of a format 3 contextual subtable (GSUB 5 and 6, GPOS 7 and 8). Each array is a count and
    /// coverage offsets; the offsets are read one entry per glyph compared, never here (RV-20).
    /// </summary>
    public static CoverageRule ReadCoverageRule(FontBytes data, int subtable, bool chained)
    {
        int p = subtable + 2;
        var backtrack = (Count: 0, At: 0);
        var lookahead = (Count: 0, At: 0);
        if (chained) backtrack = Array16(data, ref p, "a chained context rule");
        int inputCount = data.U16(p);
        int lookupCount = 0;
        if (!chained) lookupCount = data.U16(p + 2);
        int inputAt = p + (chained ? 2 : 4);
        data.Require(inputAt, 2L * inputCount, "a context rule");
        p = inputAt + 2 * inputCount;
        if (chained)
        {
            lookahead = Array16(data, ref p, "a chained context rule");
            lookupCount = data.U16(p);
            p += 2;
        }
        return new CoverageRule(inputAt, inputCount, backtrack, lookahead, p, lookupCount);
    }

    /// <summary>
    /// Sets in <paramref name="set"/> (one bit per glyph ID below <paramref name="glyphCount"/>) every glyph the
    /// Coverage table at <paramref name="offset"/> lists, with the same checks <see cref="Coverage"/> makes. Each
    /// entry and each glyph of a range spends one unit of <paramref name="budget"/>; returns false, with the set
    /// incomplete, when the budget runs out (ranges a damaged or hostile table repeats, A06).
    /// </summary>
    public static bool AddCoverage(FontBytes data, int offset, int glyphCount, ulong[] set, ref long budget)
    {
        int format = data.U16(offset);
        int count = data.U16(offset + 2);
        if (format == 1)
        {
            data.Require(offset + 4, 2L * count, "a coverage table");
            if ((budget -= count) < 0) return false;
            for (int i = 0; i < count; i++) AddGlyph(set, data.U16(offset + 4 + 2 * i), glyphCount);
            return true;
        }
        if (format == 2)
        {
            data.Require(offset + 4, 6L * count, "a coverage table");
            for (int i = 0; i < count; i++)
            {
                int record = offset + 4 + 6 * i;
                int start = data.U16(record), end = Math.Min((int)data.U16(record + 2), glyphCount - 1);
                if ((budget -= 1 + Math.Max(0, end - start + 1)) < 0) return false;
                for (int glyph = start; glyph <= end; glyph++) AddGlyph(set, glyph, glyphCount);
            }
            return true;
        }
        throw FontBytes.Corrupt($"has an unknown coverage format ({format})");
    }

    /// <summary>
    /// Returns the coverage index of <paramref name="glyph"/> in the Coverage table at <paramref name="offset"/>, or -1.
    /// </summary>
    public static int Coverage(FontBytes data, int offset, int glyph)
    {
        int format = data.U16(offset);
        int count = data.U16(offset + 2);
        if (format == 1)
        {
            data.Require(offset + 4, 2L * count, "a coverage table");
            int low = 0, high = count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int value = data.U16(offset + 4 + 2 * mid);
                if (value < glyph) low = mid + 1;
                else if (value > glyph) high = mid - 1;
                else return mid;
            }
            return -1;
        }
        if (format == 2)
        {
            data.Require(offset + 4, 6L * count, "a coverage table");
            int low = 0, high = count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int record = offset + 4 + 6 * mid;
                int start = data.U16(record), end = data.U16(record + 2);
                if (end < glyph) low = mid + 1;
                else if (start > glyph) high = mid - 1;
                else return data.U16(record + 4) + glyph - start;
            }
            return -1;
        }
        throw FontBytes.Corrupt($"has an unknown coverage format ({format})");
    }

    /// <summary>
    /// Returns the class of <paramref name="glyph"/> in the ClassDef table at <paramref name="offset"/> (0 when the
    /// glyph is not listed or the offset is 0).
    /// </summary>
    public static int ClassOf(FontBytes data, int offset, int glyph)
    {
        if (offset == 0) return 0;
        int format = data.U16(offset);
        if (format == 1)
        {
            int start = data.U16(offset + 2);
            int count = data.U16(offset + 4);
            data.Require(offset + 6, 2L * count, "a class definition");
            return glyph >= start && glyph < start + count ? data.U16(offset + 6 + 2 * (glyph - start)) : 0;
        }
        if (format == 2)
        {
            int count = data.U16(offset + 2);
            data.Require(offset + 4, 6L * count, "a class definition");
            int low = 0, high = count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int record = offset + 4 + 6 * mid;
                int start = data.U16(record), end = data.U16(record + 2);
                if (end < glyph) low = mid + 1;
                else if (start > glyph) high = mid - 1;
                else return data.U16(record + 4);
            }
            return 0;
        }
        throw FontBytes.Corrupt($"has an unknown class definition format ({format})");
    }

    /// <summary>
    /// Returns <paramref name="baseOffset"/> plus the 16-bit offset at <paramref name="at"/>, or 0 when that offset
    /// is 0 (absent).
    /// </summary>
    public static int Offset16(FontBytes data, int baseOffset, int at)
    {
        int value = data.U16(at);
        return value == 0 ? 0 : baseOffset + value;
    }

    #endregion

    #region Private Methods

    private static void AddGlyph(ulong[] set, int glyph, int glyphCount)
    {
        if (glyph < glyphCount) set[glyph >> 6] |= 1UL << (glyph & 63);
    }

    #endregion

    #endregion
}

/// <summary>
/// The glyph definition table (<c>GDEF</c>): glyph classes (1 base, 2 ligature, 3 mark, 4 component), mark
/// attachment classes, and mark filtering sets.
/// </summary>
internal sealed class GlyphDefinitions
{
    #region Fields and Constants

    private readonly FontBytes _data;
    private readonly int _glyphClasses;
    private readonly int _markAttachClasses;
    private readonly int _markSets;

    #endregion

    #region Constructors and Finalizers

    private GlyphDefinitions(FontBytes data, int glyphClasses, int markAttachClasses, int markSets)
    {
        _data = data;
        _glyphClasses = glyphClasses;
        _markAttachClasses = markAttachClasses;
        _markSets = markSets;
    }

    #endregion

    #region Methods

    #region Public Methods

    public static GlyphDefinitions? Read(OpenTypeFont font)
    {
        if (!font.HasTable("GDEF")) return null;
        var data = font.Data;
        var (gdef, length) = font.TableRange("GDEF");
        if (length < 12) throw FontBytes.Corrupt("has a GDEF table that is too short");
        int minor = data.U16(gdef + 2);
        int markSets = minor >= 2 && length >= 14 ? LayoutData.Offset16(data, gdef, gdef + 12) : 0;
        return new GlyphDefinitions(data, LayoutData.Offset16(data, gdef, gdef + 4), LayoutData.Offset16(data, gdef, gdef + 10), markSets);
    }

    public int GlyphClass(int glyph) => LayoutData.ClassOf(_data, _glyphClasses, glyph);

    public int MarkAttachClass(int glyph) => LayoutData.ClassOf(_data, _markAttachClasses, glyph);

    /// <summary>
    /// Returns whether <paramref name="glyph"/> is in mark filtering set <paramref name="set"/>.
    /// </summary>
    public bool InMarkSet(int set, int glyph)
    {
        if (_markSets == 0) return false;
        int count = _data.U16(_markSets + 2);
        if (set >= count) return false;
        long coverage = _markSets + (long)_data.U32(_markSets + 4 + 4 * set);
        return LayoutData.Coverage(_data, (int)coverage, glyph) >= 0;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public bool HasGlyphClasses => _glyphClasses != 0;

    #endregion
}
