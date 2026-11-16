using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// One of the twelve standard Helvetica, Times, and Courier fonts, not embedded: the library writes them as Type1
/// fonts with WinAnsi encoding (or, for <c>/F1</c>, ASCII text with no encoding). There are no outlines to draw, so the
/// text is drawn in the closest installed family, with each character placed at the position the PDF's widths give it.
/// </summary>
internal sealed class StandardRenderFont : RenderFont
{
    #region Fields and Constants

    // Windows-1252 characters for codes 0x80 to 0x9F; the rest of WinAnsi is Latin-1.
    private static readonly char[] WinAnsiHigh =
    {
        '€', '\0', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\0', 'Ž', '\0',
        '\0', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\0', 'ž', 'Ÿ',
    };

    private static readonly Dictionary<string, StandardFont> BaseFonts = new(StringComparer.Ordinal)
    {
        ["Helvetica"] = StandardFont.Helvetica,
        ["Helvetica-Bold"] = StandardFont.HelveticaBold,
        ["Helvetica-Oblique"] = StandardFont.HelveticaOblique,
        ["Helvetica-BoldOblique"] = StandardFont.HelveticaBoldOblique,
        ["Times-Roman"] = StandardFont.TimesRoman,
        ["Times-Bold"] = StandardFont.TimesBold,
        ["Times-Italic"] = StandardFont.TimesItalic,
        ["Times-BoldItalic"] = StandardFont.TimesBoldItalic,
        ["Courier"] = StandardFont.Courier,
        ["Courier-Bold"] = StandardFont.CourierBold,
        ["Courier-Oblique"] = StandardFont.CourierOblique,
        ["Courier-BoldOblique"] = StandardFont.CourierBoldOblique,
    };

    private readonly StandardFont _font;
    private readonly int _firstChar;
    private readonly double[]? _widths;

    #endregion

    #region Constructors and Finalizers

    private StandardRenderFont(StandardFont font, int firstChar, double[]? widths)
    {
        _font = font;
        _firstChar = firstChar;
        _widths = widths;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads a standard font from its font dictionary, or returns null when the dictionary is not one.
    /// </summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="resolve">Resolves references.</param>
    /// <returns>The font, or null.</returns>
    public static StandardRenderFont? From(PdfReadDictionary font, Func<PdfReadValue?, PdfReadValue> resolve)
    {
        if (resolve(font["Subtype"]) is not PdfReadName { Value: "Type1" }) return null;
        if (resolve(font["BaseFont"]) is not PdfReadName baseFont || !BaseFonts.TryGetValue(baseFont.Value, out var standard)) return null;
        if (font.ContainsKey("FontDescriptor") && resolve(font["FontDescriptor"]) is PdfReadDictionary descriptor
            && (descriptor.ContainsKey("FontFile") || descriptor.ContainsKey("FontFile2") || descriptor.ContainsKey("FontFile3")))
            return null; // embedded: the program would win over the standard font
        var encoding = resolve(font["Encoding"]);
        if (encoding is not (PdfReadNull or PdfReadName { Value: "WinAnsiEncoding" })) return null;

        double[]? widths = null;
        int firstChar = 0;
        if (resolve(font["Widths"]) is PdfReadArray widthArray && resolve(font["FirstChar"]) is PdfReadNumber first)
        {
            firstChar = (int)Math.Clamp(first.IntegerValue, 0, 255);
            widths = widthArray.Items.Take(256).Select(item => resolve(item) is PdfReadNumber n && double.IsFinite(n.Value) ? n.Value : 0).ToArray();
        }
        return new StandardRenderFont(standard, firstChar, widths);
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderGlyph> Glyphs(byte[] bytes)
    {
        var glyphs = new RenderGlyph[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte code = bytes[i];
            double width = _widths is not null && code >= _firstChar && code - _firstChar < _widths.Length
                ? _widths[code - _firstChar]
                : StandardFontMetrics.Width(_font, code);
            char c = code is >= 0x80 and <= 0x9F ? WinAnsiHigh[code - 0x80] : (char)code;
            glyphs[i] = new RenderGlyph(code, width, code == 32, c == '\0' ? string.Empty : c.ToString());
        }
        return glyphs;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the CSS font family list for the closest installed fonts.
    /// </summary>
    public string Family => _font switch
    {
        StandardFont.TimesRoman or StandardFont.TimesBold or StandardFont.TimesItalic or StandardFont.TimesBoldItalic =>
            "'Times New Roman', Times, 'Liberation Serif', serif",
        StandardFont.Courier or StandardFont.CourierBold or StandardFont.CourierOblique or StandardFont.CourierBoldOblique =>
            "'Courier New', Courier, 'Liberation Mono', monospace",
        _ => "Helvetica, Arial, 'Liberation Sans', sans-serif",
    };

    /// <summary>
    /// Gets whether the font is bold.
    /// </summary>
    public bool IsBold => _font is StandardFont.HelveticaBold or StandardFont.HelveticaBoldOblique or StandardFont.TimesBold
        or StandardFont.TimesBoldItalic or StandardFont.CourierBold or StandardFont.CourierBoldOblique;

    /// <summary>
    /// Gets whether the font is italic or oblique.
    /// </summary>
    public bool IsItalic => _font is StandardFont.HelveticaOblique or StandardFont.HelveticaBoldOblique or StandardFont.TimesItalic
        or StandardFont.TimesBoldItalic or StandardFont.CourierOblique or StandardFont.CourierBoldOblique;

    #endregion
}
