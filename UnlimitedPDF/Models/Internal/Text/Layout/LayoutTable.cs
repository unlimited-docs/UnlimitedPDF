using System.Collections.Concurrent;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Models.Internal.Text.Layout;

/// <summary>
/// One lookup of a GSUB or GPOS table: its type (extension lookups resolved per subtable), flags, mark filtering set,
/// and subtable offsets.
/// </summary>
internal sealed record LayoutLookup(int Index, int Type, int Flags, int MarkFilteringSet, int[] Subtables);

/// <summary>
/// A GSUB or GPOS table (D-003): the script list, feature list, lookup list, and FeatureVariations (which swap feature
/// lookups for variable-font instances). <see cref="FeatureLookups"/> returns the lookups a set of features uses for
/// a script, as the default language system defines them.
/// </summary>
internal sealed class LayoutTable
{
    #region Fields and Constants

    // Real fonts have far fewer; more is damaged or hostile (A06).
    private const int MaxRecords = 4096;

    private readonly int _scripts;
    private readonly int _features;
    private readonly int _lookups;
    private readonly int _featureVariations;

    // A font's tables are shared by every document and thread that draws with it, so the caches take concurrent use.
    private readonly ConcurrentDictionary<int, LayoutLookup> _lookupCache = new();
    private readonly ConcurrentDictionary<int, ulong[]?> _firstGlyphs = new();

    #endregion

    #region Constructors and Finalizers

    private LayoutTable(FontBytes data, string tag, int table, int scripts, int features, int lookups, int featureVariations)
    {
        Data = data;
        Tag = tag;
        Table = table;
        _scripts = scripts;
        _features = features;
        _lookups = lookups;
        _featureVariations = featureVariations;
        LookupCount = lookups == 0 ? 0 : Math.Min((int)data.U16(lookups), MaxRecords);
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads the font's <c>GSUB</c> or <c>GPOS</c> table, or returns null when it has none.
    /// </summary>
    public static LayoutTable? Read(OpenTypeFont font, string tag)
    {
        if (!font.HasTable(tag)) return null;
        var data = font.Data;
        var (table, length) = font.TableRange(tag);
        if (length < 10) throw FontBytes.Corrupt($"has a {tag} table that is too short");
        int minor = data.U16(table + 2);
        int variations = minor >= 1 && length >= 14 ? (int)Math.Min(table + (long)data.U32(table + 10), int.MaxValue) : table;
        return new LayoutTable(
            data,
            tag,
            table,
            LayoutData.Offset16(data, table, table + 4),
            LayoutData.Offset16(data, table, table + 6),
            LayoutData.Offset16(data, table, table + 8),
            variations == table ? 0 : variations);
    }

    /// <summary>
    /// Returns, for each requested feature tag the script's default language system lists (plus its required
    /// feature, reported under its own tag), the feature's lookup indexes.
    /// </summary>
    /// <param name="scriptTags">Script tags in order of preference, for example <c>dev2</c>, <c>deva</c>, <c>DFLT</c>.</param>
    /// <param name="featureTags">The features wanted.</param>
    /// <param name="coordinates">The instance's normalized coordinates, for FeatureVariations.</param>
    /// <param name="script">The script tag found, or null.</param>
    public Dictionary<string, List<int>> FeatureLookups(IReadOnlyList<string> scriptTags, ICollection<string> featureTags, double[] coordinates, out string? script)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        script = null;
        if (_scripts == 0 || _features == 0) return result;

        int langSys = FindLangSys(scriptTags, out script);
        if (langSys == 0) return result;

        var substitutes = FeatureSubstitutes(coordinates);
        int featureCount = Data.U16(_features);
        int required = Data.U16(langSys + 2);
        int count = Data.U16(langSys + 4);
        Data.Require(langSys + 6, 2L * count, "a language system");
        var indexes = Enumerable.Range(0, count).Select(i => (int)Data.U16(langSys + 6 + 2 * i)).ToList();
        if (required != 0xFFFF) indexes.Insert(0, required);

        foreach (int featureIndex in indexes)
        {
            if (featureIndex >= featureCount) throw FontBytes.Corrupt($"has a {Tag} language system that names a missing feature");
            int record = _features + 2 + 6 * featureIndex;
            string tag = Data.Tag(record);
            if (!featureTags.Contains(tag)) continue;
            int feature = substitutes.TryGetValue(featureIndex, out int alternate) ? alternate : _features + Data.U16(record + 4);
            int lookupCount = Data.U16(feature + 2);
            Data.Require(feature + 4, 2L * lookupCount, "a feature table");
            if (!result.TryGetValue(tag, out var list)) result[tag] = list = new List<int>();
            for (int i = 0; i < lookupCount; i++)
            {
                int lookup = Data.U16(feature + 4 + 2 * i);
                if (lookup < LookupCount && !list.Contains(lookup)) list.Add(lookup);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns lookup <paramref name="index"/>, resolving extension subtables to their real type and offsets.
    /// </summary>
    public LayoutLookup Lookup(int index)
    {
        if (_lookupCache.TryGetValue(index, out var cached)) return cached;
        if (index >= LookupCount) throw FontBytes.Corrupt($"uses a {Tag} lookup that does not exist");
        int lookup = _lookups + Data.U16(_lookups + 2 + 2 * index);
        int type = Data.U16(lookup);
        int flags = Data.U16(lookup + 2);
        int count = Data.U16(lookup + 4);
        if (count > MaxRecords) throw FontBytes.Corrupt($"has a {Tag} lookup with too many subtables");
        Data.Require(lookup + 6, 2L * count + 2, "a lookup");
        int markSet = (flags & 0x10) != 0 ? Data.U16(lookup + 6 + 2 * count) : -1;
        int extensionType = Tag == "GSUB" ? 7 : 9;

        var subtables = new int[count];
        int realType = type;
        for (int i = 0; i < count; i++)
        {
            int subtable = lookup + Data.U16(lookup + 6 + 2 * i);
            if (type == extensionType)
            {
                int innerType = Data.U16(subtable + 2);
                if (innerType == extensionType) throw FontBytes.Corrupt($"has a {Tag} extension that points at another extension");
                if (i > 0 && innerType != realType) throw FontBytes.Corrupt($"has a {Tag} extension lookup with mixed subtable types");
                realType = innerType;
                subtable = (int)Math.Min(subtable + (long)Data.U32(subtable + 4), int.MaxValue);
            }
            subtables[i] = subtable;
        }
        var result = new LayoutLookup(index, realType, flags, markSet, subtables);
        _lookupCache[index] = result;
        return result;
    }

    /// <summary>
    /// Returns the glyphs <paramref name="lookup"/> can apply at, one bit per glyph ID below
    /// <paramref name="glyphCount"/>: every glyph the first coverage table of one of its subtables lists. At any other
    /// glyph each subtable reads only what it reads for every glyph, then stops without applying, so
    /// <see cref="LayoutEngine"/> skips it. Returns null when the lookup has a subtable format this does not model,
    /// damaged data, or more coverage than a real font has (A06); its glyphs are then all tried, as before. Built once
    /// per lookup.
    /// </summary>
    public ulong[]? FirstGlyphs(LayoutLookup lookup, int glyphCount)
    {
        // Read first: this runs for every glyph and lookup, and a GetOrAdd factory would allocate its closure each time.
        if (_firstGlyphs.TryGetValue(lookup.Index, out var set)) return set;
        set = BuildFirstGlyphs(lookup, glyphCount);
        return _firstGlyphs.GetOrAdd(lookup.Index, set);
    }

    #endregion

    #region Private Methods

    private ulong[]? BuildFirstGlyphs(LayoutLookup lookup, int glyphCount)
    {
        if (glyphCount <= 0) return null;
        var set = new ulong[(glyphCount + 63) >> 6];
        long budget = 16L * glyphCount + 65_536;
        try
        {
            foreach (int subtable in lookup.Subtables)
            {
                int coverage = FirstCoverage(lookup.Type, subtable);
                if (coverage < 0) return null;
                if (coverage > 0 && !LayoutData.AddCoverage(Data, coverage, glyphCount, set, ref budget)) return null;
            }
            return set;
        }
        catch (PdfFontException)
        {
            // The engine meets the same damage when it reads the subtable, and reports it there.
            return null;
        }
    }

    // The offset of the coverage table a subtable checks the current glyph against first, after the same reads the
    // engine makes before it (LayoutEngine.Substitute and Position); 0 for a subtable that never applies; -1 for a
    // format this does not model.
    private int FirstCoverage(int type, int subtable)
    {
        int format = Data.U16(subtable);
        switch (Tag == "GSUB" ? type : type + 100)
        {
            case 1 or 101 or 102 when format is 1 or 2: // single substitution; single and pair adjustment
            case 2 or 4 or 8 or 104 or 105 or 106 when format == 1: // multiple, ligature, reverse chaining; mark attachment
            case 5 or 6 or 107 or 108 when format is 1 or 2: // contextual and chained contextual, by glyph or by class
                return subtable + Data.U16(subtable + 2);
            case 103 when format == 1: // cursive attachment
            {
                int coverage = subtable + Data.U16(subtable + 2);
                int count = Data.U16(subtable + 4);
                Data.Require(subtable + 6, 4L * count, "cursive entry and exit records");
                return coverage;
            }
            case 5 or 6 or 107 or 108 when format == 3: // contextual and chained contextual, by coverage
            {
                var rule = LayoutData.ReadCoverageRule(Data, subtable, chained: type is 6 or 8);
                return rule.InputCount == 0 ? 0 : subtable + Data.U16(rule.InputAt);
            }
            case 3: // alternate substitution: no shaping feature uses it, so it never applies
                return 0;
            default:
                return -1;
        }
    }

    private int FindLangSys(IReadOnlyList<string> scriptTags, out string? found)
    {
        int count = Data.U16(_scripts);
        if (count > MaxRecords) throw FontBytes.Corrupt($"has a {Tag} script list with too many scripts");
        Data.Require(_scripts + 2, 6L * count, "a script list");
        foreach (string wanted in scriptTags)
        {
            for (int i = 0; i < count; i++)
            {
                int record = _scripts + 2 + 6 * i;
                if (Data.Tag(record) != wanted) continue;
                int script = _scripts + Data.U16(record + 4);
                int langSys = LayoutData.Offset16(Data, script, script);
                if (langSys == 0) continue;
                found = wanted;
                return langSys;
            }
        }
        found = null;
        return 0;
    }

    // FeatureVariations: the first record whose every condition holds swaps in its alternate feature tables.
    private Dictionary<int, int> FeatureSubstitutes(double[] coordinates)
    {
        var substitutes = new Dictionary<int, int>();
        if (_featureVariations == 0) return substitutes;
        long count = Data.U32(_featureVariations + 4);
        if (count > MaxRecords) throw FontBytes.Corrupt($"has {Tag} feature variations with too many records");
        Data.Require(_featureVariations + 8, 8 * count, "feature variations");
        for (int r = 0; r < count; r++)
        {
            int record = _featureVariations + 8 + 8 * r;
            int conditions = (int)Math.Min(_featureVariations + (long)Data.U32(record), int.MaxValue);
            int substitution = (int)Math.Min(_featureVariations + (long)Data.U32(record + 4), int.MaxValue);
            if (!ConditionsHold(conditions, coordinates)) continue;

            int substitutionCount = Data.U16(substitution + 4);
            Data.Require(substitution + 6, 6L * substitutionCount, "a feature table substitution");
            for (int i = 0; i < substitutionCount; i++)
            {
                int entry = substitution + 6 + 6 * i;
                substitutes[Data.U16(entry)] = (int)Math.Min(substitution + (long)Data.U32(entry + 2), int.MaxValue);
            }
            break;
        }
        return substitutes;
    }

    private bool ConditionsHold(int conditionSet, double[] coordinates)
    {
        int count = Data.U16(conditionSet);
        Data.Require(conditionSet + 2, 4L * count, "a condition set");
        for (int i = 0; i < count; i++)
        {
            int condition = (int)Math.Min(conditionSet + (long)Data.U32(conditionSet + 2 + 4 * i), int.MaxValue);
            if (Data.U16(condition) != 1) return false; // unknown condition formats never hold
            int axis = Data.U16(condition + 2);
            double min = Data.I16(condition + 4) / 16384.0, max = Data.I16(condition + 6) / 16384.0;
            double value = axis < coordinates.Length ? coordinates[axis] : 0;
            if (value < min || value > max) return false;
        }
        return true;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public FontBytes Data { get; }

    public int LookupCount { get; }

    public int Table { get; }

    public string Tag { get; }

    #endregion
}
