namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Glyphs of a caller font drawn at a page position (WR-039). Each glyph is written as its 2-byte glyph ID in a hex
/// string (Identity-H), so no text from the caller reaches the content stream as literal syntax (A05), and is
/// recorded in the page's sink with the text it stands for, for the font's subset and <c>ToUnicode</c> map.
/// </summary>
internal sealed class PdfGlyphRunElement
{
    #region Constructors and Finalizers

    public PdfGlyphRunElement(PdfFont font, IReadOnlyList<(int Glyph, string Text)> glyphs, double x, double y, double fontSize)
    {
        Font = font;
        Glyphs = glyphs;
        X = x;
        Y = y;
        FontSize = fontSize;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the operators that draw the glyphs, recording each in <paramref name="sink"/>.
    /// </summary>
    /// <param name="sink">The page's draw sink.</param>
    /// <returns>The operators; empty when there are no glyphs.</returns>
    public string ToContent(PageDrawSink sink)
    {
        if (Glyphs.Count == 0) return string.Empty;
        string resource = string.Empty;
        var hex = new StringBuilder(Glyphs.Count * 4);
        foreach (var (glyph, text) in Glyphs)
        {
            resource = sink.FaceResource(Font, glyph, text);
            hex.Append(glyph.ToString("X4", CultureInfo.InvariantCulture));
        }

        // BT/ET = text object, Tf = font and size, Td = start position, Tj = show glyphs
        return $"BT /{resource} {PdfNumber.Format(FontSize)} Tf {PdfNumber.Format(X)} {PdfNumber.Format(Y)} Td <{hex}> Tj ET";
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public PdfFont Font { get; }

    public double FontSize { get; }

    public IReadOnlyList<(int Glyph, string Text)> Glyphs { get; }

    public double X { get; }

    public double Y { get; }

    #endregion
}
