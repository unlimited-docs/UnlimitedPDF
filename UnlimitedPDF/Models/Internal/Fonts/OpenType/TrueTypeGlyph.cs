namespace UnlimitedPDF.Models.Internal.Fonts.OpenType;

/// <summary>
/// One component of a composite TrueType glyph.
/// </summary>
/// <param name="Flags">The component flags, without WE_HAVE_INSTRUCTIONS.</param>
/// <param name="Glyph">The component's glyph ID.</param>
/// <param name="Argument1">The x offset (or a point number when ARGS_ARE_XY_VALUES is clear).</param>
/// <param name="Argument2">The y offset (or a point number).</param>
/// <param name="Transform">The scale or 2x2 transform bytes, kept as read.</param>
internal sealed record GlyphComponent(ushort Flags, int Glyph, int Argument1, int Argument2, byte[] Transform)
{
    public bool HasOffsets => (Flags & TrueTypeGlyph.ArgsAreXyValues) != 0;
}

/// <summary>
/// A decoded TrueType glyph: either contours of points or components (D-013). Variable-font instancing moves the
/// points or component offsets, then <see cref="Encode"/> writes the glyph back in <c>glyf</c> form without
/// instructions, which PDF viewers do not run.
/// </summary>
internal sealed class TrueTypeGlyph
{
    #region Fields and Constants

    public const ushort ArgsAreXyValues = 0x0002;

    private const ushort ArgsAreWords = 0x0001;
    private const ushort MoreComponents = 0x0020;
    private const ushort WeHaveAScale = 0x0008;
    private const ushort WeHaveAnXAndYScale = 0x0040;
    private const ushort WeHaveATwoByTwo = 0x0080;
    private const ushort WeHaveInstructions = 0x0100;

    private const byte OnCurve = 0x01;
    private const byte XShort = 0x02;
    private const byte YShort = 0x04;
    private const byte Repeat = 0x08;
    private const byte XSameOrPositive = 0x10;
    private const byte YSameOrPositive = 0x20;
    private const byte OverlapSimple = 0x40;

    // A simple glyph cannot have more points than the 16-bit end-point indexes allow.
    private const int MaxPoints = 65535;

    #endregion

    #region Constructors and Finalizers

    private TrueTypeGlyph(short[] bounds, int[] contourEnds, double[] x, double[] y, bool[] onCurve, bool overlap, GlyphComponent[] components)
    {
        Bounds = bounds;
        ContourEnds = contourEnds;
        X = x;
        Y = y;
        OnCurvePoints = onCurve;
        Overlap = overlap;
        Components = components;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Decodes glyph <paramref name="glyph"/> of a TrueType font; an empty glyph has no contours and no components.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="glyph">The glyph ID.</param>
    /// <returns>The decoded glyph.</returns>
    public static TrueTypeGlyph Decode(OpenTypeFont font, int glyph)
    {
        var data = font.Data;
        var (glyfTable, _) = font.TableRange("glyf");
        var (offset, length) = font.GlyphRange(glyph);
        if (length == 0) return new TrueTypeGlyph(new short[4], Array.Empty<int>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<bool>(), false, Array.Empty<GlyphComponent>());

        int start = glyfTable + offset, end = start + length;
        int contours = data.I16(start);
        var bounds = new[] { data.I16(start + 2), data.I16(start + 4), data.I16(start + 6), data.I16(start + 8) };
        return contours >= 0 ? DecodeSimple(data, glyph, start + 10, end, contours, bounds) : DecodeComposite(data, glyph, start + 10, end, bounds);
    }

    /// <summary>
    /// Writes the glyph in <c>glyf</c> form: 16-bit coordinates, no instructions; a simple glyph's bounds are
    /// recomputed from its points.
    /// </summary>
    /// <returns>The glyph bytes, empty for an empty glyph.</returns>
    public byte[] Encode()
    {
        if (ContourEnds.Length == 0 && Components.Length == 0) return Array.Empty<byte>();
        var w = new List<byte>();
        void U16(int v) { w.Add((byte)(v >> 8)); w.Add((byte)v); }

        if (Components.Length > 0)
        {
            U16(-1);
            foreach (short b in Bounds) U16(b);
            for (int i = 0; i < Components.Length; i++)
            {
                var c = Components[i];
                ushort flags = (ushort)(c.Flags & ~WeHaveInstructions);
                if (c.HasOffsets) flags |= ArgsAreWords;
                flags = i < Components.Length - 1 ? (ushort)(flags | MoreComponents) : (ushort)(flags & ~MoreComponents);
                U16(flags);
                U16(c.Glyph);
                if ((flags & ArgsAreWords) != 0) { U16(c.Argument1); U16(c.Argument2); }
                else { w.Add((byte)c.Argument1); w.Add((byte)c.Argument2); }
                w.AddRange(c.Transform);
            }
            return w.ToArray();
        }

        var xs = X.Select(Round).ToArray();
        var ys = Y.Select(Round).ToArray();
        U16(ContourEnds.Length);
        U16(xs.Min()); U16(ys.Min()); U16(xs.Max()); U16(ys.Max());
        foreach (int e in ContourEnds) U16(e);
        U16(0); // no instructions
        for (int i = 0; i < xs.Length; i++) w.Add((byte)((OnCurvePoints[i] ? OnCurve : 0) | (i == 0 && Overlap ? OverlapSimple : 0)));
        int last = 0;
        foreach (int v in xs) { U16(v - last); last = v; }
        last = 0;
        foreach (int v in ys) { U16(v - last); last = v; }
        return w.ToArray();
    }

    #endregion

    #region Private Methods

    private static short Round(double value) => (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(value, MidpointRounding.AwayFromZero)));

    private static TrueTypeGlyph DecodeSimple(FontBytes data, int glyph, int p, int end, int contours, short[] bounds)
    {
        var ends = new int[contours];
        int previous = -1;
        for (int i = 0; i < contours; i++)
        {
            ends[i] = data.U16(p + 2 * i);
            if (ends[i] <= previous) throw FontBytes.Corrupt($"has invalid contour ends in glyph {glyph}");
            previous = ends[i];
        }
        p += 2 * contours;
        int points = contours == 0 ? 0 : ends[^1] + 1;
        if (points > MaxPoints) throw FontBytes.Corrupt($"has too many points in glyph {glyph}");

        int instructions = data.U16(p);
        p += 2 + instructions;

        var flags = new byte[points];
        for (int i = 0; i < points;)
        {
            if (p >= end) throw FontBytes.Corrupt($"has truncated flags in glyph {glyph}");
            byte f = data.U8(p++);
            flags[i++] = f;
            if ((f & Repeat) != 0)
            {
                if (p >= end) throw FontBytes.Corrupt($"has truncated flags in glyph {glyph}");
                int repeat = data.U8(p++);
                if (i + repeat > points) throw FontBytes.Corrupt($"has too many flags in glyph {glyph}");
                for (int r = 0; r < repeat; r++) flags[i++] = f;
            }
        }

        var x = new double[points];
        var y = new double[points];
        p = ReadCoordinates(data, glyph, p, end, flags, x, XShort, XSameOrPositive);
        ReadCoordinates(data, glyph, p, end, flags, y, YShort, YSameOrPositive);
        var onCurve = flags.Select(f => (f & OnCurve) != 0).ToArray();
        return new TrueTypeGlyph(bounds, ends, x, y, onCurve, points > 0 && (flags[0] & OverlapSimple) != 0, Array.Empty<GlyphComponent>());
    }

    private static int ReadCoordinates(FontBytes data, int glyph, int p, int end, byte[] flags, double[] values, byte shortFlag, byte sameFlag)
    {
        int value = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            byte f = flags[i];
            if ((f & shortFlag) != 0)
            {
                if (p >= end) throw FontBytes.Corrupt($"has truncated coordinates in glyph {glyph}");
                int delta = data.U8(p++);
                value += (f & sameFlag) != 0 ? delta : -delta;
            }
            else if ((f & sameFlag) == 0)
            {
                if (p + 2 > end) throw FontBytes.Corrupt($"has truncated coordinates in glyph {glyph}");
                value += data.I16(p);
                p += 2;
            }
            values[i] = value;
        }
        return p;
    }

    private static TrueTypeGlyph DecodeComposite(FontBytes data, int glyph, int p, int end, short[] bounds)
    {
        var components = new List<GlyphComponent>();
        while (true)
        {
            if (p + 4 > end) throw FontBytes.Corrupt($"has a damaged composite glyph {glyph}");
            ushort flags = data.U16(p);
            int component = data.U16(p + 2);
            p += 4;
            int a1, a2;
            bool xy = (flags & ArgsAreXyValues) != 0;
            if ((flags & ArgsAreWords) != 0)
            {
                if (p + 4 > end) throw FontBytes.Corrupt($"has a damaged composite glyph {glyph}");
                a1 = xy ? data.I16(p) : data.U16(p);
                a2 = xy ? data.I16(p + 2) : data.U16(p + 2);
                p += 4;
            }
            else
            {
                if (p + 2 > end) throw FontBytes.Corrupt($"has a damaged composite glyph {glyph}");
                a1 = xy ? (sbyte)data.U8(p) : data.U8(p);
                a2 = xy ? (sbyte)data.U8(p + 1) : data.U8(p + 1);
                p += 2;
            }
            int transform = (flags & WeHaveAScale) != 0 ? 2 : (flags & WeHaveAnXAndYScale) != 0 ? 4 : (flags & WeHaveATwoByTwo) != 0 ? 8 : 0;
            if (p + transform > end) throw FontBytes.Corrupt($"has a damaged composite glyph {glyph}");
            var transformBytes = new byte[transform];
            for (int i = 0; i < transform; i++) transformBytes[i] = data.U8(p + i);
            p += transform;
            components.Add(new GlyphComponent((ushort)(flags & ~WeHaveInstructions & ~MoreComponents), component, a1, a2, transformBytes));
            if ((flags & MoreComponents) == 0) break;
        }
        return new TrueTypeGlyph(bounds, Array.Empty<int>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<bool>(), false, components.ToArray());
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the glyph's stored bounds: xMin, yMin, xMax, yMax.
    /// </summary>
    public short[] Bounds { get; }

    public GlyphComponent[] Components { get; set; }

    /// <summary>
    /// Gets the index of each contour's last point.
    /// </summary>
    public int[] ContourEnds { get; }

    public bool[] OnCurvePoints { get; }

    public bool Overlap { get; }

    public int PointCount => X.Length;

    public double[] X { get; }

    public double[] Y { get; }

    #endregion
}
