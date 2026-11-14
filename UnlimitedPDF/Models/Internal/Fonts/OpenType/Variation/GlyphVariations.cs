namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

/// <summary>
/// The <c>gvar</c> table: per-glyph point deltas for TrueType outlines (D-013). <see cref="Deltas"/> returns, for one
/// glyph at normalized coordinates, the x and y delta of every point plus the four phantom points (the second of
/// which carries the advance width), with untouched points interpolated (IUP) as the specification defines. Every
/// read is bounds-checked (A06).
/// </summary>
internal sealed class GlyphVariations
{
    #region Fields and Constants

    private const int SharedPointNumbers = 0x8000;
    private const int CountMask = 0x0FFF;
    private const int EmbeddedPeakTuple = 0x8000;
    private const int IntermediateRegion = 0x4000;
    private const int PrivatePointNumbers = 0x2000;
    private const int TupleIndexMask = 0x0FFF;

    private readonly FontBytes _data;
    private readonly int _axisCount;
    private readonly double[][] _sharedTuples;
    private readonly long[] _glyphStarts;

    #endregion

    #region Constructors and Finalizers

    private GlyphVariations(FontBytes data, int axisCount, double[][] sharedTuples, long[] glyphStarts)
    {
        _data = data;
        _axisCount = axisCount;
        _sharedTuples = sharedTuples;
        _glyphStarts = glyphStarts;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads the font's <c>gvar</c> table, or returns null when it has none.
    /// </summary>
    public static GlyphVariations? Read(OpenTypeFont font, int axisCount)
    {
        if (!font.HasTable("gvar")) return null;
        var data = font.Data;
        var (gvar, length) = font.TableRange("gvar");
        if (length < 20) throw FontBytes.Corrupt("has a gvar table that is too short");
        if (data.U16(gvar + 4) != axisCount) throw FontBytes.Corrupt("has a gvar table whose axis count differs from fvar");
        int sharedCount = data.U16(gvar + 6);
        long sharedOffset = data.U32(gvar + 8);
        int glyphCount = data.U16(gvar + 12);
        bool longOffsets = (data.U16(gvar + 14) & 1) != 0;
        long arrayOffset = data.U32(gvar + 16);
        if (glyphCount != font.GlyphCount) throw FontBytes.Corrupt("has a gvar glyph count that differs from maxp");
        if (sharedOffset + 2L * axisCount * sharedCount > length) throw FontBytes.Corrupt("has truncated gvar shared tuples");
        if (20L + (glyphCount + 1L) * (longOffsets ? 4 : 2) > length) throw FontBytes.Corrupt("has truncated gvar offsets");

        var shared = new double[sharedCount][];
        for (int t = 0; t < sharedCount; t++)
        {
            shared[t] = new double[axisCount];
            for (int a = 0; a < axisCount; a++) shared[t][a] = data.I16(gvar + (int)sharedOffset + 2 * (t * axisCount + a)) / 16384.0;
        }

        var starts = new long[glyphCount + 1];
        for (int g = 0; g <= glyphCount; g++)
        {
            long value = longOffsets ? data.U32(gvar + 20 + 4 * g) : 2L * data.U16(gvar + 20 + 2 * g);
            starts[g] = gvar + arrayOffset + value;
            if (starts[g] > gvar + length || (g > 0 && starts[g] < starts[g - 1])) throw FontBytes.Corrupt("has invalid gvar glyph offsets");
        }
        return new GlyphVariations(data, axisCount, shared, starts);
    }

    /// <summary>
    /// Returns the deltas of one glyph's points (its outline points, or one per component, then four phantom
    /// points) at the given coordinates.
    /// </summary>
    /// <param name="glyph">The glyph ID.</param>
    /// <param name="x">The points' original x coordinates (phantom points included), for interpolation.</param>
    /// <param name="y">The points' original y coordinates (phantom points included).</param>
    /// <param name="contourEnds">The last point of each contour; empty for a composite glyph (no interpolation).</param>
    /// <param name="coordinates">The normalized coordinates.</param>
    /// <returns>The x and y deltas, one per point.</returns>
    public (double[] X, double[] Y) Deltas(int glyph, double[] x, double[] y, int[] contourEnds, double[] coordinates)
    {
        int pointCount = x.Length;
        var dx = new double[pointCount];
        var dy = new double[pointCount];
        long start = _glyphStarts[glyph], end = _glyphStarts[glyph + 1];
        if (end - start < 4) return (dx, dy);

        int p = (int)start;
        int countField = _data.U16(p);
        int tupleCount = countField & CountMask;
        long serialized = start + _data.U16(p + 2);
        if (serialized > end) throw Damaged(glyph);
        p += 4;

        int q = (int)serialized;
        int[]? sharedPoints = null;
        if ((countField & SharedPointNumbers) != 0) sharedPoints = ReadPoints(ref q, (int)end, pointCount, glyph);

        for (int t = 0; t < tupleCount; t++)
        {
            if (p + 4 > end) throw Damaged(glyph);
            int dataSize = _data.U16(p);
            int index = _data.U16(p + 2);
            p += 4;

            double[] peak;
            if ((index & EmbeddedPeakTuple) != 0)
            {
                peak = ReadTuple(ref p, (int)end, glyph);
            }
            else
            {
                int shared = index & TupleIndexMask;
                if (shared >= _sharedTuples.Length) throw Damaged(glyph);
                peak = _sharedTuples[shared];
            }
            double[]? startTuple = null, endTuple = null;
            if ((index & IntermediateRegion) != 0)
            {
                startTuple = ReadTuple(ref p, (int)end, glyph);
                endTuple = ReadTuple(ref p, (int)end, glyph);
            }

            int tupleData = q;
            int tupleEnd = q + dataSize;
            if (tupleEnd > end) throw Damaged(glyph);
            q = tupleEnd;

            double scalar = Scalar(peak, startTuple, endTuple, coordinates);
            if (scalar == 0) continue;

            int r = tupleData;
            int[]? points = (index & PrivatePointNumbers) != 0 ? ReadPoints(ref r, tupleEnd, pointCount, glyph) : sharedPoints;
            int count = points?.Length ?? pointCount;
            var xs = ReadDeltas(ref r, tupleEnd, count, glyph);
            var ys = ReadDeltas(ref r, tupleEnd, count, glyph);

            if (points is null)
            {
                for (int i = 0; i < pointCount; i++) { dx[i] += scalar * xs[i]; dy[i] += scalar * ys[i]; }
                continue;
            }

            var tx = new double[pointCount];
            var ty = new double[pointCount];
            var touched = new bool[pointCount];
            for (int i = 0; i < points.Length; i++)
            {
                tx[points[i]] += xs[i];
                ty[points[i]] += ys[i];
                touched[points[i]] = true;
            }
            Interpolate(contourEnds, x, tx, touched);
            Interpolate(contourEnds, y, ty, touched);
            for (int i = 0; i < pointCount; i++) { dx[i] += scalar * tx[i]; dy[i] += scalar * ty[i]; }
        }
        return (dx, dy);
    }

    #endregion

    #region Private Methods

    private static PdfFontException Damaged(int glyph) => FontBytes.Corrupt($"has damaged gvar data for glyph {glyph}");

    private double[] ReadTuple(ref int p, int end, int glyph)
    {
        if (p + 2 * _axisCount > end) throw Damaged(glyph);
        var tuple = new double[_axisCount];
        for (int a = 0; a < _axisCount; a++) tuple[a] = _data.I16(p + 2 * a) / 16384.0;
        p += 2 * _axisCount;
        return tuple;
    }

    // The tuple's scalar: per axis, a tent from start to peak to end (implicitly 0 to peak without an intermediate
    // region); axes whose peak is 0 do not take part.
    private static double Scalar(double[] peak, double[]? start, double[]? end, double[] coordinates)
    {
        double scalar = 1;
        for (int a = 0; a < peak.Length; a++)
        {
            double p = peak[a];
            if (p == 0) continue;
            double v = a < coordinates.Length ? coordinates[a] : 0;
            if (v == 0) return 0;
            double s = start?[a] ?? Math.Min(p, 0), e = end?[a] ?? Math.Max(p, 0);
            if (v == p) continue;
            if (v < s || v > e) return 0;
            scalar *= v < p ? (p == s ? 1 : (v - s) / (p - s)) : (p == e ? 1 : (e - v) / (e - p));
        }
        return scalar;
    }

    // Packed point numbers: a count (0 means every point), then runs of byte or word deltas.
    private int[]? ReadPoints(ref int p, int end, int pointCount, int glyph)
    {
        if (p >= end) throw Damaged(glyph);
        int count = _data.U8(p++);
        if ((count & 0x80) != 0)
        {
            if (p >= end) throw Damaged(glyph);
            count = ((count & 0x7F) << 8) | _data.U8(p++);
        }
        if (count == 0) return null;
        if (count > pointCount) throw Damaged(glyph);

        var points = new int[count];
        int read = 0, value = 0;
        while (read < count)
        {
            if (p >= end) throw Damaged(glyph);
            int control = _data.U8(p++);
            bool words = (control & 0x80) != 0;
            int run = (control & 0x7F) + 1;
            if (read + run > count || p + run * (words ? 2 : 1) > end) throw Damaged(glyph);
            for (int i = 0; i < run; i++)
            {
                value += words ? _data.U16(p) : _data.U8(p);
                p += words ? 2 : 1;
                if (value >= pointCount) throw Damaged(glyph);
                points[read++] = value;
            }
        }
        return points;
    }

    // Packed deltas: runs of zero, byte, or word values.
    private int[] ReadDeltas(ref int p, int end, int count, int glyph)
    {
        var deltas = new int[count];
        int read = 0;
        while (read < count)
        {
            if (p >= end) throw Damaged(glyph);
            int control = _data.U8(p++);
            int run = (control & 0x3F) + 1;
            if (read + run > count) throw Damaged(glyph);
            if ((control & 0x80) != 0)
            {
                read += run; // zeros
                continue;
            }
            bool words = (control & 0x40) != 0;
            if (p + run * (words ? 2 : 1) > end) throw Damaged(glyph);
            for (int i = 0; i < run; i++)
            {
                deltas[read++] = words ? _data.I16(p) : (sbyte)_data.U8(p);
                p += words ? 2 : 1;
            }
        }
        return deltas;
    }

    // Interpolation of untouched points (IUP), one coordinate at a time, contour by contour. A contour with no
    // touched point gets no deltas; with one, every point gets its delta; otherwise each untouched point takes the
    // delta of its touched neighbors, interpolated by position between them, or the nearer neighbor's delta outside.
    private static void Interpolate(int[] contourEnds, double[] original, double[] deltas, bool[] touched)
    {
        int first = 0;
        foreach (int last in contourEnds)
        {
            var touchedPoints = Enumerable.Range(first, last - first + 1).Where(i => touched[i]).ToList();
            if (touchedPoints.Count == 1)
            {
                for (int i = first; i <= last; i++) if (!touched[i]) deltas[i] = deltas[touchedPoints[0]];
            }
            else if (touchedPoints.Count > 1)
            {
                for (int k = 0; k < touchedPoints.Count; k++)
                {
                    int a = touchedPoints[k], b = touchedPoints[(k + 1) % touchedPoints.Count];
                    for (int i = a + 1; ; i++)
                    {
                        if (i > last) i = first;
                        if (i == b) break;
                        deltas[i] = Between(original[i], original[a], deltas[a], original[b], deltas[b]);
                    }
                }
            }
            first = last + 1;
        }
    }

    private static double Between(double v, double c1, double d1, double c2, double d2)
    {
        if (c1 > c2) (c1, d1, c2, d2) = (c2, d2, c1, d1);
        if (c1 == c2) return d1 == d2 ? d1 : 0;
        if (v <= c1) return d1;
        if (v >= c2) return d2;
        return d1 + (v - c1) * (d2 - d1) / (c2 - c1);
    }

    #endregion

    #endregion
}
