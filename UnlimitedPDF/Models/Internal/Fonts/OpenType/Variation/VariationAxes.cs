namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

/// <summary>
/// One <c>fvar</c> axis.
/// </summary>
internal sealed record VariationAxis(string Tag, double Minimum, double Default, double Maximum);

/// <summary>
/// The variation axes of a variable font (<c>fvar</c>) and their optional <c>avar</c> segment maps (D-013). Turns a
/// weight into normalized coordinates: -1 to 1 per axis, 0 at each axis default, rounded to 2.14 fixed point as the
/// OpenType specification requires. Axes other than weight stay at their defaults.
/// </summary>
internal sealed class VariationAxes
{
    #region Fields and Constants

    // A real font has a handful of axes; more than this is damaged or hostile.
    public const int MaxAxes = 64;

    private readonly (double From, double To)[]?[] _segmentMaps;

    #endregion

    #region Constructors and Finalizers

    private VariationAxes(VariationAxis[] axes, (double, double)[]?[] segmentMaps)
    {
        Axes = axes;
        _segmentMaps = segmentMaps;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads the font's axes, or returns null when it has no <c>fvar</c> table.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The axes, or null.</returns>
    public static VariationAxes? Read(OpenTypeFont font)
    {
        if (!font.HasTable("fvar")) return null;
        var data = font.Data;
        var (fvar, length) = font.TableRange("fvar");
        if (length < 16) throw FontBytes.Corrupt("has an fvar table that is too short");
        int axesOffset = data.U16(fvar + 4);
        int count = data.U16(fvar + 8);
        int size = data.U16(fvar + 10);
        if (count == 0 || count > MaxAxes || size < 20) throw FontBytes.Corrupt("has an invalid fvar table");
        if ((long)axesOffset + (long)count * size > length) throw FontBytes.Corrupt("has a truncated fvar table");

        var axes = new VariationAxis[count];
        for (int i = 0; i < count; i++)
        {
            int record = fvar + axesOffset + i * size;
            double min = data.I32(record + 4) / 65536.0, def = data.I32(record + 8) / 65536.0, max = data.I32(record + 12) / 65536.0;
            if (!(min <= def && def <= max)) throw FontBytes.Corrupt("has an fvar axis whose default is outside its range");
            axes[i] = new VariationAxis(data.Tag(record), min, def, max);
        }
        return new VariationAxes(axes, ReadSegmentMaps(font, count));
    }

    /// <summary>
    /// Returns the normalized coordinates for a weight: the weight axis set (clamped to its range), every other axis
    /// at its default.
    /// </summary>
    /// <param name="weight">The weight, or null for the default instance.</param>
    /// <returns>One normalized coordinate per axis.</returns>
    public double[] Normalize(double? weight)
    {
        var coordinates = new double[Axes.Length];
        for (int i = 0; i < Axes.Length; i++)
        {
            var axis = Axes[i];
            if (axis.Tag != "wght" || weight is not double w) continue;
            double v = Math.Max(axis.Minimum, Math.Min(axis.Maximum, w));
            double n = v < axis.Default ? -(axis.Default - v) / (axis.Default - axis.Minimum)
                : v > axis.Default ? (v - axis.Default) / (axis.Maximum - axis.Default)
                : 0;
            if (_segmentMaps[i] is { } map) n = Map(map, n);
            coordinates[i] = Math.Round(n * 16384) / 16384; // F2Dot14
        }
        return coordinates;
    }

    /// <summary>
    /// Returns the weight axis, or null when the font has none.
    /// </summary>
    public VariationAxis? WeightAxis => Axes.FirstOrDefault(a => a.Tag == "wght");

    #endregion

    #region Private Methods

    private static (double, double)[]?[] ReadSegmentMaps(OpenTypeFont font, int axisCount)
    {
        var maps = new (double, double)[]?[axisCount];
        if (!font.HasTable("avar")) return maps;
        var data = font.Data;
        var (avar, length) = font.TableRange("avar");
        if (length < 8) throw FontBytes.Corrupt("has an avar table that is too short");
        int count = data.U16(avar + 6);
        if (count != axisCount) throw FontBytes.Corrupt("has an avar table whose axis count differs from fvar");

        int p = avar + 8, end = avar + length;
        for (int axis = 0; axis < count; axis++)
        {
            if (p + 2 > end) throw FontBytes.Corrupt("has a truncated avar table");
            int pairs = data.U16(p);
            p += 2;
            if (p + 4L * pairs > end) throw FontBytes.Corrupt("has a truncated avar table");
            var map = new (double, double)[pairs];
            for (int i = 0; i < pairs; i++) map[i] = (data.I16(p + 4 * i) / 16384.0, data.I16(p + 4 * i + 2) / 16384.0);
            p += 4 * pairs;
            // A map that is not increasing, or has only the required -1, 0, 1 identity points, changes nothing.
            if (pairs >= 3 && map.Zip(map.Skip(1), (a, b) => a.Item1 < b.Item1).All(ok => ok)) maps[axis] = map;
        }
        return maps;
    }

    private static double Map((double From, double To)[] map, double value)
    {
        if (value <= map[0].From) return map[0].To;
        for (int i = 1; i < map.Length; i++)
        {
            if (value <= map[i].From)
            {
                var (f0, t0) = map[i - 1];
                var (f1, t1) = map[i];
                return t0 + (value - f0) * (t1 - t0) / (f1 - f0);
            }
        }
        return map[^1].To;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public VariationAxis[] Axes { get; }

    #endregion
}
