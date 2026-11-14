namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

/// <summary>
/// Advance-width variations (<c>HVAR</c>) and font-wide metric variations (<c>MVAR</c>) of a variable font (D-013).
/// </summary>
internal sealed class MetricsVariations
{
    #region Fields and Constants

    private readonly FontBytes _data;
    private readonly ItemVariationStore? _advanceStore;
    private readonly DeltaSetMap? _advanceMap;
    private readonly ItemVariationStore? _metricStore;
    private readonly Dictionary<string, (int Outer, int Inner)> _metricRecords = new(StringComparer.Ordinal);

    #endregion

    #region Constructors and Finalizers

    private MetricsVariations(FontBytes data, ItemVariationStore? advanceStore, DeltaSetMap? advanceMap, ItemVariationStore? metricStore)
    {
        _data = data;
        _advanceStore = advanceStore;
        _advanceMap = advanceMap;
        _metricStore = metricStore;
    }

    #endregion

    #region Methods

    #region Public Methods

    public static MetricsVariations Read(OpenTypeFont font, int axisCount)
    {
        var data = font.Data;
        ItemVariationStore? advanceStore = null, metricStore = null;
        DeltaSetMap? advanceMap = null;

        if (font.HasTable("HVAR"))
        {
            var (hvar, length) = font.TableRange("HVAR");
            if (length < 20) throw FontBytes.Corrupt("has an HVAR table that is too short");
            int limit = hvar + length;
            advanceStore = ItemVariationStore.Read(data, hvar + (int)Math.Min(data.U32(hvar + 4), int.MaxValue / 2), limit, axisCount);
            long mapOffset = data.U32(hvar + 8);
            if (mapOffset != 0) advanceMap = DeltaSetMap.Read(data, (int)Math.Min(hvar + mapOffset, int.MaxValue), limit);
        }

        var variations = new MetricsVariations(data, advanceStore, advanceMap, null);
        if (font.HasTable("MVAR"))
        {
            var (mvar, length) = font.TableRange("MVAR");
            if (length < 12) throw FontBytes.Corrupt("has an MVAR table that is too short");
            int limit = mvar + length;
            int recordSize = data.U16(mvar + 6), count = data.U16(mvar + 8), storeOffset = data.U16(mvar + 10);
            if (recordSize < 8 || 12L + (long)recordSize * count > length) throw FontBytes.Corrupt("has a truncated MVAR table");
            if (storeOffset != 0) metricStore = ItemVariationStore.Read(data, mvar + storeOffset, limit, axisCount);
            variations = new MetricsVariations(data, advanceStore, advanceMap, metricStore);
            for (int i = 0; i < count; i++)
            {
                int record = mvar + 12 + i * recordSize;
                variations._metricRecords[data.Tag(record)] = (data.U16(record + 4), data.U16(record + 6));
            }
        }
        return variations;
    }

    /// <summary>
    /// Returns the advance-width delta of a glyph from <c>HVAR</c>, or null when the font has no <c>HVAR</c>.
    /// </summary>
    public double? AdvanceDelta(int glyph, double[] coordinates)
    {
        if (_advanceStore is null) return null;
        var (outer, inner) = _advanceMap?.Map(glyph) ?? (0, glyph);
        return _advanceStore.Delta(_data, outer, inner, coordinates);
    }

    /// <summary>
    /// Returns the delta of a font-wide metric (for example <c>hasc</c>, <c>hdsc</c>, <c>cpht</c>, <c>xhgt</c>), or 0.
    /// </summary>
    public double MetricDelta(string tag, double[] coordinates) =>
        _metricStore is not null && _metricRecords.TryGetValue(tag, out var item) ? _metricStore.Delta(_data, item.Outer, item.Inner, coordinates) : 0;

    #endregion

    #endregion

    #region Nested Types

    // A DeltaSetIndexMap: glyph ID to (outer, inner) delta-set index; glyphs past the end use the last entry.
    private sealed class DeltaSetMap
    {
        private readonly (int Outer, int Inner)[] _entries;

        private DeltaSetMap((int, int)[] entries) => _entries = entries;

        public static DeltaSetMap Read(FontBytes data, int offset, int limit)
        {
            if (offset + 4 > limit) throw FontBytes.Corrupt("has a truncated delta-set index map");
            int format = data.U8(offset), entryFormat = data.U8(offset + 1);
            long count = format == 0 ? data.U16(offset + 2) : format == 1 ? data.U32(offset + 2) : throw FontBytes.Corrupt("has an unknown delta-set index map format");
            int header = format == 0 ? 4 : 6;
            int size = ((entryFormat >> 4) & 3) + 1, innerBits = (entryFormat & 0xF) + 1;
            if (count == 0 || count > 65536 || offset + header + count * size > limit) throw FontBytes.Corrupt("has an invalid delta-set index map");
            var entries = new (int, int)[count];
            for (int i = 0; i < count; i++)
            {
                long value = 0;
                for (int b = 0; b < size; b++) value = (value << 8) | data.U8(offset + header + i * size + b);
                entries[i] = ((int)(value >> innerBits), (int)(value & ((1L << innerBits) - 1)));
            }
            return new DeltaSetMap(entries);
        }

        public (int Outer, int Inner) Map(int glyph) => _entries[Math.Min(glyph, _entries.Length - 1)];
    }

    #endregion
}
