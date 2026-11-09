namespace UnlimitedPDF.Models.Internal.Html.Parsing;

/// <summary>
/// The kind of an HTML token.
/// </summary>
internal enum HtmlTokenKind
{
    StartTag,
    EndTag,
    Text,
    Comment,
    Doctype
}

/// <summary>
/// One HTML token: a start or end tag with lowercase name and attributes, text with character references decoded, a
/// comment, or a doctype.
/// </summary>
internal sealed class HtmlToken
{
    #region Constructors and Finalizers

    public HtmlToken(HtmlTokenKind kind, string value, int line)
    {
        Kind = kind;
        Value = value;
        Line = line;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the attributes of a start tag in source order; a repeated name keeps its first value.
    /// </summary>
    public List<KeyValuePair<string, string>> Attributes { get; } = new();

    public HtmlTokenKind Kind { get; }

    /// <summary>
    /// Gets the 1-based line the token starts on.
    /// </summary>
    public int Line { get; }

    public bool SelfClosing { get; set; }

    /// <summary>
    /// Gets the tag name (lowercase) or the text.
    /// </summary>
    public string Value { get; }

    #endregion
}

/// <summary>
/// Splits HTML text into tokens following the HTML Standard's tokenizer for the parts the library uses (WR-038): tags
/// and attributes in every quoting style, comments, doctypes, bogus comments, raw text in <c>script</c> and
/// <c>style</c> and the other raw-text elements, escapable raw text in <c>title</c> and <c>textarea</c>, and character
/// references. It never fails: anything malformed becomes text or is skipped, as browsers do.
/// </summary>
internal sealed class HtmlTokenizer
{
    #region Fields and Constants

    // Elements whose content is raw text up to the matching end tag; references are decoded only in the escapable ones.
    private static readonly HashSet<string> RawTextElements = new(StringComparer.Ordinal) { "script", "style", "xmp", "iframe", "noembed", "noframes", "noscript" };
    private static readonly HashSet<string> EscapableRawTextElements = new(StringComparer.Ordinal) { "title", "textarea" };

    private readonly string _text;
    private int _line = 1;
    private int _position;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the tokenizer.
    /// </summary>
    /// <param name="text">The HTML text.</param>
    public HtmlTokenizer(string text)
    {
        _text = text.IndexOf('\r') >= 0 ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the tokens in order.
    /// </summary>
    /// <returns>The tokens.</returns>
    public IEnumerable<HtmlToken> Tokens()
    {
        var text = new StringBuilder();
        int textLine = _line;
        while (_position < _text.Length)
        {
            char c = _text[_position];
            if (c == '<' && TryReadMarkup(out var token))
            {
                if (text.Length > 0)
                {
                    yield return new HtmlToken(HtmlTokenKind.Text, text.ToString(), textLine);
                    text.Clear();
                }
                yield return token!;
                if (token!.Kind == HtmlTokenKind.StartTag && !token.SelfClosing &&
                    (RawTextElements.Contains(token.Value) || EscapableRawTextElements.Contains(token.Value)))
                {
                    var raw = ReadRawText(token.Value, EscapableRawTextElements.Contains(token.Value));
                    if (raw is not null) yield return raw;
                }
                textLine = _line;
                continue;
            }

            if (text.Length == 0) textLine = _line;
            if (c == '&' && HtmlEntities.Read(_text, _position, inAttribute: false, out int length) is { } decoded)
            {
                text.Append(decoded);
                _position += length;
                continue;
            }
            if (c == '\n') _line++;
            if (c != '\0') text.Append(c);
            _position++;
        }
        if (text.Length > 0) yield return new HtmlToken(HtmlTokenKind.Text, text.ToString(), textLine);
    }

    #endregion

    #region Private Methods

    private void Advance(int count)
    {
        int end = Math.Min(_text.Length, _position + count);
        for (int i = _position; i < end; i++)
            if (_text[i] == '\n') _line++;
        _position = end;
    }

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\f';

    // Reads the text of a raw-text element up to its end tag, leaving the end tag to be read next.
    private HtmlToken? ReadRawText(string name, bool decode)
    {
        int line = _line;
        int end = _position;
        while (true)
        {
            end = _text.IndexOf("</", end, StringComparison.Ordinal);
            if (end < 0) { end = _text.Length; break; }
            int after = end + 2 + name.Length;
            if (after <= _text.Length && string.Compare(_text, end + 2, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                (after == _text.Length || IsSpace(_text[after]) || _text[after] == '>' || _text[after] == '/'))
                break;
            end += 2;
        }
        string raw = _text.Substring(_position, end - _position);
        Advance(end - _position);
        if (raw.Length == 0) return null;
        if (decode) raw = DecodeReferences(raw, inAttribute: false);
        return new HtmlToken(HtmlTokenKind.Text, raw, line);
    }

    private static string DecodeReferences(string value, bool inAttribute)
    {
        if (value.IndexOf('&') < 0) return value;
        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '&' && HtmlEntities.Read(value, i, inAttribute, out int length) is { } decoded)
            {
                sb.Append(decoded);
                i += length - 1;
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }

    // Reads a tag, comment, or doctype at '<'. Returns false when the '<' is plain text.
    private bool TryReadMarkup(out HtmlToken? token)
    {
        token = null;
        int line = _line;
        int next = _position + 1;
        if (next >= _text.Length) return false;
        char c = _text[next];

        if (c == '!')
        {
            if (string.CompareOrdinal(_text, next + 1, "--", 0, 2) == 0)
            {
                int close = _text.IndexOf("-->", next + 3, StringComparison.Ordinal);
                int stop = close < 0 ? _text.Length : close + 3;
                string body = _text.Substring(next + 3, Math.Max(0, (close < 0 ? _text.Length : close) - next - 3));
                Advance(stop - _position);
                token = new HtmlToken(HtmlTokenKind.Comment, body, line);
                return true;
            }
            int end = _text.IndexOf('>', next);
            string inner = _text.Substring(next + 1, (end < 0 ? _text.Length : end) - next - 1);
            Advance((end < 0 ? _text.Length : end + 1) - _position);
            token = inner.StartsWith("doctype", StringComparison.OrdinalIgnoreCase)
                ? new HtmlToken(HtmlTokenKind.Doctype, inner, line)
                : new HtmlToken(HtmlTokenKind.Comment, inner, line);
            return true;
        }
        if (c == '?')
        {
            int end = _text.IndexOf('>', next);
            Advance((end < 0 ? _text.Length : end + 1) - _position);
            token = new HtmlToken(HtmlTokenKind.Comment, string.Empty, line);
            return true;
        }

        bool endTag = c == '/';
        int nameStart = endTag ? next + 1 : next;
        if (nameStart >= _text.Length) return false;
        if (!IsAsciiLetter(_text[nameStart]))
        {
            if (endTag)
            {
                // "</>" is dropped; "</ 3>" and other bogus end tags become comments.
                int end = _text.IndexOf('>', nameStart);
                Advance((end < 0 ? _text.Length : end + 1) - _position);
                token = new HtmlToken(HtmlTokenKind.Comment, string.Empty, line);
                return true;
            }
            return false;
        }

        int i = nameStart;
        while (i < _text.Length && !IsSpace(_text[i]) && _text[i] != '/' && _text[i] != '>') i++;
        string name = _text.Substring(nameStart, i - nameStart).ToLowerInvariant();
        token = new HtmlToken(endTag ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag, name, line);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (i < _text.Length)
        {
            while (i < _text.Length && IsSpace(_text[i])) i++;
            if (i >= _text.Length) break;
            if (_text[i] == '>') { i++; break; }
            if (_text[i] == '/')
            {
                i++;
                if (i < _text.Length && _text[i] == '>') { token.SelfClosing = true; i++; break; }
                continue;
            }

            int attributeStart = i;
            i++;
            while (i < _text.Length && !IsSpace(_text[i]) && _text[i] != '/' && _text[i] != '>' && _text[i] != '=') i++;
            string attributeName = _text.Substring(attributeStart, i - attributeStart).ToLowerInvariant();
            while (i < _text.Length && IsSpace(_text[i])) i++;
            string value = string.Empty;
            if (i < _text.Length && _text[i] == '=')
            {
                i++;
                while (i < _text.Length && IsSpace(_text[i])) i++;
                if (i < _text.Length && (_text[i] == '"' || _text[i] == '\''))
                {
                    char quote = _text[i++];
                    int close = _text.IndexOf(quote, i);
                    if (close < 0) close = _text.Length;
                    value = _text.Substring(i, close - i);
                    i = Math.Min(_text.Length, close + 1);
                }
                else
                {
                    int valueStart = i;
                    while (i < _text.Length && !IsSpace(_text[i]) && _text[i] != '>') i++;
                    value = _text.Substring(valueStart, i - valueStart);
                }
            }
            if (!endTag && seen.Add(attributeName))
                token.Attributes.Add(new KeyValuePair<string, string>(attributeName, DecodeReferences(value, inAttribute: true).Replace("\0", string.Empty)));
        }

        Advance(i - _position);
        return true;
    }

    #endregion

    #endregion
}
