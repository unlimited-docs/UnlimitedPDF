namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Splits PDF bytes into tokens, starting at a position that the caller can move.
/// </summary>
internal sealed class PdfLexer
{
    #region Fields and Constants

    private readonly byte[] _data;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates a lexer over <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="position">Where to start.</param>
    public PdfLexer(byte[] data, int position = 0)
    {
        _data = data;
        Position = position;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether <paramref name="value"/> is a PDF delimiter character.
    /// </summary>
    public static bool IsDelimiter(byte value) => value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'['
        or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    /// <summary>
    /// Returns whether <paramref name="value"/> is a PDF white-space character.
    /// </summary>
    public static bool IsWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    /// <summary>
    /// Reads the next token.
    /// </summary>
    /// <returns>The token; <see cref="PdfTokenKind.EndOfFile"/> at the end of the data.</returns>
    /// <exception cref="PdfReadException">The syntax is invalid.</exception>
    public PdfToken Next()
    {
        SkipWhitespaceAndComments();
        int start = Position;
        if (Position >= _data.Length) return new PdfToken(PdfTokenKind.EndOfFile, start, start);

        switch (_data[Position])
        {
            case (byte)'[':
                Position++;
                return new PdfToken(PdfTokenKind.ArrayStart, start, Position);
            case (byte)']':
                Position++;
                return new PdfToken(PdfTokenKind.ArrayEnd, start, Position);
            case (byte)'<':
                if (ByteAt(Position + 1) == '<')
                {
                    Position += 2;
                    return new PdfToken(PdfTokenKind.DictionaryStart, start, Position);
                }
                return ReadHexString();
            case (byte)'>':
                if (ByteAt(Position + 1) == '>')
                {
                    Position += 2;
                    return new PdfToken(PdfTokenKind.DictionaryEnd, start, Position);
                }
                throw PdfReadException.Damaged($"Unexpected '>' at offset {start}.");
            case (byte)'(':
                return ReadLiteralString();
            case (byte)'/':
                return ReadName();
            case (byte)')':
            case (byte)'{':
            case (byte)'}':
                throw PdfReadException.Damaged($"Unexpected '{(char)_data[Position]}' at offset {start}.");
        }

        while (Position < _data.Length && !IsWhitespace(_data[Position]) && !IsDelimiter(_data[Position])) Position++;
        string text = Encoding.ASCII.GetString(_data, start, Position - start);
        if (IsNumber(text, out bool isInteger))
            return new PdfToken(isInteger ? PdfTokenKind.Integer : PdfTokenKind.Real, start, Position, text);
        return new PdfToken(PdfTokenKind.Keyword, start, Position, text);
    }

    /// <summary>
    /// Moves past white space and comments.
    /// </summary>
    public void SkipWhitespaceAndComments()
    {
        while (Position < _data.Length)
        {
            byte value = _data[Position];
            if (IsWhitespace(value))
            {
                Position++;
            }
            else if (value == '%')
            {
                while (Position < _data.Length && _data[Position] != '\n' && _data[Position] != '\r') Position++;
            }
            else
            {
                return;
            }
        }
    }

    #endregion

    #region Private Methods

    private int ByteAt(int index) => index < _data.Length ? _data[index] : -1;

    private static int HexValue(int value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => -1,
    };

    // A number is an optional sign, digits, and at most one decimal point, with at least one digit.
    private static bool IsNumber(string text, out bool isInteger)
    {
        isInteger = true;
        int index = 0;
        while (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
        if (index > 1 || index == text.Length) return false;

        bool digit = false;
        for (; index < text.Length; index++)
        {
            char c = text[index];
            if (c >= '0' && c <= '9')
            {
                digit = true;
            }
            else if (c == '.' && isInteger)
            {
                isInteger = false;
            }
            else
            {
                return false;
            }
        }
        return digit;
    }

    // Reads the escape after a backslash: a named escape, up to three octal digits, or a line continuation. An unknown
    // escape keeps the character and drops the backslash.
    private void ReadEscape(List<byte> bytes)
    {
        if (Position >= _data.Length) return;
        byte value = _data[Position++];
        switch (value)
        {
            case (byte)'n': bytes.Add((byte)'\n'); break;
            case (byte)'r': bytes.Add((byte)'\r'); break;
            case (byte)'t': bytes.Add((byte)'\t'); break;
            case (byte)'b': bytes.Add((byte)'\b'); break;
            case (byte)'f': bytes.Add((byte)'\f'); break;
            case (byte)'\r':
                if (ByteAt(Position) == '\n') Position++;
                break;
            case (byte)'\n':
                break;
            case >= (byte)'0' and <= (byte)'7':
                int code = value - '0';
                for (int i = 0; i < 2 && ByteAt(Position) is >= '0' and <= '7'; i++)
                    code = (code << 3) | (_data[Position++] - '0');
                bytes.Add((byte)code);
                break;
            default:
                bytes.Add(value);
                break;
        }
    }

    private PdfToken ReadHexString()
    {
        int start = Position;
        Position++;
        var bytes = new List<byte>();
        int high = -1;
        while (Position < _data.Length)
        {
            byte value = _data[Position++];
            if (value == '>')
            {
                if (high >= 0) bytes.Add((byte)(high << 4));
                return new PdfToken(PdfTokenKind.HexString, start, Position, Bytes: bytes.ToArray());
            }
            if (IsWhitespace(value)) continue;

            int digit = HexValue(value);
            if (digit < 0) throw PdfReadException.Damaged($"Invalid hexadecimal string at offset {start}.");
            if (high < 0)
            {
                high = digit;
            }
            else
            {
                bytes.Add((byte)((high << 4) | digit));
                high = -1;
            }
        }
        throw PdfReadException.Damaged($"Unterminated hexadecimal string at offset {start}.");
    }

    private PdfToken ReadLiteralString()
    {
        int start = Position;
        Position++;
        var bytes = new List<byte>();
        int depth = 1;
        while (Position < _data.Length)
        {
            byte value = _data[Position++];
            switch (value)
            {
                case (byte)'\\':
                    ReadEscape(bytes);
                    break;
                case (byte)'(':
                    depth++;
                    bytes.Add(value);
                    break;
                case (byte)')':
                    if (--depth == 0) return new PdfToken(PdfTokenKind.LiteralString, start, Position, Bytes: bytes.ToArray());
                    bytes.Add(value);
                    break;
                case (byte)'\r':
                    // An unescaped end of line reads as a single line feed.
                    bytes.Add((byte)'\n');
                    if (ByteAt(Position) == '\n') Position++;
                    break;
                default:
                    bytes.Add(value);
                    break;
            }
        }
        throw PdfReadException.Damaged($"Unterminated string at offset {start}.");
    }

    private PdfToken ReadName()
    {
        int start = Position;
        Position++;
        var bytes = new List<byte>();
        while (Position < _data.Length && !IsWhitespace(_data[Position]) && !IsDelimiter(_data[Position]))
        {
            byte value = _data[Position++];
            int high = value == '#' ? HexValue(ByteAt(Position)) : -1;
            int low = high >= 0 ? HexValue(ByteAt(Position + 1)) : -1;
            if (low >= 0)
            {
                bytes.Add((byte)((high << 4) | low));
                Position += 2;
            }
            else
            {
                bytes.Add(value);
            }
        }
        return new PdfToken(PdfTokenKind.Name, start, Position, Encoding.Latin1.GetString(bytes.ToArray()));
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the data being read.
    /// </summary>
    public byte[] Data => _data;

    /// <summary>
    /// Gets or sets the offset of the next byte to read.
    /// </summary>
    public int Position { get; set; }

    #endregion
}
