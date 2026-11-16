namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// A reference to an indirect object (<c>n 0 R</c>).
/// </summary>
internal sealed record PdfRef(int Number);

/// <summary>
/// A PDF name (<c>/Name</c>), without the slash.
/// </summary>
internal sealed record PdfName(string Value);

/// <summary>
/// A content-stream operator (<c>Tj</c>, <c>BT</c>, ...).
/// </summary>
internal sealed record PdfOperator(string Value);

/// <summary>
/// Reads PDF objects and content-stream tokens for the test checker: numbers (as double), names, literal and hex
/// strings (as bytes), arrays (lists), dictionaries, references, booleans, null, and operators. It is independent of
/// the library's own reader (PR-010).
/// </summary>
internal sealed class PdfSyntax
{
    #region Fields and Constants

    private readonly byte[] _data;
    private int _position;

    #endregion

    #region Constructors and Finalizers

    public PdfSyntax(byte[] data, int position = 0)
    {
        _data = data;
        _position = position;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses one object (a dictionary, array, or value) from text such as an object's dictionary.
    /// </summary>
    public static object? ParseObject(string text) => new PdfSyntax(Encoding.Latin1.GetBytes(text)).Next();

    /// <summary>
    /// Returns every token of a content stream, objects already assembled (arrays, inline dictionaries).
    /// </summary>
    public static List<object?> Tokens(byte[] content)
    {
        var syntax = new PdfSyntax(content);
        var tokens = new List<object?>();
        while (true)
        {
            syntax.SkipWhitespace();
            if (syntax._position >= content.Length) return tokens;
            tokens.Add(syntax.Next());
        }
    }

    /// <summary>
    /// Reads the next object; references (<c>n g R</c>) are recognized inside arrays and dictionaries.
    /// </summary>
    public object? Next()
    {
        SkipWhitespace();
        if (_position >= _data.Length) throw new FormatException("Unexpected end of data.");
        byte b = _data[_position];
        switch (b)
        {
            case (byte)'/':
                _position++;
                return new PdfName(ReadRegular());
            case (byte)'(':
                return ReadLiteralString();
            case (byte)'[':
            {
                _position++;
                var items = new List<object?>();
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() == ']') { _position++; return Fold(items); }
                    items.Add(Next());
                }
            }
            case (byte)'<' when Peek(1) == '<':
            {
                _position += 2;
                var items = new List<object?>();
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() == '>' && Peek(1) == '>') { _position += 2; break; }
                    items.Add(Next());
                }
                items = Fold(items);
                var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (int i = 0; i + 1 < items.Count; i += 2)
                {
                    if (items[i] is not PdfName key) throw new FormatException("A dictionary key is not a name.");
                    dictionary[key.Value] = items[i + 1];
                }
                return dictionary;
            }
            case (byte)'<':
                return ReadHexString();
        }

        string token = ReadRegular();
        if (token.Length == 0) throw new FormatException($"Unexpected byte 0x{b:X2}.");
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
        return token switch
        {
            "true" => true,
            "false" => false,
            "null" => null,
            _ => new PdfOperator(token)
        };
    }

    #endregion

    #region Private Methods

    // Turns "n g R" triples in an array or dictionary body into references.
    private static List<object?> Fold(List<object?> items)
    {
        var folded = new List<object?>();
        for (int i = 0; i < items.Count; i++)
        {
            if (i + 2 < items.Count && items[i] is double n && items[i + 1] is double && items[i + 2] is PdfOperator { Value: "R" })
            {
                folded.Add(new PdfRef((int)n));
                i += 2;
            }
            else
            {
                folded.Add(items[i]);
            }
        }
        return folded;
    }

    private static bool IsDelimiter(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private static bool IsWhitespace(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;

    private char Peek(int ahead = 0) => _position + ahead < _data.Length ? (char)_data[_position + ahead] : '\0';

    private byte[] ReadHexString()
    {
        _position++;
        var digits = new StringBuilder();
        while (_position < _data.Length && _data[_position] != '>')
        {
            char c = (char)_data[_position++];
            if (Uri.IsHexDigit(c)) digits.Append(c);
            else if (!IsWhitespace((byte)c)) throw new FormatException($"Invalid hex string character '{c}'.");
        }
        if (_position >= _data.Length) throw new FormatException("Unterminated hex string.");
        _position++;
        if (digits.Length % 2 == 1) digits.Append('0');
        return Convert.FromHexString(digits.ToString());
    }

    private byte[] ReadLiteralString()
    {
        _position++;
        var bytes = new List<byte>();
        int depth = 1;
        while (true)
        {
            if (_position >= _data.Length) throw new FormatException("Unterminated literal string.");
            byte b = _data[_position++];
            if (b == '(') depth++;
            else if (b == ')' && --depth == 0) return bytes.ToArray();
            if (b != '\\') { bytes.Add(b); continue; }

            if (_position >= _data.Length) throw new FormatException("Unterminated literal string.");
            byte e = _data[_position++];
            switch (e)
            {
                case (byte)'n': bytes.Add(10); break;
                case (byte)'r': bytes.Add(13); break;
                case (byte)'t': bytes.Add(9); break;
                case (byte)'b': bytes.Add(8); break;
                case (byte)'f': bytes.Add(12); break;
                case (byte)'\r': if (Peek() == '\n') _position++; break;
                case (byte)'\n': break;
                case >= (byte)'0' and <= (byte)'7':
                {
                    int value = e - '0';
                    for (int i = 0; i < 2 && Peek() is >= '0' and <= '7'; i++) value = value * 8 + (_data[_position++] - '0');
                    bytes.Add((byte)value);
                    break;
                }
                default: bytes.Add(e); break;
            }
        }
    }

    private string ReadRegular()
    {
        int start = _position;
        while (_position < _data.Length && !IsWhitespace(_data[_position]) && !IsDelimiter(_data[_position])) _position++;
        return Encoding.Latin1.GetString(_data, start, _position - start);
    }

    private void SkipWhitespace()
    {
        while (_position < _data.Length)
        {
            if (IsWhitespace(_data[_position])) _position++;
            else if (_data[_position] == '%')
            {
                while (_position < _data.Length && _data[_position] is not (10 or 13)) _position++;
            }
            else return;
        }
    }

    #endregion

    #endregion
}
