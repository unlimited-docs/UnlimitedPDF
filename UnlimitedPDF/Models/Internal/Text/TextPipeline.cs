using System.Globalization;

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
/// A line of text laid out in fonts: its segments in reading order and its total width in points.
/// </summary>
internal sealed record LaidOutText(IReadOnlyList<TextSegment> Segments, double Width)
{
    /// <summary>
    /// Gets the whole line's text.
    /// </summary>
    public string Text => string.Concat(Segments.Select(s => s.Text));
}

/// <summary>
/// Lays out and draws a line of text with caller fonts (WR-039, D-007): each text element (grapheme cluster) takes
/// the first of the call's font and the document's fallback fonts that has every character of it, else the built-in
/// Helvetica; consecutive elements in one font and script form a run, shaped with the font's own rules; widths come
/// from the shaped glyphs (G-012). Glyphs are written as hex glyph IDs (A05).
/// </summary>
internal static class TextPipeline
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Lays out <paramref name="text"/>, or returns null when it takes the built-in path unchanged: no font on the
    /// call and no fallback fonts (D-009 keeps those documents byte-identical).
    /// </summary>
    public static LaidOutText? Layout(string text, PdfFont? font, IReadOnlyList<PdfFont> fallback, double fontSize)
    {
        if (font is null && fallback.Count == 0) return null;
        var candidates = new List<PdfFont?>();
        if (font is not null) candidates.Add(font);
        candidates.AddRange(fallback.Where(f => !ReferenceEquals(f, font)));
        return Layout(text, candidates, fontSize, run => StandardText.Width(run, fontSize));
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
    /// <returns>The laid-out text.</returns>
    public static LaidOutText Layout(string text, IReadOnlyList<PdfFont?> candidates, double fontSize, Func<string, double> standardWidth)
    {
        // Split into text elements, choose each one's font, and group runs by font and script.
        var runs = new List<(PdfFont? Font, string Script, StringBuilder Text)>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            var codePoints = CodePoints(element).ToList();
            var chosen = candidates.FirstOrDefault(f => f is null ? !StandardText.IsMissing(element) : codePoints.All(f.Draws));
            string? script = codePoints.Select(ScriptOf).FirstOrDefault(s => s is not null);
            if (runs.Count > 0 && ReferenceEquals(runs[^1].Font, chosen) && (script is null || runs[^1].Script == script || runs[^1].Script == string.Empty))
            {
                runs[^1].Text.Append(element);
                if (script is not null && runs[^1].Script == string.Empty) runs[^1] = (runs[^1].Font, script, runs[^1].Text);
            }
            else
            {
                runs.Add((chosen, script ?? (runs.Count > 0 && ReferenceEquals(runs[^1].Font, chosen) ? runs[^1].Script : string.Empty), new StringBuilder(element)));
            }
        }

        var segments = new List<TextSegment>();
        foreach (var (runFont, script, builder) in runs)
        {
            string runText = builder.ToString();
            if (runFont is null)
            {
                segments.Add(new StandardSegment(runText, standardWidth(runText)));
                continue;
            }
            var shaped = script is "deva" or "telu" or "taml" ? IndicShaper.Shape(runFont, runText, script) : TextShaper.Shape(runFont, runText, ScriptTags(script));
            var spans = new List<ActualTextSpan>();
            var texts = GlyphTexts(runFont, shaped, runText, spans);
            segments.Add(new FontSegment(runFont, shaped, texts, spans, runText, shaped.Advance * fontSize / runFont.Font.UnitsPerEm));
        }
        return new LaidOutText(segments, segments.Sum(s => s.Width));
    }

    /// <summary>
    /// Returns the operators that draw laid-out text with its baseline starting at (<paramref name="x"/>,
    /// <paramref name="baseline"/>), in black, recording fonts and glyphs in <paramref name="sink"/>.
    /// </summary>
    public static string Draw(LaidOutText layout, double x, double baseline, double fontSize, PageDrawSink sink)
    {
        // One text object for the whole line, so a line in several fonts stays one line of text for copy and search.
        // BT/ET = text object, rg = black fill; each segment sets its font (Tf) and position (Tm).
        var sb = new StringBuilder("BT 0 0 0 rg");
        double pen = x;
        foreach (var segment in layout.Segments)
        {
            if (segment is FontSegment shaped) DrawShaped(sb, shaped, pen, baseline, fontSize, sink);
            else
            {
                // Tj = show text in the built-in font
                var (resource, literal) = StandardText.Prepare(segment.Text, sink, layout.Text);
                sb.Append(" /").Append(resource).Append(' ').Append(PdfNumber.Format(fontSize)).Append(" Tf 1 0 0 1 ")
                  .Append(PdfNumber.Format(pen)).Append(' ').Append(PdfNumber.Format(baseline)).Append(" Tm (").Append(literal).Append(") Tj");
            }
            pen += segment.Width;
        }
        return sb.Append(" ET").ToString();
    }

    #endregion

    #region Private Methods

    private static IEnumerable<int> CodePoints(string text)
    {
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            yield return char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
    }

    // The script of a character for run splitting and shaping; null for characters every script shares (spaces,
    // digits, punctuation), which join the run around them.
    private static string? ScriptOf(int codePoint) => codePoint switch
    {
        >= 0x0900 and <= 0x097F or >= 0xA8E0 and <= 0xA8FF => "deva",
        >= 0x0B80 and <= 0x0BFF => "taml",
        >= 0x0C00 and <= 0x0C7F => "telu",
        >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= 0x00C0 and <= 0x024F => "latn",
        _ => null
    };

    private static string[] ScriptTags(string script) => script switch
    {
        "deva" => new[] { "dev2", "deva", "DFLT", "dflt" },
        "telu" => new[] { "tel2", "telu", "DFLT", "dflt" },
        "taml" => new[] { "tml2", "taml", "DFLT", "dflt" },
        _ => new[] { "latn", "DFLT", "dflt" }
    };

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
        var starts = glyphs.Select(g => g.Cluster).Distinct().OrderBy(c => c).ToList();
        var last = new Dictionary<int, int>();
        for (int i = 0; i < glyphs.Count; i++) last[glyphs[i].Cluster] = i;

        var texts = new string[glyphs.Count];
        int at = 0;
        while (at < glyphs.Count)
        {
            int end = last[glyphs[at].Cluster] + 1;
            for (int j = at; j < end; j++) end = Math.Max(end, last[glyphs[j].Cluster] + 1);

            int first = int.MaxValue, final = int.MinValue;
            for (int j = at; j < end; j++) (first, final) = (Math.Min(first, glyphs[j].Cluster), Math.Max(final, glyphs[j].Cluster));
            int next = starts.FirstOrDefault(c => c > final, text.Length);
            string groupText = text.Substring(first, Math.Max(0, next - first));
            var characters = Characters(groupText);

            var meanings = new string?[end - at];
            for (int j = at; j < end; j++)
            {
                int codePoint = font.Font.CharacterMap.CodePointFor(glyphs[j].Glyph);
                meanings[j - at] = codePoint < 0 ? null : char.ConvertFromUtf32(codePoint);
            }

            bool unchanged = characters.Count == end - at && characters.Select((c, k) => meanings[k] == c).All(same => same);
            if (!unchanged)
            {
                // The characters no character glyph accounts for, in text order, go to the formed glyphs.
                var rest = new List<string>(characters);
                foreach (var meaning in meanings) if (meaning is not null) rest.Remove(meaning);
                string formed = rest.Count > 0 ? string.Concat(rest) : groupText;
                for (int k = 0; k < meanings.Length; k++) meanings[k] ??= formed;
                spans.Add(new ActualTextSpan(at, end, groupText));
            }
            for (int k = 0; k < meanings.Length; k++) texts[at + k] = meanings[k]!;
            at = end;
        }
        return texts;
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
    /// after each space, in points.
    /// </summary>
    public static void DrawShaped(StringBuilder sb, FontSegment segment, double x, double baseline, double fontSize, PageDrawSink sink, double letterSpacing = 0, double wordSpacing = 0)
    {
        var font = segment.Font;
        double unitsPerEm = font.Font.UnitsPerEm;
        string resource = string.Empty;
        for (int i = 0; i < segment.Run.Glyphs.Count; i++) resource = sink.FaceResource(font, segment.Run.Glyphs[i].Glyph, segment.GlyphTexts[i]);
        if (segment.Run.Glyphs.Count == 0) return;

        // Tf = font and size, Tm = text position, TJ = show glyphs with spacing, Ts = text rise for glyphs placed above
        // or below the baseline
        sb.Append(" /").Append(resource).Append(' ').Append(PdfNumber.Format(fontSize)).Append(" Tf 1 0 0 1 ")
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
            if (span < segment.Spans.Count && segment.Spans[span].Start == i)
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
            sb.Append('<').Append(glyph.Glyph.ToString("X4", CultureInfo.InvariantCulture)).Append('>');
            pen = target + Math.Round(font.Instance.Advance(glyph.Glyph) * 1000.0 / unitsPerEm);
            if (i + 1 == glyphs.Count || glyphs[i + 1].Cluster != glyph.Cluster)
            {
                if (letterSpacing != 0) shift += letterSpacing * 1000 / fontSize;
                if (wordSpacing != 0 && glyph.Cluster < segment.Text.Length && segment.Text[glyph.Cluster] == ' ') shift += wordSpacing * 1000 / fontSize;
            }
            if (span < segment.Spans.Count && segment.Spans[span].End == i + 1)
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
