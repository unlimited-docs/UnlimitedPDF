using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Html.Css;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// Chooses fonts for HTML text (WR-039, D-011, D-012, D-013): each text element takes the first font in its
/// <c>font-family</c> list that has it (a registered caller family gives its face nearest the text's weight and
/// style; a built-in family name gives the standard font), then the fallback fonts, then the standard font, which
/// draws Western European (WinAnsi) characters and '?' for the rest. Without registered fonts and fallback fonts,
/// HTML text keeps the standard-font path unchanged (<see cref="Simple"/>). A face that is not bold or not italic
/// where the text asks for it is a missing face: reported, or synthesized when the load opted in.
/// </summary>
internal sealed class HtmlFonts
{
    #region Fields and Constants

    // Laid-out texts kept per layout; words repeat, so most lookups hit. Cleared when it grows past this many entries.
    private const int CacheLimit = 20_000;

    private readonly Dictionary<(string Text, string Families, int Weight, bool Italic, StandardFontFamily Standard, double Size), LaidOutText> _cache = new();
    private readonly IReadOnlyList<PdfFont> _fallback;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PdfFont>> _fonts;

    #endregion

    #region Constructors and Finalizers

    public HtmlFonts(IReadOnlyDictionary<string, IReadOnlyList<PdfFont>> fonts, IReadOnlyList<PdfFont> fallback, bool synthesize = false)
    {
        _fonts = fonts;
        _fallback = fallback;
        Synthesize = synthesize;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the registered face of a family nearest a weight and style: the smallest weight distance (zero inside a
    /// variable font's range), then the matching style, then the order the faces were added. A variable face is
    /// returned at the requested weight, clamped to its range.
    /// </summary>
    /// <param name="faces">The family's faces.</param>
    /// <param name="weight">The CSS weight.</param>
    /// <param name="italic">Whether the style is italic or oblique.</param>
    /// <returns>The face.</returns>
    public static PdfFont Match(IReadOnlyList<PdfFont> faces, int weight, bool italic)
    {
        static int Distance(PdfFont face, int weight) =>
            weight < face.MinimumWeight ? face.MinimumWeight - weight : weight > face.MaximumWeight ? weight - face.MaximumWeight : 0;

        var best = faces
            .Select((face, order) => (Face: face, Distance: Distance(face, weight), Style: face.IsItalic == italic ? 0 : 1, Order: order))
            .OrderBy(f => f.Distance).ThenBy(f => f.Style).ThenBy(f => f.Order)
            .First();
        return best.Face.AtWeight(weight);
    }

    /// <summary>
    /// Returns how a face misses a style: whether the text asks for bold (weight 600 or more) and the face is not
    /// bold, or the other way round, and the same for italic.
    /// </summary>
    public static (bool Weight, bool Italic) Gap(PdfFont face, int weight, bool italic) =>
        ((weight >= StandardFonts.BoldWeight) != (face.Weight >= StandardFonts.BoldWeight), italic != face.IsItalic);

    /// <summary>
    /// Returns the registered families of a style whose face misses the style and cannot be synthesized, among the
    /// faces in <paramref name="used"/>: the family name and the face used.
    /// </summary>
    /// <param name="style">The text's style.</param>
    /// <param name="used">The caller fonts the text is drawn with.</param>
    /// <returns>The missing faces.</returns>
    public IEnumerable<(string Family, PdfFont Face)> MissingFaces(ComputedStyle style, IReadOnlyCollection<PdfFont> used)
    {
        foreach (string name in style.FontFamilies.Distinct(StringComparer.Ordinal))
        {
            if (!_fonts.TryGetValue(name, out var faces) || faces.Count == 0) continue;
            var face = Match(faces, style.FontWeight, style.Italic);
            if (!used.Contains(face)) continue;
            var (weightGap, italicGap) = Gap(face, style.FontWeight, style.Italic);
            bool bold = style.FontWeight >= StandardFonts.BoldWeight;

            // Synthesis adds bold to a lighter face and a slant to an upright one; it cannot take either away.
            bool covered = Synthesize && (!weightGap || bold) && (!italicGap || style.Italic);
            if ((weightGap || italicGap) && !covered) yield return (name, face);
        }
    }

    /// <summary>
    /// Returns the styles to synthesize for text drawn with a caller font: bold when the text is bold and the face is
    /// not, a slant when the text is italic and the face is not; none unless the load opted in.
    /// </summary>
    public (bool Bold, bool Italic) Synthesis(ComputedStyle style, PdfFont font) => Synthesize
        ? (style.FontWeight >= StandardFonts.BoldWeight && font.Weight < StandardFonts.BoldWeight, style.Italic && !font.IsItalic)
        : (false, false);

    /// <summary>
    /// Returns the fonts a style tries, in order; null stands for the standard font.
    /// </summary>
    /// <param name="style">The text's style.</param>
    /// <returns>The candidates.</returns>
    public IReadOnlyList<PdfFont?> Candidates(ComputedStyle style)
    {
        var candidates = new List<PdfFont?>();
        bool standard = false;
        foreach (string name in style.FontFamilies)
        {
            if (_fonts.TryGetValue(name, out var faces) && faces.Count > 0)
            {
                var face = Match(faces, style.FontWeight, style.Italic);
                if (!candidates.Contains(face)) candidates.Add(face);
            }
            else if (!standard && StandardFonts.IsKnown(name))
            {
                candidates.Add(null);
                standard = true;
            }
        }

        // Text with no font-family uses the initial family, a built-in one, before the fallback fonts.
        if (style.FontFamilies.Count == 0) candidates.Add(null);
        foreach (var font in _fallback) if (!candidates.Contains(font)) candidates.Add(font);
        return candidates;
    }

    /// <summary>
    /// Lays out text in a style's fonts, without letter spacing.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The style.</param>
    /// <returns>The laid-out text.</returns>
    public LaidOutText Layout(string text, ComputedStyle style)
    {
        var key = (text, string.Join("\u0001", style.FontFamilies), style.FontWeight, style.Italic, style.FontFamily, style.FontSize);
        if (_cache.TryGetValue(key, out var known)) return known;
        var standard = StandardFonts.Select(style.FontFamily, style.FontWeight, style.Italic);
        double size = style.FontSize;
        var layout = TextPipeline.Layout(text, Candidates(style), size, run => StandardFontMetrics.Measure(standard, run, size));
        if (_cache.Count >= CacheLimit) _cache.Clear();
        _cache[key] = layout;
        return layout;
    }

    /// <summary>
    /// Returns the width of text in a style's fonts, letter spacing included: after each character drawn in the
    /// standard font and after each cluster of a caller font.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The style.</param>
    /// <returns>The width in points.</returns>
    public double Measure(string text, ComputedStyle style)
    {
        var layout = Layout(text, style);
        return layout.Width + style.LetterSpacing * layout.Segments.Sum(Slots);
    }

    /// <summary>
    /// Returns the number of places letter spacing goes in a segment.
    /// </summary>
    public static int Slots(TextSegment segment) => segment is FontSegment shaped ? TextPipeline.Clusters(shaped) : segment.Text.Length;

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets whether HTML text takes the standard-font path unchanged: no registered fonts and no fallback fonts.
    /// </summary>
    public bool Simple => _fonts.Count == 0 && _fallback.Count == 0;

    /// <summary>
    /// Gets whether missing bold and italic faces are synthesized (D-012).
    /// </summary>
    public bool Synthesize { get; }

    #endregion
}
