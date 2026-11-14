namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

/// <summary>
/// An OpenType ItemVariationStore (used by <c>HVAR</c>, <c>MVAR</c>, and <c>CFF2</c>): variation regions plus delta
/// sets addressed by (outer, inner) indexes (D-013). Every count and offset is checked when it is read (A06).
/// </summary>
internal sealed class ItemVariationStore
{
    #region Fields and Constants

    private const int MaxRegions = 65535;

    private readonly double[][][] _regions; // [region][axis] = (start, peak, end) as three entries
    private readonly DeltaData[] _data;

    #endregion

    #region Constructors and Finalizers

    private ItemVariationStore(double[][][] regions, DeltaData[] data)
    {
        _regions = regions;
        _data = data;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads a store at <paramref name="offset"/>; nothing may extend past <paramref name="limit"/>.
    /// </summary>
    public static ItemVariationStore Read(FontBytes data, int offset, int limit, int axisCount)
    {
        if (offset < 0 || offset + 8 > limit) throw FontBytes.Corrupt("has an item variation store outside its table");
        if (data.U16(offset) != 1) throw FontBytes.Corrupt("has an unknown item variation store format");
        long regionList = offset + (long)data.U32(offset + 2);
        int dataCount = data.U16(offset + 6);
        if (offset + 8L + 4L * dataCount > limit || regionList + 4 > limit) throw FontBytes.Corrupt("has a truncated item variation store");

        int regionAxes = data.U16((int)regionList);
        int regionCount = data.U16((int)regionList + 2);
        if (regionAxes != axisCount || regionCount > MaxRegions) throw FontBytes.Corrupt("has variation regions that do not match the font's axes");
        if (regionList + 4 + 6L * regionAxes * regionCount > limit) throw FontBytes.Corrupt("has truncated variation regions");
        var regions = new double[regionCount][][];
        for (int r = 0; r < regionCount; r++)
        {
            regions[r] = new double[regionAxes][];
            for (int a = 0; a < regionAxes; a++)
            {
                int p = (int)regionList + 4 + 6 * (r * regionAxes + a);
                regions[r][a] = new[] { data.I16(p) / 16384.0, data.I16(p + 2) / 16384.0, data.I16(p + 4) / 16384.0 };
            }
        }

        var deltaData = new DeltaData[dataCount];
        for (int i = 0; i < dataCount; i++)
        {
            long subtable = offset + (long)data.U32(offset + 8 + 4 * i);
            if (subtable + 6 > limit) throw FontBytes.Corrupt("has a truncated item variation data");
            int s = (int)subtable;
            int items = data.U16(s);
            int wordField = data.U16(s + 2);
            int regionIndexCount = data.U16(s + 4);
            bool longWords = (wordField & 0x8000) != 0;
            int wordCount = wordField & 0x7FFF;
            if (wordCount > regionIndexCount) throw FontBytes.Corrupt("has an invalid item variation data");
            if (s + 6L + 2L * regionIndexCount > limit) throw FontBytes.Corrupt("has a truncated item variation data");
            var regionIndexes = new int[regionIndexCount];
            for (int r = 0; r < regionIndexCount; r++)
            {
                regionIndexes[r] = data.U16(s + 6 + 2 * r);
                if (regionIndexes[r] >= regionCount) throw FontBytes.Corrupt("uses a variation region that does not exist");
            }
            int wordSize = longWords ? 4 : 2, smallSize = longWords ? 2 : 1;
            int rowSize = wordCount * wordSize + (regionIndexCount - wordCount) * smallSize;
            int rows = s + 6 + 2 * regionIndexCount;
            if (rows + (long)rowSize * items > limit) throw FontBytes.Corrupt("has truncated variation deltas");
            deltaData[i] = new DeltaData(items, regionIndexes, wordCount, longWords, rows, rowSize);
        }
        return new ItemVariationStore(regions, deltaData);
    }

    /// <summary>
    /// Returns the scalar of every region one delta set uses, at the given normalized coordinates.
    /// </summary>
    /// <param name="outer">The ItemVariationData index.</param>
    /// <param name="coordinates">The normalized coordinates.</param>
    /// <returns>One scalar per region of that data, in its region order.</returns>
    public double[] Scalars(int outer, double[] coordinates)
    {
        if (outer >= _data.Length) throw FontBytes.Corrupt("uses item variation data that does not exist");
        return _data[outer].RegionIndexes.Select(r => RegionScalar(_regions[r], coordinates)).ToArray();
    }

    /// <summary>
    /// Returns the region count of one ItemVariationData (the number of deltas a CFF2 <c>blend</c> takes per value).
    /// </summary>
    public int RegionCount(int outer) =>
        outer < _data.Length ? _data[outer].RegionIndexes.Length : throw FontBytes.Corrupt("uses item variation data that does not exist");

    /// <summary>
    /// Returns the interpolated delta of item (<paramref name="outer"/>, <paramref name="inner"/>).
    /// </summary>
    public double Delta(FontBytes data, int outer, int inner, double[] coordinates)
    {
        if (outer >= _data.Length) throw FontBytes.Corrupt("uses item variation data that does not exist");
        var d = _data[outer];
        if (inner >= d.Items) throw FontBytes.Corrupt("uses a variation delta that does not exist");
        var scalars = Scalars(outer, coordinates);
        int p = d.Rows + inner * d.RowSize;
        double total = 0;
        for (int r = 0; r < d.RegionIndexes.Length; r++)
        {
            int delta;
            if (r < d.WordCount)
            {
                delta = d.LongWords ? data.I32(p) : data.I16(p);
                p += d.LongWords ? 4 : 2;
            }
            else
            {
                delta = d.LongWords ? data.I16(p) : (sbyte)data.U8(p);
                p += d.LongWords ? 2 : 1;
            }
            total += scalars[r] * delta;
        }
        return total;
    }

    /// <summary>
    /// Returns the scalar of one region at the given coordinates: the product of each axis's tent function.
    /// </summary>
    public static double RegionScalar(double[][] region, double[] coordinates)
    {
        double scalar = 1;
        for (int a = 0; a < region.Length && a < coordinates.Length; a++)
        {
            double start = region[a][0], peak = region[a][1], end = region[a][2], v = coordinates[a];
            if (start > peak || peak > end || (start < 0 && end > 0 && peak != 0) || peak == 0) continue;
            if (v == peak) continue;
            if (v <= start || v >= end) return 0;
            scalar *= v < peak ? (v - start) / (peak - start) : (end - v) / (end - peak);
        }
        return scalar;
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed record DeltaData(int Items, int[] RegionIndexes, int WordCount, bool LongWords, int Rows, int RowSize);

    #endregion
}
