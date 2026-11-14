using System.Buffers.Binary;
using System.IO.Compression;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// An embedded composite font as the checker reads it back (D-005, D-010): the Type0 font, its CIDFont, descriptor,
/// widths, <c>ToUnicode</c> map, and decoded font program.
/// </summary>
internal sealed record EmbeddedFont(
    int ObjectNumber,
    string BaseFont,
    string CidFontSubtype,
    Dictionary<string, object?> Descriptor,
    Dictionary<int, double> Widths,
    Dictionary<int, string> ToUnicode,
    byte[] Program,
    string ProgramKey)
{
    /// <summary>
    /// Gets the IDs of the glyphs whose outlines the embedded program holds (non-empty <c>glyf</c> data, or a CFF
    /// charstring longer than a bare <c>endchar</c>).
    /// </summary>
    public IReadOnlySet<int> GlyphsWithOutlines => ProgramKey == "FontFile2" ? TrueTypeGlyphs(Program) : CffGlyphs(Program);

    /// <summary>
    /// Gets the glyph count of the embedded program.
    /// </summary>
    public int GlyphCount => ProgramKey == "FontFile2" ? TrueTypeGlyphCount(Program) : CffCharstrings(Program).Count;

    internal static Dictionary<string, (int Offset, int Length)> SfntTables(byte[] font)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            var record = font.AsSpan(12 + 16 * i);
            tables[Encoding.ASCII.GetString(record.Slice(0, 4))] = ((int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(8)), (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(12)));
        }
        return tables;
    }

    internal static int TrueTypeGlyphCount(byte[] font) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(SfntTables(font)["maxp"].Offset + 4));

    internal static int[] TrueTypeGlyphOffsets(byte[] font)
    {
        var tables = SfntTables(font);
        int glyphs = TrueTypeGlyphCount(font);
        bool longOffsets = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(tables["head"].Offset + 50)) == 1;
        int loca = tables["loca"].Offset;
        return Enumerable.Range(0, glyphs + 1)
            .Select(i => longOffsets ? (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(loca + 4 * i)) : 2 * BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(loca + 2 * i)))
            .ToArray();
    }

    private static IReadOnlySet<int> TrueTypeGlyphs(byte[] font)
    {
        var offsets = TrueTypeGlyphOffsets(font);
        return Enumerable.Range(0, offsets.Length - 1).Where(g => offsets[g + 1] > offsets[g]).ToHashSet();
    }

    private static IReadOnlySet<int> CffGlyphs(byte[] cff)
    {
        var charstrings = CffCharstrings(cff);
        return Enumerable.Range(0, charstrings.Count).Where(g => !IsBareEndChar(charstrings[g])).ToHashSet();
    }

    // A glyph without outlines is endchar, possibly after a width operand.
    private static bool IsBareEndChar(byte[] charstring) =>
        charstring.Length > 0 && charstring[^1] == 14 && (charstring.Length == 1 || (charstring.Length <= 4 && charstring.Take(charstring.Length - 1).All(b => b >= 28)));

    // The CharStrings INDEX of a CFF version 1 table, found through the Top DICT's CharStrings operator.
    internal static List<byte[]> CffCharstrings(byte[] cff)
    {
        int p = cff[2];
        p = SkipIndex(cff, p); // Name INDEX
        var top = IndexEntries(cff, p)[0];
        var operands = new List<int>();
        int charStrings = -1;
        for (int i = 0; i < top.Length;)
        {
            int b0 = top[i];
            if (b0 <= 21)
            {
                int op = b0 == 12 ? 1200 + top[++i] : b0;
                i++;
                if (op == 17) charStrings = operands[^1];
                operands.Clear();
            }
            else if (b0 == 28) { operands.Add((short)((top[i + 1] << 8) | top[i + 2])); i += 3; }
            else if (b0 == 29) { operands.Add(BinaryPrimitives.ReadInt32BigEndian(top.AsSpan(i + 1))); i += 5; }
            else if (b0 == 30) { i++; while ((top[i] & 0x0F) != 0x0F && (top[i] >> 4) != 0x0F) i++; i++; operands.Add(0); }
            else if (b0 <= 246) { operands.Add(b0 - 139); i++; }
            else if (b0 <= 250) { operands.Add((b0 - 247) * 256 + top[i + 1] + 108); i += 2; }
            else { operands.Add(-(b0 - 251) * 256 - top[i + 1] - 108); i += 2; }
        }
        if (charStrings < 0) throw new FormatException("The CFF Top DICT has no CharStrings.");
        return IndexEntries(cff, charStrings);
    }

    private static int SkipIndex(byte[] data, int p)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p));
        if (count == 0) return p + 2;
        int offSize = data[p + 2];
        int last = ReadOffset(data, p + 3 + count * offSize, offSize);
        return p + 3 + (count + 1) * offSize + last - 1;
    }

    private static List<byte[]> IndexEntries(byte[] data, int p)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p));
        var entries = new List<byte[]>();
        if (count == 0) return entries;
        int offSize = data[p + 2];
        int dataStart = p + 3 + (count + 1) * offSize - 1;
        for (int i = 0; i < count; i++)
        {
            int start = ReadOffset(data, p + 3 + i * offSize, offSize), end = ReadOffset(data, p + 3 + (i + 1) * offSize, offSize);
            entries.Add(data.AsSpan(dataStart + start, end - start).ToArray());
        }
        return entries;
    }

    private static int ReadOffset(byte[] data, int p, int size)
    {
        int value = 0;
        for (int i = 0; i < size; i++) value = (value << 8) | data[p + i];
        return value;
    }
}

/// <summary>
/// Reads back a PDF for tests: objects, the page tree, page fonts, embedded composite fonts, and page text (D-005).
/// Text extraction follows <c>Tf</c>, <c>Tj</c>, <c>TJ</c>, <c>'</c>, <c>"</c>, and marked content: inside a
/// <c>/Span &lt;&lt; /ActualText ... &gt;&gt; BDC ... EMC</c> span the actual text replaces the glyphs' own text, as
/// PDF viewers use it for copy, search, and screen readers.
/// </summary>
internal sealed class PdfInspector
{
    #region Fields and Constants

    // WinAnsi codes 128 to 159 (zero where undefined); other codes are Latin-1.
    private static readonly char[] WinAnsiHigh =
    {
        '€', '\0', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\0', 'Ž', '\0',
        '\0', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\0', 'ž', 'Ÿ'
    };

    private readonly IReadOnlyDictionary<int, (string Dictionary, byte[]? Stream)> _objects;

    #endregion

    #region Constructors and Finalizers

    private PdfInspector(IReadOnlyDictionary<int, (string, byte[]?)> objects, int root)
    {
        _objects = objects;
        Root = root;
    }

    #endregion

    #region Methods

    #region Public Methods

    public static PdfInspector Read(byte[] pdf)
    {
        var (objects, root) = PdfStructureChecker.ReadObjects(pdf);
        return new PdfInspector(objects, root);
    }

    /// <summary>
    /// Returns an object's dictionary (empty when the object is not a dictionary).
    /// </summary>
    public Dictionary<string, object?> Dictionary(int number) =>
        _objects.TryGetValue(number, out var o) && PdfSyntax.ParseObject(o.Dictionary) is Dictionary<string, object?> d ? d : new(StringComparer.Ordinal);

    public bool Exists(int number) => _objects.ContainsKey(number);

    /// <summary>
    /// Returns every Type0 font in the file, read back with its parts.
    /// </summary>
    public List<EmbeddedFont> Fonts()
    {
        var fonts = new List<EmbeddedFont>();
        foreach (int number in _objects.Keys.OrderBy(n => n))
        {
            var font = Dictionary(number);
            if (!IsName(font, "Type", "Font") || !IsName(font, "Subtype", "Type0")) continue;
            fonts.Add(ReadFont(number, font));
        }
        return fonts;
    }

    /// <summary>
    /// Returns the text of each page, one line per text object (BT ... ET).
    /// </summary>
    /// <param name="errors">Receives problems found: unknown fonts, unmapped glyph codes, unbalanced marked content.</param>
    public List<string> PageText(List<string>? errors = null)
    {
        errors ??= new List<string>();
        var pages = new List<string>();
        foreach (var page in Pages()) pages.Add(ExtractText(page, errors));
        return pages;
    }

    /// <summary>
    /// Returns the page objects in page order.
    /// </summary>
    public List<int> Pages()
    {
        var pages = new List<int>();
        var catalog = Dictionary(Root);
        if (catalog.GetValueOrDefault("Pages") is PdfRef pagesRef) CollectPages(pagesRef.Number, pages, depth: 0);
        return pages;
    }

    /// <summary>
    /// Returns a stream's data, inflated when it has <c>/Filter /FlateDecode</c>.
    /// </summary>
    public byte[] StreamData(int number)
    {
        if (!_objects.TryGetValue(number, out var o) || o.Stream is null) throw new InvalidOperationException($"Object {number} is not a stream.");
        var dictionary = Dictionary(number);
        if (!IsName(dictionary, "Filter", "FlateDecode")) return o.Stream;
        using var input = new ZLibStream(new MemoryStream(o.Stream), CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Decodes a PDF text string: UTF-16BE with a byte-order mark, else PDFDocEncoding read as Latin-1.
    /// </summary>
    public static string TextString(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF ? Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2) : Encoding.Latin1.GetString(bytes);

    /// <summary>
    /// Reads a <c>ToUnicode</c> CMap's <c>bfchar</c> and <c>bfrange</c> entries.
    /// </summary>
    public static Dictionary<int, string> ParseToUnicode(byte[] cmap)
    {
        var map = new Dictionary<int, string>();
        var tokens = PdfSyntax.Tokens(cmap);
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is not PdfOperator op) continue;
            if (op.Value == "beginbfchar")
            {
                for (i++; i + 1 < tokens.Count && tokens[i] is byte[] source; i += 2) map[Code(source)] = Utf16(tokens[i + 1]);
            }
            else if (op.Value == "beginbfrange")
            {
                for (i++; i + 2 < tokens.Count && tokens[i] is byte[] low; i += 3)
                {
                    int lo = Code(low), hi = Code((byte[])tokens[i + 1]!);
                    if (tokens[i + 2] is List<object?> list)
                    {
                        for (int c = lo; c <= hi; c++) map[c] = Utf16(list[c - lo]);
                    }
                    else
                    {
                        var destination = (byte[])tokens[i + 2]!;
                        for (int c = lo; c <= hi; c++)
                        {
                            var bytes = (byte[])destination.Clone();
                            int last = (bytes[^2] << 8 | bytes[^1]) + (c - lo);
                            bytes[^2] = (byte)(last >> 8);
                            bytes[^1] = (byte)last;
                            map[c] = Encoding.BigEndianUnicode.GetString(bytes);
                        }
                    }
                }
            }
        }
        return map;
    }

    #endregion

    #region Private Methods

    private static int Code(byte[] bytes) => bytes.Aggregate(0, (value, b) => (value << 8) | b);

    private static bool IsName(Dictionary<string, object?> d, string key, string value) => d.GetValueOrDefault(key) is PdfName n && n.Value == value;

    private static string Utf16(object? value) => value is byte[] bytes ? Encoding.BigEndianUnicode.GetString(bytes) : string.Empty;

    private void CollectPages(int number, List<int> pages, int depth)
    {
        if (depth > 32) return;
        var node = Dictionary(number);
        if (IsName(node, "Type", "Page")) { pages.Add(number); return; }
        if (node.GetValueOrDefault("Kids") is not List<object?> kids) return;
        foreach (var kid in kids.OfType<PdfRef>()) CollectPages(kid.Number, pages, depth + 1);
    }

    private object? Resolve(object? value) => value is PdfRef r ? PdfSyntax.ParseObject(_objects.TryGetValue(r.Number, out var o) ? o.Dictionary : "null") : value;

    private EmbeddedFont ReadFont(int number, Dictionary<string, object?> font)
    {
        var descendants = (List<object?>)Resolve(font["DescendantFonts"])!;
        int cidNumber = ((PdfRef)descendants[0]!).Number;
        var cidFont = Dictionary(cidNumber);
        var descriptor = Dictionary(((PdfRef)cidFont["FontDescriptor"]!).Number);
        string key = descriptor.ContainsKey("FontFile2") ? "FontFile2" : "FontFile3";
        var program = StreamData(((PdfRef)descriptor[key]!).Number);
        var toUnicode = font.GetValueOrDefault("ToUnicode") is PdfRef map ? ParseToUnicode(StreamData(map.Number)) : new Dictionary<int, string>();
        return new EmbeddedFont(number, ((PdfName)font["BaseFont"]!).Value, ((PdfName)cidFont["Subtype"]!).Value, descriptor, Widths(cidFont), toUnicode, program, key);
    }

    // The /W array: c [w1 w2 ...] or c_first c_last w.
    private Dictionary<int, double> Widths(Dictionary<string, object?> cidFont)
    {
        var widths = new Dictionary<int, double>();
        if (Resolve(cidFont.GetValueOrDefault("W")) is not List<object?> w) return widths;
        for (int i = 0; i < w.Count;)
        {
            int first = (int)(double)w[i]!;
            if (w[i + 1] is List<object?> list)
            {
                for (int j = 0; j < list.Count; j++) widths[first + j] = (double)list[j]!;
                i += 2;
            }
            else
            {
                int last = (int)(double)w[i + 1]!;
                for (int c = first; c <= last; c++) widths[c] = (double)w[i + 2]!;
                i += 3;
            }
        }
        return widths;
    }

    private string ExtractText(int page, List<string> errors)
    {
        var pageDict = Dictionary(page);
        var resources = Resolve(pageDict.GetValueOrDefault("Resources")) as Dictionary<string, object?> ?? new(StringComparer.Ordinal);
        var fontResources = Resolve(resources.GetValueOrDefault("Font")) as Dictionary<string, object?> ?? new(StringComparer.Ordinal);
        var decoders = new Dictionary<string, Func<byte[], List<string>, string>>(StringComparer.Ordinal);

        var content = new List<byte>();
        foreach (var stream in Resolve(pageDict.GetValueOrDefault("Contents")) is List<object?> list ? list.OfType<PdfRef>() : pageDict.GetValueOrDefault("Contents") is PdfRef single ? new[] { single } : Array.Empty<PdfRef>())
        {
            content.AddRange(StreamData(stream.Number));
            content.Add((byte)'\n');
        }

        var lines = new List<string>();
        var line = new StringBuilder();
        var operands = new List<object?>();
        var spans = new Stack<(string? Actual, bool Written)>();
        Func<byte[], List<string>, string>? decode = null;
        string fontName = string.Empty;

        void Show(byte[] bytes)
        {
            if (decode is null) { errors.Add($"Page {page}: text is shown before any font is set."); return; }
            string text = decode(bytes, errors);
            if (spans.Any(s => s.Actual is not null)) return; // the span's actual text replaces the glyphs' text
            line.Append(text);
        }

        foreach (var token in PdfSyntax.Tokens(content.ToArray()))
        {
            if (token is not PdfOperator op) { operands.Add(token); continue; }
            switch (op.Value)
            {
                case "BT":
                    line.Clear();
                    break;
                case "ET":
                    lines.Add(line.ToString());
                    line.Clear();
                    break;
                case "Tf" when operands.Count >= 2 && operands[^2] is PdfName name:
                    fontName = name.Value;
                    if (!decoders.TryGetValue(fontName, out decode))
                    {
                        decode = Decoder(fontResources, fontName, page, errors);
                        if (decode is not null) decoders[fontName] = decode;
                    }
                    break;
                case "Tj" or "'" when operands.Count >= 1 && operands[^1] is byte[] s:
                    Show(s);
                    break;
                case "\"" when operands.Count >= 3 && operands[^1] is byte[] s:
                    Show(s);
                    break;
                case "TJ" when operands.Count >= 1 && operands[^1] is List<object?> array:
                    foreach (var item in array) if (item is byte[] part) Show(part);
                    break;
                case "BDC":
                {
                    string? actual = operands.Count >= 1 && Resolve(operands[^1]) is Dictionary<string, object?> props && props.GetValueOrDefault("ActualText") is byte[] a ? TextString(a) : null;
                    spans.Push((actual, false));
                    if (actual is not null) line.Append(actual);
                    break;
                }
                case "BMC":
                    spans.Push((null, false));
                    break;
                case "EMC":
                    if (spans.Count == 0) errors.Add($"Page {page}: EMC without a matching BDC or BMC.");
                    else spans.Pop();
                    break;
            }
            operands.Clear();
        }
        if (spans.Count > 0) errors.Add($"Page {page}: marked content is not closed.");
        if (line.Length > 0) lines.Add(line.ToString());
        return string.Join("\n", lines);
    }

    private Func<byte[], List<string>, string>? Decoder(Dictionary<string, object?> fontResources, string name, int page, List<string> errors)
    {
        if (fontResources.GetValueOrDefault(name) is not PdfRef reference || !Exists(reference.Number))
        {
            errors.Add($"Page {page}: font resource /{name} is missing from the page's resources.");
            return null;
        }
        var font = Dictionary(reference.Number);
        if (IsName(font, "Subtype", "Type0"))
        {
            var map = font.GetValueOrDefault("ToUnicode") is PdfRef cmap ? ParseToUnicode(StreamData(cmap.Number)) : new Dictionary<int, string>();
            return (bytes, errs) =>
            {
                var sb = new StringBuilder();
                for (int i = 0; i + 1 < bytes.Length; i += 2)
                {
                    int code = (bytes[i] << 8) | bytes[i + 1];
                    if (map.TryGetValue(code, out var text)) sb.Append(text);
                    else { sb.Append('�'); errs.Add($"Page {page}: glyph code {code} of font /{name} has no ToUnicode entry."); }
                }
                return sb.ToString();
            };
        }
        return (bytes, _) => new string(bytes.Select(b => b is >= 0x80 and <= 0x9F ? WinAnsiHigh[b - 0x80] : (char)b).ToArray());
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the numbers of the file's objects, in increasing order.
    /// </summary>
    public IEnumerable<int> ObjectNumbers => _objects.Keys.OrderBy(n => n);

    public int Root { get; }

    #endregion
}
