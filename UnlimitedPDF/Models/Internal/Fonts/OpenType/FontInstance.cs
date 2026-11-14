using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

namespace UnlimitedPDF.Models.Internal.Fonts.OpenType;

/// <summary>
/// One drawable instance of a font (D-013): the advance widths, outlines, and metrics at fixed variation coordinates.
/// A static font, or a variable font at its default, uses the font's own data. Any other instance is computed
/// once, at load, for every glyph (TrueType outlines through <c>gvar</c>, advances through <c>HVAR</c> or the phantom
/// points, metrics through <c>MVAR</c>), so a damaged variation table fails at load. CFF and CFF2 glyphs are kept as
/// flattened CFF version 1 charstrings, ready for embedding.
/// </summary>
internal sealed class FontInstance
{
    #region Fields and Constants

    private readonly OpenTypeFont _font;
    private readonly int[] _advances;
    private readonly byte[]?[]? _glyphData;
    private readonly byte[][]? _charstrings;

    #endregion

    #region Constructors and Finalizers

    private FontInstance(OpenTypeFont font, double[] coordinates, int[] advances, byte[]?[]? glyphData, byte[][]? charstrings, (int, int, int, int) metrics)
    {
        _font = font;
        Coordinates = coordinates;
        _advances = advances;
        _glyphData = glyphData;
        _charstrings = charstrings;
        (Ascender, Descender, CapHeight, XHeight) = metrics;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Creates the instance of <paramref name="font"/> at <paramref name="coordinates"/>.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="axes">The font's axes, or null for a static font.</param>
    /// <param name="coordinates">Normalized coordinates, one per axis (empty for a static font).</param>
    /// <returns>The instance.</returns>
    public static FontInstance Create(OpenTypeFont font, VariationAxes? axes, double[] coordinates)
    {
        bool atDefault = coordinates.All(c => c == 0);
        int axisCount = axes?.Axes.Length ?? 0;
        var advances = Enumerable.Range(0, font.GlyphCount).Select(font.Advance).ToArray();
        int capHeight = font.CapHeight != 0 ? font.CapHeight : (int)(font.UnitsPerEm * 0.7);
        var metrics = (Ascender: (int)font.Ascender, Descender: (int)font.Descender, CapHeight: capHeight, XHeight: (int)font.XHeight);

        byte[][]? charstrings = null;
        if (font.Outlines == FontOutlines.Cff2)
        {
            var (offset, length) = font.TableRange("CFF2");
            var cff2 = Cff2Font.Read(font.Data, offset, length, font.GlyphCount, axisCount);
            charstrings = Enumerable.Range(0, font.GlyphCount).Select(g => cff2.Instance(g, coordinates)).ToArray();
        }
        else if (font.Outlines == FontOutlines.Cff)
        {
            charstrings = Enumerable.Range(0, font.GlyphCount).Select(g => font.Cff!.Flatten(g)).ToArray();
        }

        if (atDefault || axes is null) return new FontInstance(font, coordinates, advances, null, charstrings, metrics);

        var metricVariations = MetricsVariations.Read(font, axisCount);
        byte[]?[]? glyphData = null;
        if (font.Outlines == FontOutlines.TrueType)
        {
            var gvar = GlyphVariations.Read(font, axisCount);
            glyphData = new byte[]?[font.GlyphCount];
            for (int g = 0; g < font.GlyphCount; g++)
            {
                var (data, phantomAdvance) = InstanceGlyph(font, gvar, g, coordinates);
                glyphData[g] = data;
                double? hvar = metricVariations.AdvanceDelta(g, coordinates);
                advances[g] = hvar is double delta ? Math.Max(0, (int)Math.Round(font.Advance(g) + delta)) : phantomAdvance;
            }
        }
        else
        {
            for (int g = 0; g < font.GlyphCount; g++)
            {
                if (metricVariations.AdvanceDelta(g, coordinates) is double delta) advances[g] = Math.Max(0, (int)Math.Round(font.Advance(g) + delta));
            }
        }

        metrics = (
            (int)Math.Round(metrics.Ascender + metricVariations.MetricDelta("hasc", coordinates)),
            (int)Math.Round(metrics.Descender + metricVariations.MetricDelta("hdsc", coordinates)),
            (int)Math.Round(metrics.CapHeight + metricVariations.MetricDelta("cpht", coordinates)),
            (int)Math.Round(metrics.XHeight + metricVariations.MetricDelta("xhgt", coordinates)));
        return new FontInstance(font, coordinates, advances, glyphData, charstrings, metrics);
    }

    /// <summary>
    /// Returns a glyph's advance width in font units.
    /// </summary>
    public int Advance(int glyph) => _advances[glyph];

    /// <summary>
    /// Returns a CFF or CFF2 glyph as a flattened CFF version 1 charstring.
    /// </summary>
    public byte[] Charstring(int glyph) => _charstrings?[glyph] ?? throw new InvalidOperationException("The font has TrueType outlines.");

    /// <summary>
    /// Returns a TrueType glyph's <c>glyf</c> data at this instance (empty for an empty glyph).
    /// </summary>
    public byte[] GlyphData(int glyph)
    {
        if (_glyphData?[glyph] is { } instanced) return instanced;
        var (glyf, _) = _font.TableRange("glyf");
        var (offset, length) = _font.GlyphRange(glyph);
        return _font.Data.Slice(glyf + offset, length);
    }

    #endregion

    #region Private Methods

    // Applies gvar deltas to one glyph: outline points (or component offsets) and the four phantom points, whose
    // second point gives the new advance width.
    private static (byte[] Data, int Advance) InstanceGlyph(OpenTypeFont font, GlyphVariations? gvar, int glyph, double[] coordinates)
    {
        var decoded = TrueTypeGlyph.Decode(font, glyph);
        int advance = font.Advance(glyph);
        if (gvar is null) return (decoded.Encode(), advance);

        bool composite = decoded.Components.Length > 0;
        int outlinePoints = composite ? decoded.Components.Length : decoded.PointCount;
        var x = new double[outlinePoints + 4];
        var y = new double[outlinePoints + 4];
        for (int i = 0; i < outlinePoints; i++)
        {
            x[i] = composite ? decoded.Components[i].Argument1 : decoded.X[i];
            y[i] = composite ? decoded.Components[i].Argument2 : decoded.Y[i];
        }
        double left = decoded.Bounds[0] - font.LeftSideBearing(glyph);
        x[outlinePoints] = left;
        x[outlinePoints + 1] = left + advance;

        var (dx, dy) = gvar.Deltas(glyph, x, y, composite ? Array.Empty<int>() : decoded.ContourEnds, coordinates);
        if (composite)
        {
            decoded.Components = decoded.Components
                .Select((c, i) => c.HasOffsets ? c with { Argument1 = (int)Math.Round(c.Argument1 + dx[i]), Argument2 = (int)Math.Round(c.Argument2 + dy[i]) } : c)
                .ToArray();
        }
        else
        {
            for (int i = 0; i < outlinePoints; i++) { decoded.X[i] += dx[i]; decoded.Y[i] += dy[i]; }
        }
        double newLeft = left + dx[outlinePoints], newRight = left + advance + dx[outlinePoints + 1];
        return (decoded.Encode(), Math.Max(0, (int)Math.Round(newRight - newLeft)));
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public int Ascender { get; }

    public int CapHeight { get; }

    public double[] Coordinates { get; }

    public int Descender { get; }

    public int XHeight { get; }

    #endregion
}
