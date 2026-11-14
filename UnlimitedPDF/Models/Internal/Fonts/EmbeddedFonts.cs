using System.IO.Compression;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

namespace UnlimitedPDF.Models.Internal.Fonts;

/// <summary>
/// One caller font a document draws with: its page resource name and, once written, its five objects (Type0 font,
/// CIDFont, font descriptor, font program, <c>ToUnicode</c> map).
/// </summary>
internal sealed class EmbeddedFace
{
    #region Constructors and Finalizers

    public EmbeddedFace(PdfFont font, string resourceName)
    {
        Font = font;
        ResourceName = resourceName;
    }

    #endregion

    #region Properties, Indexers

    public PdfFont Font { get; }

    /// <summary>
    /// Gets the face's object numbers (Type0, CIDFont, descriptor, program, ToUnicode), or null before the first save
    /// that draws with it.
    /// </summary>
    public int[]? Objects { get; set; }

    public string ResourceName { get; }

    #endregion
}

/// <summary>
/// The caller fonts of one document (WR-039): resource names <c>/E1</c>, <c>/E2</c>, ... in order of first use, kept
/// across saves, and the writer of each face's objects. A face is embedded once per document, with the glyphs every
/// page of the current save uses (D-010).
/// </summary>
internal sealed class EmbeddedFonts
{
    #region Fields and Constants

    // Values in a ToUnicode bfchar block: the CMap format allows at most 100 entries per block.
    private const int BlockSize = 100;

    private readonly Dictionary<PdfFont, EmbeddedFace> _faces = new(ReferenceEqualityComparer.Instance);
    private readonly List<EmbeddedFace> _order = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the face of <paramref name="font"/>, adding it with the next resource name on first use.
    /// </summary>
    public EmbeddedFace Face(PdfFont font)
    {
        if (!_faces.TryGetValue(font, out var face))
        {
            face = new EmbeddedFace(font, "E" + (_order.Count + 1).ToString(CultureInfo.InvariantCulture));
            _faces[font] = face;
            _order.Add(face);
        }
        return face;
    }

    /// <summary>
    /// Returns a face's objects for its glyph use: the Type0, CIDFont, and descriptor dictionaries and the bodies
    /// and data of the program and ToUnicode streams.
    /// </summary>
    /// <param name="face">The face, with its object numbers set.</param>
    /// <param name="used">Every glyph the save draws with this face and the text it stands for.</param>
    public static FaceObjects Write(EmbeddedFace face, IReadOnlyDictionary<int, string> used)
    {
        var font = face.Font;
        var objects = face.Objects!;
        var source = font.Font;
        var instance = font.Instance;
        double scale = 1000.0 / source.UnitsPerEm;
        bool trueType = source.Outlines == FontOutlines.TrueType;

        // Both outline kinds keep glyph IDs, so CIDs equal glyph IDs: TrueType through an identity CIDToGIDMap, CFF
        // through a charset that maps every glyph to the CID of the same number.
        var kept = TrueTypeSubsetter.KeptGlyphs(font, used.Keys);
        string name = SubsetTag(source.PostScriptName + "|" + font.Weight, kept) + "+" + SafeName(source);
        var program = trueType ? TrueTypeSubsetter.Build(font, kept) : CffSubsetter.Build(font, kept, name);

        var compressed = Compress(program);
        var programBody = Encoding.ASCII.GetBytes(trueType
            ? $"<< /Length {compressed.Length} /Length1 {program.Length} /Filter /FlateDecode >>"
            : $"<< /Subtype /CIDFontType0C /Length {compressed.Length} /Filter /FlateDecode >>");
        var toUnicode = ToUnicode(used);
        var toUnicodeBody = Encoding.ASCII.GetBytes($"<< /Length {toUnicode.Length} >>");

        var (xMin, yMin, xMax, yMax) = source.Bounds;
        int flags = 4 | (source.IsFixedPitch ? 1 : 0) | (font.IsItalic ? 64 : 0); // Symbolic, FixedPitch, Italic
        double stemV = Math.Round(50 + Math.Pow(font.Weight / 65.0, 2));
        string descriptor =
            $"<< /Type /FontDescriptor /FontName /{name} /Flags {flags} " +
            $"/FontBBox [{N(xMin * scale)} {N(yMin * scale)} {N(xMax * scale)} {N(yMax * scale)}] /ItalicAngle {N(source.ItalicAngle)} " +
            $"/Ascent {N(instance.Ascender * scale)} /Descent {N(instance.Descender * scale)} /CapHeight {N(instance.CapHeight * scale)} " +
            $"/StemV {N(stemV)} /{(trueType ? "FontFile2" : "FontFile3")} {objects[3]} 0 R >>";

        string cidFont =
            $"<< /Type /Font /Subtype /{(trueType ? "CIDFontType2" : "CIDFontType0")} /BaseFont /{name} " +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
            $"/FontDescriptor {objects[2]} 0 R {(trueType ? "/CIDToGIDMap /Identity " : string.Empty)}/W {Widths(used.Keys, instance, scale)} >>";

        string type0 = $"<< /Type /Font /Subtype /Type0 /BaseFont /{name} /Encoding /Identity-H /DescendantFonts [{objects[1]} 0 R] /ToUnicode {objects[4]} 0 R >>";
        return new FaceObjects(type0, cidFont, descriptor, programBody, compressed, toUnicodeBody, toUnicode);
    }

    #endregion

    #region Private Methods

    private static string N(double value) => PdfNumber.Format(Math.Round(value));

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data, 0, data.Length);
        return output.ToArray();
    }

    // The font's PostScript name (else its family name) reduced to letters, digits, and hyphens, at most 63 long, so
    // names from untrusted font data can never break PDF syntax (A05).
    private static string SafeName(OpenTypeFont font)
    {
        string raw = font.PostScriptName.Length > 0 ? font.PostScriptName : font.FamilyName;
        var safe = new string(raw.Where(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-').Take(63).ToArray());
        return safe.Length > 0 ? safe : "Font";
    }

    // Six capital letters derived from the font and its kept glyphs with FNV-1a: the same document always gets the
    // same tag, and different subsets of one font get different tags.
    private static string SubsetTag(string fontKey, IEnumerable<int> glyphs)
    {
        ulong hash = 14695981039346656037UL;
        void Mix(int value)
        {
            hash ^= (uint)value;
            hash *= 1099511628211UL;
        }
        foreach (char c in fontKey) Mix(c);
        foreach (int g in glyphs) Mix(g);
        var tag = new char[6];
        for (int i = 0; i < 6; i++)
        {
            tag[i] = (char)('A' + (int)(hash % 26));
            hash /= 26;
        }
        return new string(tag);
    }

    private static byte[] ToUnicode(IReadOnlyDictionary<int, string> used)
    {
        var entries = used.Where(u => u.Value.Length > 0).OrderBy(u => u.Key).ToList();
        var sb = new StringBuilder();
        sb.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
        sb.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
        sb.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
        sb.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        for (int i = 0; i < entries.Count; i += BlockSize)
        {
            var block = entries.Skip(i).Take(BlockSize).ToList();
            sb.Append(block.Count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");
            foreach (var (glyph, text) in block)
                sb.Append('<').Append(glyph.ToString("X4", CultureInfo.InvariantCulture)).Append("> <").Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))).Append(">\n");
            sb.Append("endbfchar\n");
        }
        sb.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    // /W lists every used glyph's width in 1/1000 em, consecutive glyphs grouped: [g [w1 w2 ...] ...].
    private static string Widths(IEnumerable<int> glyphs, FontInstance instance, double scale)
    {
        var sorted = glyphs.OrderBy(g => g).ToList();
        var sb = new StringBuilder("[");
        for (int i = 0; i < sorted.Count;)
        {
            int start = i;
            while (i + 1 < sorted.Count && sorted[i + 1] == sorted[i] + 1) i++;
            sb.Append(sorted[start].ToString(CultureInfo.InvariantCulture)).Append(" [");
            for (int j = start; j <= i; j++) sb.Append(j > start ? " " : string.Empty).Append(N(instance.Advance(sorted[j]) * scale));
            sb.Append("] ");
            i++;
        }
        return sb.ToString().TrimEnd() + "]";
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public IReadOnlyList<EmbeddedFace> Faces => _order;

    #endregion
}

/// <summary>
/// The written objects of one embedded face.
/// </summary>
internal sealed record FaceObjects(string Type0, string CidFont, string Descriptor, byte[] ProgramBody, byte[] ProgramData, byte[] ToUnicodeBody, byte[] ToUnicodeData);
