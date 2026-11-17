using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// A Type0 font with Identity-H encoding and an embedded TrueType (<c>CIDFontType2</c>, <c>FontFile2</c>) or CFF
/// (<c>CIDFontType0</c>, <c>FontFile3</c>) program, as the library writes caller fonts: each 2-byte code is a glyph ID,
/// widths come from <c>/W</c>, and characters from <c>/ToUnicode</c>. Glyphs are drawn from their outlines.
/// </summary>
internal sealed class EmbeddedRenderFont : RenderFont
{
    #region Fields and Constants

    // CFF Top DICT operators.
    private const int OpCharset = 15;
    private const int OpCharStrings = 17;
    private const int OpFdArray = 1236;
    private const int OpFontMatrix = 1207;
    private const int OpRos = 1230;

    // The most width entries written from /W, counting overwrites.
    private const int MaxWidthWrites = 2 * 65_536;

    private readonly CffFont? _cff;
    private readonly double _defaultWidth;
    private readonly Dictionary<int, string> _paths = new();
    private readonly OpenTypeFont? _trueType;
    private readonly ToUnicodeMap? _toUnicode;
    private readonly Dictionary<int, double> _widths;

    #endregion

    #region Constructors and Finalizers

    private EmbeddedRenderFont(OpenTypeFont? trueType, CffFont? cff, double unitsToEm, Dictionary<int, double> widths, double defaultWidth, ToUnicodeMap? toUnicode)
    {
        _trueType = trueType;
        _cff = cff;
        UnitsToEm = unitsToEm;
        _widths = widths;
        _defaultWidth = defaultWidth;
        _toUnicode = toUnicode;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads an embedded Type0 font from its font dictionary, or returns null when the dictionary is not one this
    /// renderer draws.
    /// </summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="resolve">Resolves references.</param>
    /// <param name="decode">Decodes a stream within the page's bounds.</param>
    /// <returns>The font, or null.</returns>
    /// <exception cref="PageRenderException">The font is one this renderer draws, but it is damaged.</exception>
    public static EmbeddedRenderFont? From(PdfReadDictionary font, Func<PdfReadValue?, PdfReadValue> resolve, Func<PdfReadStream, byte[]> decode)
    {
        if (resolve(font["Subtype"]) is not PdfReadName { Value: "Type0" }) return null;
        if (resolve(font["Encoding"]) is not PdfReadName { Value: "Identity-H" }) return null;
        if (resolve(font["DescendantFonts"]) is not PdfReadArray { Count: 1 } descendants || resolve(descendants[0]) is not PdfReadDictionary cid) return null;
        if (resolve(cid["FontDescriptor"]) is not PdfReadDictionary descriptor) return null;

        string subtype = (resolve(cid["Subtype"]) as PdfReadName)?.Value ?? string.Empty;
        OpenTypeFont? trueType = null;
        CffFont? cff = null;
        double unitsToEm;
        if (subtype == "CIDFontType2" && resolve(descriptor["FontFile2"]) is PdfReadStream fontFile2)
        {
            // Glyph IDs are the codes only with an identity CID-to-GID map.
            if (cid.ContainsKey("CIDToGIDMap") && resolve(cid["CIDToGIDMap"]) is not PdfReadName { Value: "Identity" }) return null;
            trueType = OpenTypeFont.Parse(decode(fontFile2));
            if (trueType.Outlines != FontOutlines.TrueType || trueType.UnitsPerEm == 0) return null;
            unitsToEm = 1.0 / trueType.UnitsPerEm;
        }
        else if (subtype == "CIDFontType0" && resolve(descriptor["FontFile3"]) is PdfReadStream fontFile3
            && resolve(fontFile3.Dictionary["Subtype"]) is PdfReadName { Value: "CIDFontType0C" })
        {
            (cff, unitsToEm) = ReadCff(decode(fontFile3));
        }
        else
        {
            return null;
        }

        double defaultWidth = resolve(cid["DW"]) is PdfReadNumber dw && double.IsFinite(dw.Value) ? dw.Value : 1000;
        var toUnicode = resolve(font["ToUnicode"]) is PdfReadStream map ? ToUnicodeMap.Read(decode(map)) : null;
        return new EmbeddedRenderFont(trueType, cff, unitsToEm, Widths(resolve(cid["W"]), resolve), defaultWidth, toUnicode);
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderGlyph> Glyphs(byte[] bytes)
    {
        if (bytes.Length % 2 != 0) throw new PageRenderException("A string in a two-byte font has an odd length.");
        var glyphs = new RenderGlyph[bytes.Length / 2];
        for (int i = 0; i < glyphs.Length; i++)
        {
            int code = (bytes[2 * i] << 8) | bytes[2 * i + 1];
            glyphs[i] = new RenderGlyph(code, _widths.TryGetValue(code, out double width) ? width : _defaultWidth, false,
                _toUnicode?.Text(code) ?? string.Empty);
        }
        return glyphs;
    }

    /// <summary>
    /// Returns the outline of a glyph as SVG path data in font units, y up; empty for a glyph without outlines.
    /// </summary>
    /// <param name="glyph">The glyph ID.</param>
    /// <param name="maxLength">The longest path allowed; a longer one stops the page.</param>
    /// <param name="pageComponents">The composite components expanded on the page so far.</param>
    /// <param name="maxPageComponents">The most components the page may expand.</param>
    /// <returns>The path data.</returns>
    /// <exception cref="PageRenderException">The glyph is not in the font, or its outline is damaged or too large.</exception>
    public string Outline(int glyph, int maxLength, ref int pageComponents, int maxPageComponents)
    {
        if (_paths.TryGetValue(glyph, out var path)) return path;
        if (_trueType is not null)
        {
            path = GlyphOutlines.TrueType(_trueType, glyph, maxLength, ref pageComponents, maxPageComponents);
        }
        else
        {
            if (glyph < 0 || glyph >= _cff!.CharStrings.Count) throw new PageRenderException("The page shows a glyph its font doesn't have.");
            path = GlyphOutlines.Type2(_cff.Flatten(glyph), maxLength);
        }
        _paths[glyph] = path;
        return path;
    }

    #endregion

    #region Private Methods

    // A bare CFF program (FontFile3, CIDFontType0C): its glyph count comes from its CharStrings INDEX, and its units from
    // the Top DICT's FontMatrix (the library writes 1/unitsPerEm there when the font's em is not 1000).
    private static (CffFont Font, double UnitsToEm) ReadCff(byte[] program)
    {
        var data = new FontBytes(program);
        if (program.Length < 4) throw FontBytes.Corrupt("is too short to be a CFF program");
        int headerSize = data.U8(2);
        var names = CffIndex.Read(data, headerSize, program.Length, "Name INDEX");
        var topDicts = CffIndex.Read(data, names.End, program.Length, "Top DICT INDEX");
        if (topDicts.Count != 1) throw FontBytes.Corrupt("holds more than one CFF font");
        var (topOffset, topLength) = topDicts.Entry(0);
        var top = CffDict.Read(data, topOffset, topLength);
        int glyphCount = CffIndex.Read(data, top.Int(OpCharStrings), program.Length, "CharStrings INDEX").Count;

        // Codes are CIDs; they are glyph IDs only in a CID-keyed font whose charset maps each glyph to its own number,
        // and the glyphs keep the Top DICT's units only when no font dictionary has a matrix of its own.
        if (!top.Has(OpRos)) throw new PageRenderException("A CFF font on the page isn't CID-keyed, which this preview doesn't draw.");
        if (!IdentityCharset(data, program.Length, top.Has(OpCharset) ? top.Int(OpCharset) : 0, glyphCount))
            throw new PageRenderException("A CFF font on the page maps its glyphs in an order this preview doesn't draw.");
        if (top.Has(OpFdArray))
        {
            var fdArray = CffIndex.Read(data, top.Int(OpFdArray), program.Length, "FDArray");
            for (int fd = 0; fd < fdArray.Count; fd++)
            {
                var (fdOffset, fdLength) = fdArray.Entry(fd);
                var fontDict = CffDict.Read(data, fdOffset, fdLength);
                if (fontDict.Has(OpFontMatrix) && !(fontDict.Number(OpFontMatrix, 0) == 1 && fontDict.Number(OpFontMatrix, 1) == 0
                    && fontDict.Number(OpFontMatrix, 2) == 0 && fontDict.Number(OpFontMatrix, 3) == 1
                    && fontDict.Number(OpFontMatrix, 4) == 0 && fontDict.Number(OpFontMatrix, 5) == 0))
                    throw new PageRenderException("A CFF font on the page scales glyphs per font dictionary, which this preview doesn't draw.");
            }
        }
        double scale = top.Has(OpFontMatrix) ? top.Number(OpFontMatrix, 0) : 0.001;
        if (!double.IsFinite(scale) || scale <= 0 || scale > 1) throw FontBytes.Corrupt("has a CFF FontMatrix this preview doesn't draw");
        if (top.Has(OpFontMatrix) && (top.Number(OpFontMatrix, 1) != 0 || top.Number(OpFontMatrix, 2) != 0 || top.Number(OpFontMatrix, 3) != scale
            || top.Number(OpFontMatrix, 4) != 0 || top.Number(OpFontMatrix, 5) != 0))
            throw FontBytes.Corrupt("has a CFF FontMatrix this preview doesn't draw");
        return (CffFont.Read(data, 0, program.Length, glyphCount), scale);
    }

    // Whether a CID-keyed CFF charset maps glyph i to CID i for every glyph: the predefined charset 0, or a
    // format 0, 1, or 2 charset whose CIDs run 1, 2, 3, ... from glyph 1.
    private static bool IdentityCharset(FontBytes data, int limit, int offset, int glyphCount)
    {
        if (offset == 0) return true;
        if (offset <= 2 || offset >= limit) return false;
        int format = data.U8(offset);
        int p = offset + 1, expected = 1;
        while (expected < glyphCount)
        {
            if (format == 0)
            {
                if (p + 2 > limit || data.U16(p) != expected) return false;
                p += 2;
                expected++;
            }
            else if (format is 1 or 2)
            {
                int rangeLength = format == 1 ? 3 : 4;
                if (p + rangeLength > limit || data.U16(p) != expected) return false;
                int left = format == 1 ? data.U8(p + 2) : data.U16(p + 2);
                expected += left + 1;
                p += rangeLength;
            }
            else
            {
                return false;
            }
        }
        return expected == glyphCount;
    }

    // /W: [c [w1 w2 ...]] gives widths from c on; [c1 c2 w] gives one width to a range.
    private static Dictionary<int, double> Widths(PdfReadValue value, Func<PdfReadValue?, PdfReadValue> resolve)
    {
        var widths = new Dictionary<int, double>();
        if (value is not PdfReadArray array) return widths;
        int writes = 0;
        void Set(int code, double width)
        {
            if (++writes > MaxWidthWrites) throw new PageRenderException("A font's widths are too many.");
            widths[code] = width;
        }
        var items = array.Items.Select(item => resolve(item)).ToList();
        for (int i = 0; i < items.Count;)
        {
            if (items[i] is not PdfReadNumber first) throw new PageRenderException("A font's widths are damaged.");
            int start = (int)Math.Clamp(first.IntegerValue, 0, 0xFFFF);
            if (i + 1 < items.Count && items[i + 1] is PdfReadArray list)
            {
                for (int k = 0; k < list.Count && start + k <= 0xFFFF; k++)
                {
                    if (resolve(list[k]) is PdfReadNumber w && double.IsFinite(w.Value)) Set(start + k, w.Value);
                }
                i += 2;
            }
            else if (i + 2 < items.Count && items[i + 1] is PdfReadNumber last && items[i + 2] is PdfReadNumber w && double.IsFinite(w.Value))
            {
                int end = (int)Math.Clamp(last.IntegerValue, start, 0xFFFF);
                for (int code = start; code <= end; code++) Set(code, w.Value);
                i += 3;
            }
            else
            {
                throw new PageRenderException("A font's widths are damaged.");
            }
        }
        return widths;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of characters the font's text map holds.
    /// </summary>
    public long TextCharacters => _toUnicode?.Characters ?? 0;

    /// <summary>
    /// Gets the scale from font units to the em (1/unitsPerEm, or the CFF FontMatrix scale).
    /// </summary>
    public double UnitsToEm { get; }

    #endregion
}
