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
    /// Appends to <paramref name="sb"/> the operators that draw the glyphs, recording each in
    /// <paramref name="sink"/>. Nothing is appended when there are no glyphs, and no line break follows them.
    /// </summary>
    /// <param name="sb">The page's content.</param>
    /// <param name="sink">The page's draw sink.</param>
    public void ToContent(StringBuilder sb, PageDrawSink sink)
    {
        if (Glyphs.Count == 0) return;
        string resource = string.Empty;
        foreach (var (glyph, text) in Glyphs) resource = sink.FaceResource(Font, glyph, text);

        // BT/ET = text object, Tf = font and size, Td = start position, Tj = show glyphs
        sb.Append("BT /").Append(resource).Append(' ').Append(PdfNumber.Format(FontSize)).Append(" Tf ")
          .Append(PdfNumber.Format(X)).Append(' ').Append(PdfNumber.Format(Y)).Append(" Td <");
        foreach (var (glyph, _) in Glyphs) TextPipeline.AppendGlyphId(sb, glyph);
        sb.Append("> Tj ET");
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
