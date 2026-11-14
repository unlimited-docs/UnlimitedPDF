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
/// Text shaped in a caller font: positioned glyphs, and the text each glyph stands for (the first glyph of a cluster
/// carries the cluster's text).
/// </summary>
internal sealed record FontSegment(PdfFont Font, ShapedRun Run, string[] GlyphTexts, string Text, double Width) : TextSegment(Text, Width);

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
        var candidates = new List<PdfFont>();
        if (font is not null) candidates.Add(font);
        candidates.AddRange(fallback.Where(f => !ReferenceEquals(f, font)));

        // Split into text elements, choose each one's font, and group runs by font and script.
        var runs = new List<(PdfFont? Font, string Script, StringBuilder Text)>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            var codePoints = CodePoints(element).ToList();
            var chosen = candidates.FirstOrDefault(f => codePoints.All(cp => f.Font.CharacterMap.GlyphFor(cp) != 0));
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
                segments.Add(new StandardSegment(runText, StandardText.Width(runText, fontSize)));
                continue;
            }
            var shaped = script is "deva" or "telu" or "taml" ? IndicShaper.Shape(runFont, runText, script) : TextShaper.Shape(runFont, runText, ScriptTags(script));
            segments.Add(new FontSegment(runFont, shaped, GlyphTexts(runFont, shaped, runText), runText, shaped.Advance * fontSize / runFont.Font.UnitsPerEm));
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

    // The first glyph of each cluster carries the cluster's text: from its first character to the next cluster. A
    // cluster drawn unchanged (one glyph per character, each the character's own glyph, in text order) gives each
    // glyph its own character instead, so a glyph keeps one meaning wherever it is drawn.
    private static string[] GlyphTexts(PdfFont font, ShapedRun run, string text)
    {
        var starts = run.Glyphs.Select(g => g.Cluster).Distinct().OrderBy(c => c).ToList();
        var texts = new string[run.Glyphs.Count];
        var seen = new HashSet<int>();
        int i = 0;
        while (i < run.Glyphs.Count)
        {
            int cluster = run.Glyphs[i].Cluster;
            int end = i + 1;
            while (end < run.Glyphs.Count && run.Glyphs[end].Cluster == cluster) end++;
            if (!seen.Add(cluster))
            {
                for (int k = i; k < end; k++) texts[k] = string.Empty;
                i = end;
                continue;
            }

            int next = starts.FirstOrDefault(s => s > cluster, text.Length);
            string clusterText = text.Substring(cluster, Math.Max(0, next - cluster));
            var characters = Characters(clusterText);
            bool unchanged = characters.Count == end - i
                && characters.Select((c, k) => font.Font.CharacterMap.GlyphFor(char.ConvertToUtf32(c, 0)) == run.Glyphs[i + k].Glyph).All(same => same);
            for (int k = i; k < end; k++) texts[k] = unchanged ? characters[k - i] : k == i ? clusterText : string.Empty;
            i = end;
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

    // Draws a shaped run: TJ arrays of hex glyph IDs whose spacing numbers move the pen from where the previous glyph's
    // /W width leaves it to the glyph's shaped position; a change of vertical offset starts a new array after Ts.
    private static void DrawShaped(StringBuilder sb, FontSegment segment, double x, double baseline, double fontSize, PageDrawSink sink)
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
        int rise = 0;
        bool open = false;
        foreach (var glyph in segment.Run.Glyphs)
        {
            if (glyph.Y != rise)
            {
                if (open) { sb.Append("] TJ"); open = false; }
                rise = glyph.Y;
                sb.Append(' ').Append(PdfNumber.Format(Math.Round(rise * fontSize / unitsPerEm, 4))).Append(" Ts");
            }
            bool first = !open;
            if (!open) { sb.Append(" ["); open = true; }
            double target = glyph.X * 1000.0 / unitsPerEm;
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
        }
        if (open) sb.Append("] TJ");
        if (rise != 0) sb.Append(" 0 Ts");
    }

    #endregion

    #endregion
}
