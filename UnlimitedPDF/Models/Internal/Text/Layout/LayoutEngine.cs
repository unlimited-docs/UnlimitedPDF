using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Models.Internal.Text.Layout;

/// <summary>
/// Applies GSUB and GPOS lookups to a glyph buffer (D-003): GSUB types 1, 2, 4, 5, 6, and 8 and GPOS types 1, 2, 4, 5,
/// 6, 7, and 8 (extension types 7 and 9 are resolved by <see cref="LayoutTable"/>), honoring lookup flags, mark
/// attachment classes, and mark filtering sets. Contextual lookups call nested lookups at most 16 levels deep, and all
/// work is counted against a budget of 64 operations per glyph; a font that exceeds either, or grows the text too far,
/// fails with <see cref="PdfFontException"/> (A06).
/// </summary>
internal sealed class LayoutEngine
{
    #region Fields and Constants

    public const int MaxNestingDepth = 16;
    public const int OperationsPerGlyph = 64;

    // Lookup flags.
    private const int IgnoreBaseGlyphs = 0x0002;
    private const int IgnoreLigatures = 0x0004;
    private const int IgnoreMarks = 0x0008;
    private const int UseMarkFilteringSet = 0x0010;

    private readonly LayoutTable _table;
    private readonly GlyphDefinitions? _gdef;
    private readonly List<ShapingGlyph> _buffer;
    private readonly int _maxLength;
    private readonly int _glyphCount;
    private long _budget;
    private int _nextLigatureId = 1;

    #endregion

    #region Constructors and Finalizers

    public LayoutEngine(LayoutTable table, GlyphDefinitions? gdef, List<ShapingGlyph> buffer, int glyphCount)
    {
        _glyphCount = glyphCount;
        _table = table;
        _gdef = gdef;
        _buffer = buffer;
        _budget = (long)OperationsPerGlyph * buffer.Count + 4096;
        _maxLength = 32 * buffer.Count + 256;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Applies one lookup to every glyph whose mask has a bit of <paramref name="mask"/>, left to right (right to left
    /// for reverse chaining substitution).
    /// </summary>
    public void Apply(int lookupIndex, ulong mask)
    {
        var lookup = _table.Lookup(lookupIndex);
        if (_table.Tag == "GSUB" && lookup.Type == 8)
        {
            for (int i = _buffer.Count - 1; i >= 0; i--)
            {
                if ((_buffer[i].Mask & mask) == 0 || Skip(_buffer[i], lookup)) continue;
                ApplyOnce(lookup, i, 0, out _);
            }
            return;
        }

        int position = 0;
        while (position < _buffer.Count)
        {
            var glyph = _buffer[position];
            if ((glyph.Mask & mask) != 0 && !Skip(glyph, lookup) && ApplyOnce(lookup, position, 0, out int next))
                position = Math.Max(next, position + 1);
            else
                position++;
        }
    }

    #endregion

    #region Private Methods

    private static (int X, int Y) Anchor(FontBytes data, int offset) => (data.I16(offset + 2), data.I16(offset + 4));

    private static int ValueSize(int format) => 2 * System.Numerics.BitOperations.PopCount((uint)(format & 0xFF));

    private void Spend()
    {
        if (--_budget < 0) throw FontBytes.Corrupt("has layout rules that do too much work for its text");
    }

    private bool Skip(ShapingGlyph glyph, LayoutLookup lookup)
    {
        int flags = lookup.Flags;
        switch (glyph.GlyphClass)
        {
            case 1: return (flags & IgnoreBaseGlyphs) != 0;
            case 2: return (flags & IgnoreLigatures) != 0;
            case 3:
                if ((flags & IgnoreMarks) != 0) return true;
                if ((flags & UseMarkFilteringSet) != 0) return _gdef is null || !_gdef.InMarkSet(lookup.MarkFilteringSet, glyph.Glyph);
                int attachType = flags >> 8;
                return attachType != 0 && (_gdef?.MarkAttachClass(glyph.Glyph) ?? 0) != attachType;
            default: return false;
        }
    }

    private int NextIndex(int from, LayoutLookup lookup)
    {
        for (int i = from + 1; i < _buffer.Count; i++) if (!Skip(_buffer[i], lookup)) return i;
        return -1;
    }

    private int PreviousIndex(int from, LayoutLookup lookup)
    {
        for (int i = from - 1; i >= 0; i--) if (!Skip(_buffer[i], lookup)) return i;
        return -1;
    }

    // Tries each subtable at one position; returns true when one applied, with the position to continue from.
    private bool ApplyOnce(LayoutLookup lookup, int position, int depth, out int next)
    {
        next = position + 1;
        foreach (int subtable in lookup.Subtables)
        {
            bool applied = _table.Tag == "GSUB"
                ? Substitute(lookup, subtable, position, depth, out next)
                : Position(lookup, subtable, position, depth, out next);
            if (applied)
            {
                Spend();
                return true;
            }
        }
        return false;
    }

    private void ClassFromGdef(ShapingGlyph glyph)
    {
        if (_gdef is { HasGlyphClasses: true }) glyph.GlyphClass = Math.Max(1, _gdef.GlyphClass(glyph.Glyph));
    }

    // A substitution must produce a glyph the font has (A06).
    private int Existing(int glyph) =>
        glyph < _glyphCount ? glyph : throw FontBytes.Corrupt($"has a {_table.Tag} rule that substitutes a glyph that does not exist ({glyph})");

    private void CheckLength()
    {
        if (_buffer.Count > _maxLength) throw FontBytes.Corrupt("has layout rules that grow its text too far");
    }

    #region Substitution

    private bool Substitute(LayoutLookup lookup, int subtable, int position, int depth, out int next)
    {
        var data = _table.Data;
        int format = data.U16(subtable);
        var glyph = _buffer[position];
        next = position + 1;
        switch (lookup.Type)
        {
            case 1:
            {
                int index = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), glyph.Glyph);
                if (index < 0) return false;
                if (format == 1) glyph.Glyph = Existing((glyph.Glyph + data.I16(subtable + 4)) & 0xFFFF);
                else if (format == 2)
                {
                    if (index >= data.U16(subtable + 4)) return false;
                    glyph.Glyph = Existing(data.U16(subtable + 6 + 2 * index));
                }
                else return false;
                ClassFromGdef(glyph);
                return true;
            }
            case 2:
            {
                int index = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), glyph.Glyph);
                if (format != 1 || index < 0 || index >= data.U16(subtable + 4)) return false;
                int sequence = subtable + data.U16(subtable + 6 + 2 * index);
                int count = data.U16(sequence);
                data.Require(sequence + 2, 2L * count, "a multiple substitution");
                var replacement = new List<ShapingGlyph>(count);
                for (int i = 0; i < count; i++)
                {
                    var part = glyph.WithGlyph(Existing(data.U16(sequence + 2 + 2 * i)));
                    ClassFromGdef(part);
                    replacement.Add(part);
                }
                _buffer.RemoveAt(position);
                _buffer.InsertRange(position, replacement);
                CheckLength();
                next = position + count;
                return true;
            }
            case 4:
                return Ligature(lookup, subtable, position, out next);
            case 5:
            case 6:
                return Context(lookup, subtable, position, depth, chained: lookup.Type == 6, out next);
            case 8:
                return ReverseChain(lookup, subtable, position);
            default:
                return false; // type 3 (alternates) is not used by any shaping feature here
        }
    }

    private bool Ligature(LayoutLookup lookup, int subtable, int position, out int next)
    {
        var data = _table.Data;
        next = position + 1;
        if (data.U16(subtable) != 1) return false;
        int index = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), _buffer[position].Glyph);
        if (index < 0 || index >= data.U16(subtable + 4)) return false;
        int set = subtable + data.U16(subtable + 6 + 2 * index);
        int count = data.U16(set);
        data.Require(set + 2, 2L * count, "a ligature set");
        for (int l = 0; l < count; l++)
        {
            int ligature = set + data.U16(set + 2 + 2 * l);
            int ligatureGlyph = Existing(data.U16(ligature));
            int components = data.U16(ligature + 2);
            if (components == 0) continue;
            data.Require(ligature + 4, 2L * (components - 1), "a ligature");

            var matched = new List<int> { position };
            int at = position;
            bool ok = true;
            for (int c = 1; c < components && ok; c++)
            {
                at = NextIndex(at, lookup);
                ok = at >= 0 && _buffer[at].Glyph == data.U16(ligature + 4 + 2 * (c - 1));
                if (ok) matched.Add(at);
            }
            if (!ok) continue;

            // The ligature replaces the first glyph; the other components go. Marks between components stay and
            // remember which component they follow, for mark-to-ligature positioning. Clusters merge.
            int id = _nextLigatureId++;
            int last = matched[^1];
            int cluster = Enumerable.Range(position, last - position + 1).Min(i => _buffer[i].Cluster);
            int component = 0;
            for (int i = position; i <= last; i++)
            {
                _buffer[i].Cluster = cluster;
                if (matched.Contains(i)) component++;
                else if (_buffer[i].IsMark) { _buffer[i].LigatureId = id; _buffer[i].LigatureComponent = component; }
            }
            var first = _buffer[position];
            first.Glyph = ligatureGlyph;
            first.LigatureId = id;
            first.LigatureComponent = 0;
            first.ComponentCount = components;
            first.GlyphClass = 2;
            ClassFromGdef(first);
            for (int k = matched.Count - 1; k >= 1; k--) _buffer.RemoveAt(matched[k]);
            next = position + 1;
            return true;
        }
        return false;
    }

    private bool ReverseChain(LayoutLookup lookup, int subtable, int position)
    {
        var data = _table.Data;
        if (data.U16(subtable) != 1) return false;
        int index = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), _buffer[position].Glyph);
        if (index < 0) return false;
        int backtrack = data.U16(subtable + 4);
        int p = subtable + 6;
        int at = position;
        for (int b = 0; b < backtrack; b++)
        {
            at = PreviousIndex(at, lookup);
            if (at < 0 || LayoutData.Coverage(data, subtable + data.U16(p + 2 * b), _buffer[at].Glyph) < 0) return false;
        }
        p += 2 * backtrack;
        int lookahead = data.U16(p);
        at = position;
        for (int a = 0; a < lookahead; a++)
        {
            at = NextIndex(at, lookup);
            if (at < 0 || LayoutData.Coverage(data, subtable + data.U16(p + 2 + 2 * a), _buffer[at].Glyph) < 0) return false;
        }
        p += 2 + 2 * lookahead;
        if (index >= data.U16(p)) return false;
        _buffer[position].Glyph = Existing(data.U16(p + 2 + 2 * index));
        ClassFromGdef(_buffer[position]);
        return true;
    }

    #endregion

    #region Positioning

    private bool Position(LayoutLookup lookup, int subtable, int position, int depth, out int next)
    {
        var data = _table.Data;
        int format = data.U16(subtable);
        var glyph = _buffer[position];
        next = position + 1;
        switch (lookup.Type)
        {
            case 1:
            {
                int index = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), glyph.Glyph);
                if (index < 0) return false;
                int valueFormat = data.U16(subtable + 4);
                if (format == 1) AddValue(glyph, subtable + 6, valueFormat);
                else if (format == 2)
                {
                    if (index >= data.U16(subtable + 6)) return false;
                    AddValue(glyph, subtable + 8 + index * ValueSize(valueFormat), valueFormat);
                }
                else return false;
                return true;
            }
            case 2:
                return Pair(lookup, subtable, position, out next);
            case 4:
            case 5:
            case 6:
                return Mark(lookup, subtable, position);
            case 7:
            case 8:
                return Context(lookup, subtable, position, depth, chained: lookup.Type == 8, out next);
            default:
                return false; // type 3 (cursive attachment) is for joining scripts, which are out of scope
        }
    }

    private void AddValue(ShapingGlyph glyph, int record, int format)
    {
        var data = _table.Data;
        int p = record;
        if ((format & 0x1) != 0) { glyph.XOffset += data.I16(p); p += 2; }
        if ((format & 0x2) != 0) { glyph.YOffset += data.I16(p); p += 2; }
        if ((format & 0x4) != 0) { glyph.XAdvance += data.I16(p); p += 2; }
        if ((format & 0x8) != 0) { glyph.YAdvance += data.I16(p); }
    }

    private bool Pair(LayoutLookup lookup, int subtable, int position, out int next)
    {
        var data = _table.Data;
        next = position + 1;
        int format = data.U16(subtable);
        int index = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), _buffer[position].Glyph);
        if (index < 0) return false;
        int second = NextIndex(position, lookup);
        if (second < 0) return false;
        int format1 = data.U16(subtable + 4), format2 = data.U16(subtable + 6);
        int size1 = ValueSize(format1), size2 = ValueSize(format2);

        int record;
        if (format == 1)
        {
            if (index >= data.U16(subtable + 8)) return false;
            int set = subtable + data.U16(subtable + 10 + 2 * index);
            int count = data.U16(set);
            int recordSize = 2 + size1 + size2;
            data.Require(set + 2, (long)recordSize * count, "a pair set");
            record = -1;
            int low = 0, high = count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int candidate = set + 2 + mid * recordSize;
                int glyph = data.U16(candidate);
                if (glyph < _buffer[second].Glyph) low = mid + 1;
                else if (glyph > _buffer[second].Glyph) high = mid - 1;
                else { record = candidate + 2; break; }
            }
            if (record < 0) return false;
        }
        else if (format == 2)
        {
            int class1 = LayoutData.ClassOf(data, subtable + data.U16(subtable + 8), _buffer[position].Glyph);
            int class2 = LayoutData.ClassOf(data, subtable + data.U16(subtable + 10), _buffer[second].Glyph);
            int class1Count = data.U16(subtable + 12), class2Count = data.U16(subtable + 14);
            if (class1 >= class1Count || class2 >= class2Count) return false;
            record = subtable + 16 + (class1 * class2Count + class2) * (size1 + size2);
            data.Require(record, size1 + size2, "a class pair");
        }
        else return false;

        AddValue(_buffer[position], record, format1);
        AddValue(_buffer[second], record + size1, format2);
        next = format2 != 0 ? second + 1 : second;
        return true;
    }

    // Mark-to-base (4), mark-to-ligature (5), and mark-to-mark (6) attachment.
    private bool Mark(LayoutLookup lookup, int subtable, int position)
    {
        var data = _table.Data;
        if (data.U16(subtable) != 1) return false;
        var mark = _buffer[position];
        int markIndex = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), mark.Glyph);
        if (markIndex < 0) return false;
        int classCount = data.U16(subtable + 6);
        int markArray = subtable + data.U16(subtable + 8);
        int targetArray = subtable + data.U16(subtable + 10);
        if (markIndex >= data.U16(markArray)) return false;
        int markClass = data.U16(markArray + 2 + 4 * markIndex);
        if (markClass >= classCount) return false;
        var markAnchor = Anchor(data, markArray + data.U16(markArray + 4 + 4 * markIndex));

        // The attachment target: for type 6 the previous mark (marks of other attachment classes skipped by the
        // lookup flags); otherwise the previous glyph that is not a mark.
        int target = -1;
        for (int i = position - 1; i >= 0; i--)
        {
            var candidate = _buffer[i];
            if (lookup.Type == 6)
            {
                if (Skip(candidate, lookup)) continue;
                if (candidate.IsMark && candidate.LigatureId == mark.LigatureId && candidate.LigatureComponent == mark.LigatureComponent) target = i;
                break;
            }
            if (!candidate.IsMark) { target = i; break; }
        }
        if (target < 0) return false;
        var targetGlyph = _buffer[target];
        int targetIndex = LayoutData.Coverage(data, subtable + data.U16(subtable + 4), targetGlyph.Glyph);
        if (targetIndex < 0 || targetIndex >= data.U16(targetArray)) return false;

        int anchorOffset;
        if (lookup.Type == 5)
        {
            int attach = targetArray + data.U16(targetArray + 2 + 2 * targetIndex);
            int components = data.U16(attach);
            if (components == 0) return false;
            int component = mark.LigatureId == targetGlyph.LigatureId && mark.LigatureComponent > 0 ? Math.Min(mark.LigatureComponent, components) - 1 : components - 1;
            int entry = attach + 2 + 2 * (component * classCount + markClass);
            data.Require(entry, 2, "a ligature anchor");
            anchorOffset = data.U16(entry) == 0 ? 0 : attach + data.U16(entry);
        }
        else
        {
            int entry = targetArray + 2 + 2 * (targetIndex * classCount + markClass);
            data.Require(entry, 2, "a base anchor");
            anchorOffset = data.U16(entry) == 0 ? 0 : targetArray + data.U16(entry);
        }
        if (anchorOffset == 0) return false;
        var targetAnchor = Anchor(data, anchorOffset);

        mark.AttachTo = target;
        mark.AttachDx = targetAnchor.X - markAnchor.X;
        mark.AttachDy = targetAnchor.Y - markAnchor.Y;
        return true;
    }

    #endregion

    #region Context

    // Contextual and chained contextual lookups (GSUB 5 and 6, GPOS 7 and 8), formats 1 to 3.
    private bool Context(LayoutLookup lookup, int subtable, int position, int depth, bool chained, out int next)
    {
        var data = _table.Data;
        int format = data.U16(subtable);
        next = position + 1;
        int glyph = _buffer[position].Glyph;

        if (format == 3)
        {
            int p = subtable + 2;
            int[] backtrack = Array.Empty<int>(), lookahead = Array.Empty<int>(), input;
            if (chained)
            {
                backtrack = Offsets(ref p, subtable);
                input = Offsets(ref p, subtable);
                lookahead = Offsets(ref p, subtable);
            }
            else
            {
                int inputCount = data.U16(p);
                int lookupCount = data.U16(p + 2);
                data.Require(p + 4, 2L * inputCount, "a context rule");
                input = Enumerable.Range(0, inputCount).Select(i => subtable + data.U16(p + 4 + 2 * i)).ToArray();
                p += 4 + 2 * inputCount;
                return input.Length > 0 && Match(lookup, position, input.Length, (k, g) => LayoutData.Coverage(data, input[k], g) >= 0, out var matchedNoChain)
                    && ApplyRecords(p, lookupCount, matchedNoChain, depth, out next);
            }
            if (input.Length == 0) return false;
            if (!Match(lookup, position, input.Length, (k, g) => LayoutData.Coverage(data, input[k], g) >= 0, out var matched)) return false;
            if (!MatchBack(lookup, position, backtrack.Length, (k, g) => LayoutData.Coverage(data, backtrack[k], g) >= 0)) return false;
            if (!MatchAhead(lookup, matched[^1], lookahead.Length, (k, g) => LayoutData.Coverage(data, lookahead[k], g) >= 0)) return false;
            int count = data.U16(p);
            return ApplyRecords(p + 2, count, matched, depth, out next);
        }

        int coverageIndex = LayoutData.Coverage(data, subtable + data.U16(subtable + 2), glyph);
        if (coverageIndex < 0) return false;

        if (format == 1)
        {
            int sets = data.U16(subtable + 4);
            if (coverageIndex >= sets) return false;
            int set = LayoutData.Offset16(data, subtable, subtable + 6 + 2 * coverageIndex);
            return set != 0 && RuleSet(lookup, set, position, depth, chained, (k, g, sequence) => g == sequence, null, out next);
        }
        if (format == 2)
        {
            int backtrackDef = chained ? LayoutData.Offset16(data, subtable, subtable + 4) : 0;
            int inputDef = LayoutData.Offset16(data, subtable, subtable + (chained ? 6 : 4));
            int lookaheadDef = chained ? LayoutData.Offset16(data, subtable, subtable + 8) : 0;
            int setsAt = subtable + (chained ? 10 : 6);
            int firstClass = LayoutData.ClassOf(data, inputDef, glyph);
            if (firstClass >= data.U16(setsAt)) return false;
            int set = LayoutData.Offset16(data, subtable, setsAt + 2 + 2 * firstClass);
            var classDefs = new[] { backtrackDef, inputDef, lookaheadDef };
            return set != 0 && RuleSet(lookup, set, position, depth, chained, (part, g, value) => LayoutData.ClassOf(data, classDefs[part], g) == value, classDefs, out next);
        }
        return false;
    }

    // A rule set of format 1 (glyph IDs) or format 2 (classes); the first rule that matches applies.
    private bool RuleSet(LayoutLookup lookup, int set, int position, int depth, bool chained, Func<int, int, int, bool> matches, int[]? classDefs, out int next)
    {
        var data = _table.Data;
        next = position + 1;
        int rules = data.U16(set);
        data.Require(set + 2, 2L * rules, "a rule set");
        for (int r = 0; r < rules; r++)
        {
            int rule = set + data.U16(set + 2 + 2 * r);
            int p = rule;
            int[] backtrack = Array.Empty<int>(), lookahead = Array.Empty<int>();
            if (chained) backtrack = Values(ref p);
            int inputCount = data.U16(p);
            if (inputCount == 0) { if (!chained) continue; }
            int lookupCount = 0;
            if (!chained) { lookupCount = data.U16(p + 2); p += 4; }
            else p += 2;
            data.Require(p, 2L * Math.Max(0, inputCount - 1), "a rule");
            var input = Enumerable.Range(0, Math.Max(0, inputCount - 1)).Select(i => (int)data.U16(p + 2 * i)).ToArray();
            p += 2 * input.Length;
            if (chained) { lookahead = Values(ref p); lookupCount = data.U16(p); p += 2; }
            if (inputCount == 0) continue;

            // Part 0 is backtrack, 1 input, 2 lookahead (only class-based rules look at the part).
            if (!Match(lookup, position, inputCount, (k, g) => k == 0 || matches(1, g, input[k - 1]), out var matched)) continue;
            if (!MatchBack(lookup, position, backtrack.Length, (k, g) => matches(0, g, backtrack[k]))) continue;
            if (!MatchAhead(lookup, matched[^1], lookahead.Length, (k, g) => matches(2, g, lookahead[k]))) continue;
            if (ApplyRecords(p, lookupCount, matched, depth, out next)) return true;
        }
        return false;
    }

    // Reads a count and that many 16-bit values.
    private int[] Values(ref int p)
    {
        var data = _table.Data;
        int start = p;
        int count = data.U16(start);
        data.Require(start + 2, 2L * count, "a rule");
        var values = new int[count];
        for (int i = 0; i < count; i++) values[i] = data.U16(start + 2 + 2 * i);
        p += 2 + 2 * count;
        return values;
    }

    // Reads a count and that many coverage offsets relative to the subtable.
    private int[] Offsets(ref int p, int subtable)
    {
        var data = _table.Data;
        int start = p;
        int count = data.U16(start);
        data.Require(start + 2, 2L * count, "a chained context rule");
        var offsets = new int[count];
        for (int i = 0; i < count; i++) offsets[i] = subtable + data.U16(start + 2 + 2 * i);
        p += 2 + 2 * count;
        return offsets;
    }

    private bool Match(LayoutLookup lookup, int position, int count, Func<int, int, bool> matches, out List<int> matched)
    {
        matched = new List<int> { position };
        if (!matches(0, _buffer[position].Glyph)) return false;
        int at = position;
        for (int k = 1; k < count; k++)
        {
            at = NextIndex(at, lookup);
            if (at < 0 || !matches(k, _buffer[at].Glyph)) return false;
            matched.Add(at);
        }
        return true;
    }

    private bool MatchBack(LayoutLookup lookup, int position, int count, Func<int, int, bool> matches)
    {
        int at = position;
        for (int k = 0; k < count; k++)
        {
            at = PreviousIndex(at, lookup);
            if (at < 0 || !matches(k, _buffer[at].Glyph)) return false;
        }
        return true;
    }

    private bool MatchAhead(LayoutLookup lookup, int last, int count, Func<int, int, bool> matches)
    {
        int at = last;
        for (int k = 0; k < count; k++)
        {
            at = NextIndex(at, lookup);
            if (at < 0 || !matches(k, _buffer[at].Glyph)) return false;
        }
        return true;
    }

    // Applies a rule's nested lookups at their sequence positions; later positions follow length changes.
    private bool ApplyRecords(int records, int count, List<int> matched, int depth, out int next)
    {
        var data = _table.Data;
        data.Require(records, 4L * count, "sequence lookup records");
        if (depth + 1 > MaxNestingDepth) throw FontBytes.Corrupt($"nests layout lookups deeper than {MaxNestingDepth} levels");
        var positions = new List<int>(matched);
        for (int r = 0; r < count; r++)
        {
            int sequenceIndex = data.U16(records + 4 * r);
            int lookupIndex = data.U16(records + 4 * r + 2);
            if (sequenceIndex >= positions.Count) continue;
            int at = positions[sequenceIndex];
            if (at >= _buffer.Count) continue;
            int before = _buffer.Count;
            Spend();
            ApplyOnce(_table.Lookup(lookupIndex), at, depth + 1, out _);
            int delta = _buffer.Count - before;
            if (delta == 0) continue;
            for (int i = 0; i < positions.Count; i++) if (positions[i] > at) positions[i] += delta;
        }
        next = Math.Max(positions[^1] + 1, matched[0] + 1);
        return true;
    }

    #endregion

    #endregion

    #endregion
}
