using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// A <c>/ToUnicode</c> CMap: the characters each 2-byte code stands for, read from its <c>bfchar</c> and
/// <c>bfrange</c> sections.
/// </summary>
internal sealed class ToUnicodeMap
{
    #region Fields and Constants

    // The most code mappings read; a CMap for 2-byte codes needs at most 65,536.
    private const int MaxEntries = 65_536;

    private readonly Dictionary<int, string> _map = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads a CMap.
    /// </summary>
    /// <param name="data">The decoded CMap stream.</param>
    /// <returns>The map.</returns>
    /// <exception cref="PageRenderException">The CMap is damaged or too large.</exception>
    public static ToUnicodeMap Read(byte[] data)
    {
        var map = new ToUnicodeMap();
        var lexer = new PdfLexer(data);
        var tokens = new List<PdfToken>();
        string? section = null;
        try
        {
            for (var token = lexer.Next(); token.Kind != PdfTokenKind.EndOfFile; token = lexer.Next())
            {
                if (token.Kind == PdfTokenKind.Keyword)
                {
                    switch (token.Text)
                    {
                        case "beginbfchar": case "beginbfrange": section = token.Text; tokens.Clear(); break;
                        case "endbfchar": map.ReadChars(tokens); section = null; break;
                        case "endbfrange": map.ReadRanges(tokens); section = null; break;
                    }
                }
                else if (section is not null)
                {
                    tokens.Add(token);
                    if (tokens.Count > 4 * MaxEntries) throw new PageRenderException("A font's text map is too large.");
                }
            }
        }
        catch (PdfReadException exception)
        {
            throw new PageRenderException($"A font's text map is damaged: {exception.Message}");
        }
        return map;
    }

    /// <summary>
    /// Returns the characters code <paramref name="code"/> stands for, or an empty string.
    /// </summary>
    public string Text(int code) => _map.TryGetValue(code, out var text) ? text : string.Empty;

    #endregion

    #region Private Methods

    private static int Code(PdfToken token) => token.Kind == PdfTokenKind.HexString && token.Bytes is { Length: >= 1 and <= 2 } bytes
        ? bytes.Length == 2 ? (bytes[0] << 8) | bytes[1] : bytes[0]
        : throw new PageRenderException("A font's text map has a code that isn't one or two bytes.");

    private static string Utf16(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length / 2);
        for (int i = 0; i + 1 < bytes.Length; i += 2) text.Append((char)((bytes[i] << 8) | bytes[i + 1]));
        return text.ToString();
    }

    private void Add(int code, string text)
    {
        if (_map.Count >= MaxEntries && !_map.ContainsKey(code)) throw new PageRenderException("A font's text map is too large.");
        _map[code] = text;
    }

    // Pairs: <code> <utf16>.
    private void ReadChars(List<PdfToken> tokens)
    {
        for (int i = 0; i + 1 < tokens.Count; i += 2)
        {
            if (tokens[i + 1].Kind != PdfTokenKind.HexString) throw new PageRenderException("A font's text map is damaged.");
            Add(Code(tokens[i]), Utf16(tokens[i + 1].Bytes!));
        }
        tokens.Clear();
    }

    // Triples: <low> <high> <utf16>, the last character counting up; or <low> <high> [<utf16> ...], one each.
    private void ReadRanges(List<PdfToken> tokens)
    {
        int i = 0;
        while (i + 2 < tokens.Count)
        {
            int low = Code(tokens[i]), high = Code(tokens[i + 1]);
            if (high < low || high - low >= MaxEntries) throw new PageRenderException("A font's text map has a bad range.");
            if (tokens[i + 2].Kind == PdfTokenKind.HexString)
            {
                string start = Utf16(tokens[i + 2].Bytes!);
                if (start.Length == 0) throw new PageRenderException("A font's text map has an empty range.");
                for (int code = low; code <= high; code++)
                {
                    Add(code, start.Substring(0, start.Length - 1) + (char)(start[^1] + (code - low)));
                }
                i += 3;
            }
            else if (tokens[i + 2].Kind == PdfTokenKind.ArrayStart)
            {
                int j = i + 3;
                for (int code = low; j < tokens.Count && tokens[j].Kind == PdfTokenKind.HexString; code++, j++)
                {
                    if (code <= high) Add(code, Utf16(tokens[j].Bytes!));
                }
                if (j >= tokens.Count || tokens[j].Kind != PdfTokenKind.ArrayEnd) throw new PageRenderException("A font's text map is damaged.");
                i = j + 1;
            }
            else
            {
                throw new PageRenderException("A font's text map is damaged.");
            }
        }
        tokens.Clear();
    }

    #endregion

    #endregion
}
