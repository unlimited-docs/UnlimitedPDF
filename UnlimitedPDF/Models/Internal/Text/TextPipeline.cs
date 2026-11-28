using UnlimitedPDF.Models.Internal.Text.Bidi;

namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// A piece of laid-out text in one font.
/// </summary>
internal abstract record TextSegment(string Text, double Width);

/// <summary>
/// Text drawn with the built-in Helvetica: characters no caller font has (WinAnsi characters draw, others become '?').
/// </summary>
internal sealed record StandardSegment(string Text, double Width) : TextSegment(Text, Width);

/// <summary>
/// Text shaped in a caller font: positioned glyphs, the text each glyph stands for in <c>ToUnicode</c>, and the glyph
/// ranges drawn inside an <c>ActualText</c> span because their glyphs do not read back as their text.
/// </summary>
internal sealed record FontSegment(PdfFont Font, ShapedRun Run, string[] GlyphTexts, IReadOnlyList<ActualTextSpan> Spans, string Text, double Width) : TextSegment(Text, Width);

/// <summary>
/// Glyphs <paramref name="Start"/> (inclusive) to <paramref name="End"/> (exclusive) of a shaped run, and the original
/// text they show.
/// </summary>
internal readonly record struct ActualTextSpan(int Start, int End, string Text);

/// <summary>
/// A line of text laid out in fonts: its segments in drawing order (left to right) and its total width in points. A
/// line the bidirectional algorithm reordered (WR-041) also keeps its text in logical order; RightToLeft is its
/// paragraph direction.
/// </summary>
internal sealed record LaidOutText(IReadOnlyList<TextSegment> Segments, double Width, string? LogicalText = null, bool RightToLeft = false)
{
    /// <summary>
    /// Gets the whole line's text, in logical order.
    /// </summary>
    public string Text => LogicalText ?? string.Concat(Segments.Select(s => s.Text));

    /// <summary>
    /// Gets whether the segments are not in logical order, so the line is drawn inside one <c>ActualText</c> span.
    /// </summary>
    public bool Reordered => LogicalText is not null;
}

/// <summary>
/// Lays out and draws a line of text with caller fonts (WR-039, D-007): each text element (grapheme cluster) takes
/// the first of the call's font and the document's fallback fonts that has every character of it, else the built-in
/// Helvetica; consecutive elements in one font and script form a run, shaped with the font's own rules; widths come
/// from the shaped glyphs (G-012). Glyphs are written as hex glyph IDs (A05).
/// </summary>
internal static class TextPipeline
{
    #region Fields and Constants

    // One string per code point below U+0800, filled as glyphs need them (equal strings, so a race is harmless).
    private static readonly string?[] SharedTexts = new string?[0x0800];

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> ScriptTagLists = new(StringComparer.Ordinal);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Appends a glyph ID as at least four uppercase hex digits, the digits <c>ToString("X4")</c> gives, without
    /// allocating a string.
    /// </summary>
    public static StringBuilder AppendGlyphId(StringBuilder sb, int glyph)
    {
        // Eight digits hold any int in hex, so the format always fits.
        Span<char> digits = stackalloc char[8];
        glyph.TryFormat(digits, out int written, "X4", CultureInfo.InvariantCulture);
        return sb.Append(digits.Slice(0, written));
    }

    /// <summary>
    /// Lays out <paramref name="text"/>, or returns null when it takes the built-in path unchanged: no font on the
    /// call and no fallback fonts (D-009 keeps those documents byte-identical).
    /// </summary>
    public static LaidOutText? Layout(string text, PdfFont? font, IReadOnlyList<PdfFont> fallback, double fontSize, int? baseLevel = null)
    {
        if (font is null && fallback.Count == 0) return null;
        var candidates = new List<PdfFont?>();
        if (font is not null) candidates.Add(font);
        candidates.AddRange(fallback.Where(f => !ReferenceEquals(f, font)));
        return Layout(text, candidates, fontSize, run => StandardText.Width(run, fontSize), baseLevel);
    }

    /// <summary>
    /// Lays out <paramref name="text"/> with fonts tried in order for each text element. A null candidate stands for
    /// the built-in font, which takes an element when it draws no character of it as '?'; an element no candidate
    /// takes is drawn with the built-in font too.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="candidates">The fonts in order; null for the built-in font.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="standardWidth">The width of a run drawn with the built-in font, in points.</param>
    /// <param name="baseLevel">The paragraph direction (0 left to right, 1 right to left), or null to take it from the
    /// first strong character (WR-041).</param>
    /// <returns>The laid-out text.</returns>
    public static LaidOutText Layout(string text, IReadOnlyList<PdfFont?> candidates, double fontSize, Func<string, double> standardWidth, int? baseLevel = null)
    {
        // Text with no right-to-left character, Arabic number, or bidirectional control, in a paragraph that is not
        // right to left, keeps the left-to-right path unchanged, so its output is byte-identical (WR-041).
        if (baseLevel != 1 && !NeedsBidi(text)) return LayoutLeftToRight(text, candidates, fontSize, standardWidth);
        return LayoutBidi(text, candidates, fontSize, standardWidth, baseLevel);
    }

    private static LaidOutText LayoutLeftToRight(string text, IReadOnlyList<PdfFont?> candidates, double fontSize, Func<string, double> standardWidth)
    {
        // Split into text elements, choose each one's font, and group runs by font and script. Every element joins a
        // run in text order, so a run is one range of the text.
        var runs = new List<(PdfFont? Font, string Script, int Start, int Length)>();
        for (int start = 0, length; start < text.Length; start += length)
        {
            length = StringInfo.GetNextTextElementLength(text, start);
            var element = text.AsSpan(start, length);
            var chosen = Choose(element, candidates);
            string? script = FirstScript(element);
            if (runs.Count > 0 && ReferenceEquals(runs[^1].Font, chosen) && (script is null || runs[^1].Script == script || runs[^1].Script == string.Empty))
            {
                var run = runs[^1];
                runs[^1] = (run.Font, script is not null && run.Script == string.Empty ? script : run.Script, run.Start, run.Length + length);
            }
            else
            {
                runs.Add((chosen, script ?? (runs.Count > 0 && ReferenceEquals(runs[^1].Font, chosen) ? runs[^1].Script : string.Empty), start, length));
            }
        }

        var segments = new List<TextSegment>(runs.Count);
        foreach (var (runFont, script, runStart, runLength) in runs)
        {
            string runText = text.Substring(runStart, runLength);
            if (runFont is null)
            {
                segments.Add(new StandardSegment(runText, standardWidth(runText)));
                continue;
            }
            var shaped = IndicScript.For(script) is not null ? IndicShaper.Shape(runFont, runText, script) : TextShaper.Shape(runFont, runText, ScriptTags(script));
            var spans = new List<ActualTextSpan>();
            var texts = GlyphTexts(runFont, shaped, runText, spans);
            segments.Add(new FontSegment(runFont, shaped, texts, spans, runText, shaped.Advance * fontSize / runFont.Font.UnitsPerEm));
        }
        return new LaidOutText(segments, segments.Sum(s => s.Width));
    }

    // Bidirectional layout (WR-041): levels from the algorithm; runs split by font, script, and level; each run shaped
    // in its direction (a right-to-left run comes back in visual order); runs ordered by rule L2. Bidirectional format
    // characters draw nothing; mirrored characters in right-to-left runs take their mirror glyph (TextShaper).
    private static LaidOutText LayoutBidi(string text, IReadOnlyList<PdfFont?> candidates, double fontSize, Func<string, double> standardWidth, int? baseLevel)
    {
        var paragraph = BidiParagraph.Create(text, baseLevel);
        var levels = paragraph.LineLevels(0, paragraph.Count);
        var runs = new List<(PdfFont? Font, string Script, byte Level, StringBuilder Text)>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (AllFormatCharacters(element)) continue;
            byte level = levels[paragraph.IndexOf(elements.ElementIndex)];
            var chosen = Choose(element, candidates);
            string? script = FirstScript(element);
            bool sameRun = runs.Count > 0 && ReferenceEquals(runs[^1].Font, chosen) && runs[^1].Level == level;
            if (sameRun && (script is null || runs[^1].Script == script || runs[^1].Script == string.Empty))
            {
                runs[^1].Text.Append(element);
                if (script is not null && runs[^1].Script == string.Empty) runs[^1] = (runs[^1].Font, script, level, runs[^1].Text);
            }
            else
            {
                runs.Add((chosen, script ?? (sameRun ? runs[^1].Script : string.Empty), level, new StringBuilder(element)));
            }
        }

        var segments = new TextSegment[runs.Count];
        var runLevels = new byte[runs.Count];
        for (int r = 0; r < runs.Count; r++)
        {
            var (runFont, script, level, builder) = runs[r];
            string runText = builder.ToString();
            runLevels[r] = level;
            bool rightToLeft = (level & 1) == 1;
            if (runFont is null)
            {
                // The built-in font draws only Western European characters; a right-to-left run of them reads reversed.
                string drawn = rightToLeft ? Reverse(runText) : runText;
                segments[r] = new StandardSegment(drawn, standardWidth(drawn));
                continue;
            }
            var shaped = IndicScript.For(script) is not null
                ? IndicShaper.Shape(runFont, runText, script)
                : script == "arab"
                    ? ArabicShaper.Shape(runFont, runText, ScriptTags(script), rightToLeft)
                    : TextShaper.Shape(runFont, runText, ScriptTags(script), rightToLeft);
            var spans = new List<ActualTextSpan>();
            var texts = GlyphTexts(runFont, shaped, runText, spans);
            segments[r] = new FontSegment(runFont, shaped, texts, spans, runText, shaped.Advance * fontSize / runFont.Font.UnitsPerEm);
        }

        bool reordered = runLevels.Any(l => (l & 1) == 1);
        BidiParagraph.Reverse(segments, runLevels);
        return new LaidOutText(segments, segments.Sum(s => s.Width), reordered ? text : null, paragraph.BaseLevel == 1);
    }

    // Whether the text needs the bidirectional algorithm: a right-to-left letter, an Arabic number, or a bidirectional
    // control or mark.
    internal static bool NeedsBidi(string text)
    {
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            int codePoint = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
            if (codePoint < 0x0590) continue;
            if (BidiParagraph.IsFormatCharacter(codePoint) || BidiData.ClassOf(codePoint) is BidiClass.R or BidiClass.AL or BidiClass.AN) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether <paramref name="text"/> holds an Arabic-script letter, whose joined letters take no letter spacing
    /// (WR-041, AC-12).
    /// </summary>
    internal static bool IsArabic(string text)
    {
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            int codePoint = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
            if (codePoint >= 0x0600 && ScriptOf(codePoint) == "arab") return true;
        }
        return false;
    }

    private static string Reverse(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) elements.Add(enumerator.GetTextElement());
        elements.Reverse();
        return string.Concat(elements);
    }

    /// <summary>
    /// Appends to <paramref name="sb"/> the operators that draw laid-out text with its baseline starting at
    /// (<paramref name="x"/>, <paramref name="baseline"/>), in black, recording fonts and glyphs in
    /// <paramref name="sink"/>. No line break follows them.
    /// </summary>
    public static void Draw(StringBuilder sb, LaidOutText layout, double x, double baseline, double fontSize, PageDrawSink sink)
    {
        // One text object for the whole line, so a line in several fonts stays one line of text for copy and search.
        // BT/ET = text object, rg = black fill; each segment sets its font (Tf) and position (Tm).
        sb.Append("BT 0 0 0 rg");
        // A reordered line is one ActualText span holding its logical text, so copy and search read it in order
        // (WR-041); its segments then draw without spans of their own. BDC = begin marked content.
        if (layout.Reordered) sb.Append(" /Span << /ActualText <FEFF").Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(layout.Text))).Append("> >> BDC");
        double pen = x;
        foreach (var segment in layout.Segments)
        {
            if (segment is FontSegment shaped) DrawShaped(sb, shaped, pen, baseline, fontSize, sink, spans: !layout.Reordered);
            else
            {
                // Tj = show text in the built-in font
                var (resource, literal) = StandardText.Prepare(segment.Text, sink, layout.Text);
                sb.Append(" /").Append(resource).Append(' ').Append(PdfNumber.Format(fontSize)).Append(" Tf 1 0 0 1 ")
                  .Append(PdfNumber.Format(pen)).Append(' ').Append(PdfNumber.Format(baseline)).Append(" Tm (").Append(literal).Append(") Tj");
            }
            pen += segment.Width;
        }
        // EMC = end marked content
        if (layout.Reordered) sb.Append(" EMC");
        sb.Append(" ET");
    }

    #endregion

    #region Private Methods

    // The code point at text[i], and how many chars it takes (a lone surrogate is a code point of its own).
    private static int CodePointAt(string text, int i, out int length) => CodePointAt(text.AsSpan(), i, out length);

    private static int CodePointAt(ReadOnlySpan<char> text, int i, out int length)
    {
        bool pair = i + 1 < text.Length && char.IsSurrogatePair(text[i], text[i + 1]);
        length = pair ? 2 : 1;
        return pair ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
    }

    // The first candidate that takes a text element: a caller font that has every character of it, or the built-in
    // font (null) when it draws no character of it as '?'; null when none does, so the built-in font draws it.
    private static PdfFont? Choose(ReadOnlySpan<char> element, IReadOnlyList<PdfFont?> candidates)
    {
        for (int c = 0; c < candidates.Count; c++)
        {
            var font = candidates[c];
            if (font is null ? !StandardText.IsMissing(element) : DrawsAll(font, element)) return font;
        }
        return null;
    }

    private static bool DrawsAll(PdfFont font, ReadOnlySpan<char> element)
    {
        for (int i = 0, length; i < element.Length; i += length)
            if (!font.Draws(CodePointAt(element, i, out length))) return false;
        return true;
    }

    private static bool AllFormatCharacters(string element)
    {
        for (int i = 0, length; i < element.Length; i += length)
            if (!BidiParagraph.IsFormatCharacter(CodePointAt(element, i, out length))) return false;
        return true;
    }

    // The script of a text element: that of its first character that has one.
    private static string? FirstScript(ReadOnlySpan<char> element)
    {
        for (int i = 0, length; i < element.Length; i += length)
            if (ScriptOf(CodePointAt(element, i, out length)) is { } script) return script;
        return null;
    }

    // The script of a character for run splitting and shaping; null for characters every script shares (spaces,
    // digits, punctuation), which join the run around them.
    private static string? ScriptOf(int codePoint) => codePoint switch
    {
        0x0964 or 0x0965 => null, // danda and double danda: Bengali, Gurmukhi, Oriya, and others use them too (WR-039)
        // Arabic comma, semicolon, question mark, tatweel, and Arabic-Indic digits are shared with other scripts (Thaana
        // among them) and join the run around them (WR-039).
        0x060C or 0x061B or 0x061F or 0x0640 or >= 0x0660 and <= 0x0669 => null,
        >= 0x0900 and <= 0x097F or >= 0xA8E0 and <= 0xA8FF => "deva",
        >= 0x0980 and <= 0x09FF => "beng",
        >= 0x0A00 and <= 0x0A7F => "guru",
        >= 0x0A80 and <= 0x0AFF => "gujr",
        >= 0x0B00 and <= 0x0B7F => "orya",
        >= 0x0B80 and <= 0x0BFF => "taml",
        >= 0x0C00 and <= 0x0C7F => "telu",
        >= 0x0C80 and <= 0x0CFF => "knda",
        >= 0x0D00 and <= 0x0D7F => "mlym",
        >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= 0x00C0 and <= 0x024F => "latn",
        >= 0x0370 and <= 0x03FF or >= 0x1F00 and <= 0x1FFF => "grek",
        >= 0x0400 and <= 0x052F or >= 0x1C80 and <= 0x1C8F or >= 0x2DE0 and <= 0x2DFF or >= 0xA640 and <= 0xA69F => "cyrl",
        >= 0x0531 and <= 0x058F or >= 0xFB13 and <= 0xFB17 => "armn",
        >= 0x10A0 and <= 0x10FF or >= 0x1C90 and <= 0x1CBF or >= 0x2D00 and <= 0x2D2F => "geor",
        >= 0x0591 and <= 0x05F4 or >= 0xFB1D and <= 0xFB4F => "hebr",
        >= 0x0780 and <= 0x07BF => "thaa",
        >= 0x0600 and <= 0x06FF or >= 0x0750 and <= 0x077F or >= 0x0870 and <= 0x089F or >= 0x08A0 and <= 0x08FF
            or >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF => "arab",
        _ => null
    };

    // Greek, Cyrillic, Armenian, and Georgian take their own script's features first (QF-26112401). Latin comes last,
    // after the defaults, for fonts that list their features under Latin only, as these scripts used before.
    // One list per script, built on first use: the scripts are the fixed set ScriptOf returns, so the cache stays small.
    // Callers only read the lists.
    internal static string[] ScriptTags(string script) => ScriptTagLists.GetOrAdd(script, static s => IndicScript.For(s) is { } indic
        ? new[] { indic.Version2Tag, indic.Version1Tag, "DFLT", "dflt" }
        : s is "grek" or "cyrl" or "armn" or "geor" or "hebr" or "thaa" or "arab"
            ? new[] { s, "DFLT", "dflt", "latn" }
            : new[] { "latn", "DFLT", "dflt" });

    // The text of each glyph for ToUnicode, and the spans that need ActualText. Glyphs are taken in groups: the
    // smallest runs of glyphs that hold every glyph of their clusters (a reordered syllable is one group). A glyph
    // the font maps a character to stands for that character (the lowest non-control code point, so a glyph means
    // the same wherever it is drawn); a formed glyph (ligature, half form, reph) stands for the group's characters
    // that no character glyph accounts for. A group drawn unchanged (one glyph per character, each meaning its
    // character, in text order) reads back through ToUnicode alone; every other group gets an ActualText span with
    // its original text (D-005).
    private static string[] GlyphTexts(PdfFont font, ShapedRun run, string text, List<ActualTextSpan> spans)
    {
        var glyphs = run.Glyphs;
        // The run's distinct clusters in order.
        var clusters = new int[glyphs.Count];
        for (int i = 0; i < glyphs.Count; i++) clusters[i] = glyphs[i].Cluster;
        Array.Sort(clusters);
        int distinct = 0;
        for (int i = 0; i < clusters.Length; i++) if (distinct == 0 || clusters[i] != clusters[distinct - 1]) clusters[distinct++] = clusters[i];
        // The last glyph of each cluster, by cluster (clusters are character indexes, so an array of the largest + 1).
        var last = new int[distinct == 0 ? 0 : clusters[distinct - 1] + 1];
        for (int i = 0; i < glyphs.Count; i++) last[glyphs[i].Cluster] = i;

        // Each group's meanings are worked out in place in the result.
        var texts = new string?[glyphs.Count];
        int at = 0;
        while (at < glyphs.Count)
        {
            int end = last[glyphs[at].Cluster] + 1;
            for (int j = at; j < end; j++) end = Math.Max(end, last[glyphs[j].Cluster] + 1);

            int first = int.MaxValue, final = int.MinValue;
            for (int j = at; j < end; j++) (first, final) = (Math.Min(first, glyphs[j].Cluster), Math.Max(final, glyphs[j].Cluster));
            // final is a cluster of the run, so it is a distinct cluster; the next one ends the group's text (linear
            // overall, A06).
            int index = Array.BinarySearch(clusters, 0, distinct, final);
            int next = index + 1 < distinct ? clusters[index + 1] : text.Length;
            int groupLength = Math.Max(0, next - first);
            var group = text.AsSpan(first, groupLength);

            var meanings = texts.AsSpan(at, end - at);
            for (int j = at; j < end; j++)
            {
                int codePoint = font.Font.CharacterMap.CodePointFor(glyphs[j].Glyph);
                meanings[j - at] = codePoint < 0 ? null : CodePointText(codePoint);
            }

            if (!ReadsBack(group, meanings))
            {
                string groupText = group.ToString();
                var characters = Characters(groupText);
                // The characters no character glyph accounts for, in text order, go to the formed glyphs.
                var accounted = new Dictionary<string, int>();
                foreach (var meaning in meanings) if (meaning is not null) accounted[meaning] = accounted.GetValueOrDefault(meaning) + 1;
                var rest = new List<string>(characters.Count);
                foreach (string character in characters)
                {
                    if (accounted.TryGetValue(character, out int count) && count > 0) accounted[character] = count - 1;
                    else rest.Add(character);
                }
                string formed = rest.Count > 0 ? string.Concat(rest) : groupText;
                for (int k = 0; k < meanings.Length; k++) meanings[k] ??= formed;
                spans.Add(new ActualTextSpan(at, end, groupText));
            }
            at = end;
        }
        // Every group either reads back (each glyph has its meaning) or gave its formed text to the rest.
        return texts!;
    }

    // Whether a group is drawn unchanged: one glyph per character of its text, each meaning that character, in order.
    private static bool ReadsBack(ReadOnlySpan<char> group, ReadOnlySpan<string?> meanings)
    {
        int k = 0;
        for (int i = 0; i < group.Length; k++)
        {
            int size = i + 1 < group.Length && char.IsSurrogatePair(group[i], group[i + 1]) ? 2 : 1;
            if (k >= meanings.Length || meanings[k] is not { } meaning || !group.Slice(i, size).SequenceEqual(meaning.AsSpan())) return false;
            i += size;
        }
        return k == meanings.Length;
    }

    // The text of a code point. Glyphs below U+0800 (no surrogates among them) share one string per code point,
    // so drawing common text does not allocate one per glyph.
    private static string CodePointText(int codePoint)
    {
        if (codePoint >= SharedTexts.Length) return char.ConvertFromUtf32(codePoint);
        return SharedTexts[codePoint] ??= ((char)codePoint).ToString();
    }

    private static List<string> Characters(string text)
    {
        var characters = new List<string>(text.Length);
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            characters.Add(text.Substring(i, char.IsSurrogatePair(text, i) ? 2 : 1));
        return characters;
    }

    /// <summary>
    /// Returns the number of clusters in a shaped segment: the places extra letter spacing goes, one after each.
    /// </summary>
    public static int Clusters(FontSegment segment)
    {
        var glyphs = segment.Run.Glyphs;
        int count = 0;
        for (int i = 0; i < glyphs.Count; i++) if (i + 1 == glyphs.Count || glyphs[i + 1].Cluster != glyphs[i].Cluster) count++;
        return count;
    }

    /// <summary>
    /// Writes the operators that draw a shaped segment inside a text object: TJ arrays of hex glyph IDs whose spacing
    /// numbers move the pen from where the previous glyph's /W width leaves it to the glyph's shaped position; a change
    /// of vertical offset starts a new array after Ts. A span whose glyphs do not read back as their text is wrapped in
    /// a marked-content span with its ActualText. Extra letter spacing goes after each cluster and extra word spacing
    /// after each space, in points; a skew slants the text (the text matrix's horizontal shear).
    /// </summary>
    public static void DrawShaped(StringBuilder sb, FontSegment segment, double x, double baseline, double fontSize, PageDrawSink sink, double letterSpacing = 0, double wordSpacing = 0, double skew = 0, bool spans = true)
    {
        var font = segment.Font;
        double unitsPerEm = font.Font.UnitsPerEm;
        // Right-to-left letters the library does not shape are drawn, but recorded like missing characters (WR-039).
        foreach (string unshaped in RightToLeftSupport.UnshapedElements(segment.Text)) sink.RecordMissing(unshaped, segment.Text);
        if (segment.Run.Glyphs.Count == 0) return;
        // The face is looked up once per run; each glyph is recorded with the first text drawn for it.
        var used = sink.FaceGlyphs(font, out string resource);
        for (int i = 0; i < segment.Run.Glyphs.Count; i++) used.TryAdd(segment.Run.Glyphs[i].Glyph, segment.GlyphTexts[i]);
        var instance = font.Instance;

        // Tf = font and size, Tm = text position, TJ = show glyphs with spacing, Ts = text rise for glyphs placed above
        // or below the baseline
        sb.Append(" /").Append(resource).Append(' ').Append(PdfNumber.Format(fontSize)).Append(" Tf 1 0 ").Append(PdfNumber.Format(skew)).Append(" 1 ")
          .Append(PdfNumber.Format(x)).Append(' ').Append(PdfNumber.Format(baseline)).Append(" Tm");
        double pen = 0; // in 1/1000 em
        double shift = 0; // extra spacing so far, in 1/1000 em
        int rise = 0;
        bool open = false;
        int span = 0;
        var glyphs = segment.Run.Glyphs;
        for (int i = 0; i < glyphs.Count; i++)
        {
            var glyph = glyphs[i];
            if (spans && span < segment.Spans.Count && segment.Spans[span].Start == i)
            {
                // BDC = begin marked content: a /Span whose ActualText (UTF-16BE hex) replaces the glyphs' own text
                if (open) { sb.Append("] TJ"); open = false; }
                sb.Append(" /Span << /ActualText <FEFF").Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(segment.Spans[span].Text))).Append("> >> BDC");
            }
            if (glyph.Y != rise)
            {
                if (open) { sb.Append("] TJ"); open = false; }
                rise = glyph.Y;
                sb.Append(' ').Append(PdfNumber.Format(Math.Round(rise * fontSize / unitsPerEm, 4))).Append(" Ts");
            }
            bool first = !open;
            if (!open) { sb.Append(" ["); open = true; }
            double target = glyph.X * 1000.0 / unitsPerEm + shift;
            double adjust = Math.Round(pen - target, 3);
            if (Math.Abs(adjust) >= 0.001)
            {
                if (!first) sb.Append(' ');
                sb.Append(PdfNumber.Format(adjust));
                first = false;
            }
            if (!first) sb.Append(' ');
            AppendGlyphId(sb.Append('<'), glyph.Glyph).Append('>');
            pen = target + Math.Round(instance.Advance(glyph.Glyph) * 1000.0 / unitsPerEm);
            if (i + 1 == glyphs.Count || glyphs[i + 1].Cluster != glyph.Cluster)
            {
                if (letterSpacing != 0) shift += letterSpacing * 1000 / fontSize;
                if (wordSpacing != 0 && glyph.Cluster < segment.Text.Length && segment.Text[glyph.Cluster] == ' ') shift += wordSpacing * 1000 / fontSize;
            }
            if (spans && span < segment.Spans.Count && segment.Spans[span].End == i + 1)
            {
                // EMC = end marked content
                if (open) { sb.Append("] TJ"); open = false; }
                sb.Append(" EMC");
                span++;
            }
        }
        if (open) sb.Append("] TJ");
        if (rise != 0) sb.Append(" 0 Ts");
    }

    #endregion

    #endregion
}
