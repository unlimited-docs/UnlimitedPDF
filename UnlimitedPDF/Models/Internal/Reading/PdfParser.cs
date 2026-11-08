namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Parses PDF objects from a <see cref="PdfLexer"/>: direct values, references, and indirect objects with streams.
/// Nesting deeper than <see cref="MaxDepth"/> fails, so a hostile file cannot exhaust the stack (A06).
/// </summary>
internal sealed class PdfParser
{
    #region Fields and Constants

    /// <summary>
    /// The deepest array and dictionary nesting accepted.
    /// </summary>
    public const int MaxDepth = 256;

    private static readonly byte[] EndStreamKeyword = Encoding.ASCII.GetBytes("endstream");

    private readonly PdfLexer _lexer;
    private readonly Func<int, PdfReadValue>? _resolve;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates a parser.
    /// </summary>
    /// <param name="lexer">The token source.</param>
    /// <param name="resolve">Resolves an object number to its value, for a stream <c>/Length</c> given as a
    /// reference; null when references cannot be resolved.</param>
    public PdfParser(PdfLexer lexer, Func<int, PdfReadValue>? resolve = null)
    {
        _lexer = lexer;
        _resolve = resolve;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses an indirect object, <c>n g obj ... endobj</c>, at the lexer position.
    /// </summary>
    /// <returns>The object number, generation, and value.</returns>
    /// <exception cref="PdfReadException">The object is not valid PDF syntax.</exception>
    public PdfIndirectObject ParseIndirectObject()
    {
        int start = _lexer.Position;
        var number = _lexer.Next();
        var generation = _lexer.Next();
        if (number.Kind != PdfTokenKind.Integer || generation.Kind != PdfTokenKind.Integer || !_lexer.Next().IsKeyword("obj"))
            throw PdfReadException.Damaged($"No object header at offset {start}.");

        var value = ParseValue(0);
        int afterValue = _lexer.Position;
        var token = _lexer.Next();
        if (token.IsKeyword("stream"))
        {
            if (value is not PdfReadDictionary dictionary)
                throw PdfReadException.Damaged($"Stream without a dictionary at offset {start}.");
            value = ReadStream(dictionary);
            afterValue = _lexer.Position;
            token = _lexer.Next();
        }

        // A missing "endobj" is tolerated: the value is complete.
        if (!token.IsKeyword("endobj")) _lexer.Position = afterValue;

        return new PdfIndirectObject(ToInt(number), ToInt(generation), value);
    }

    /// <summary>
    /// Parses one direct value at the lexer position. An integer followed by <c>g R</c> is a reference.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="PdfReadException">The value is not valid PDF syntax.</exception>
    public PdfReadValue ParseValue() => ParseValue(0);

    #endregion

    #region Private Methods

    private static int FindEndStream(byte[] data, int from)
    {
        for (int i = from; i <= data.Length - EndStreamKeyword.Length; i++)
        {
            if (data.AsSpan(i, EndStreamKeyword.Length).SequenceEqual(EndStreamKeyword)) return i;
        }
        return -1;
    }

    private PdfReadValue FromToken(PdfToken token, int depth)
    {
        if (depth > MaxDepth) throw PdfReadException.Damaged($"Objects are nested more than {MaxDepth} levels deep.");

        switch (token.Kind)
        {
            case PdfTokenKind.Integer:
                return ReferenceOrNumber(token);
            case PdfTokenKind.Real:
                return new PdfReadNumber(token.Text!, isInteger: false);
            case PdfTokenKind.LiteralString:
            case PdfTokenKind.HexString:
                return new PdfReadString(token.Bytes!);
            case PdfTokenKind.Name:
                return new PdfReadName(token.Text!);
            case PdfTokenKind.ArrayStart:
                return ReadArray(depth);
            case PdfTokenKind.DictionaryStart:
                return ReadDictionary(depth);
            case PdfTokenKind.Keyword when token.Text == "true":
                return new PdfReadBoolean(true);
            case PdfTokenKind.Keyword when token.Text == "false":
                return new PdfReadBoolean(false);
            case PdfTokenKind.Keyword when token.Text == "null":
                return PdfReadNull.Instance;
            case PdfTokenKind.EndOfFile:
                throw PdfReadException.Damaged("The data ends inside an object.");
            default:
                throw PdfReadException.Damaged($"Unexpected '{token.Text ?? token.Kind.ToString()}' at offset {token.Start}.");
        }
    }

    private PdfReadValue ParseValue(int depth) => FromToken(_lexer.Next(), depth);

    private PdfReadArray ReadArray(int depth)
    {
        var items = new List<PdfReadValue>();
        while (true)
        {
            var token = _lexer.Next();
            if (token.Kind == PdfTokenKind.ArrayEnd) return new PdfReadArray(items);
            items.Add(FromToken(token, depth + 1));
        }
    }

    private PdfReadDictionary ReadDictionary(int depth)
    {
        var dictionary = new PdfReadDictionary();
        while (true)
        {
            var key = _lexer.Next();
            if (key.Kind == PdfTokenKind.DictionaryEnd) return dictionary;
            if (key.Kind != PdfTokenKind.Name)
                throw PdfReadException.Damaged($"Dictionary key expected at offset {key.Start}.");

            var value = _lexer.Next();
            if (value.Kind == PdfTokenKind.DictionaryEnd)
                throw PdfReadException.Damaged($"Dictionary key /{key.Text} has no value at offset {key.Start}.");
            dictionary.Set(key.Text!, FromToken(value, depth + 1));
        }
    }

    // The stream data starts after the end of line that follows "stream". /Length is trusted when "endstream" follows
    // it; otherwise the data runs to the next "endstream".
    private PdfReadStream ReadStream(PdfReadDictionary dictionary)
    {
        byte[] data = _lexer.Data;
        int dataStart = _lexer.Position;
        if (dataStart < data.Length && data[dataStart] == '\r') dataStart++;
        if (dataStart < data.Length && data[dataStart] == '\n') dataStart++;

        long length = StreamLength(dictionary);
        if (length >= 0 && dataStart + length <= data.Length)
        {
            var after = new PdfLexer(data, (int)(dataStart + length));
            if (after.Next().IsKeyword("endstream"))
            {
                _lexer.Position = after.Position;
                return new PdfReadStream(dictionary, new ReadOnlyMemory<byte>(data, dataStart, (int)length));
            }
        }

        int end = FindEndStream(data, dataStart);
        if (end < 0) throw PdfReadException.Damaged($"Stream at offset {dataStart} has no end.");
        _lexer.Position = end + EndStreamKeyword.Length;

        int dataEnd = end;
        if (dataEnd > dataStart && data[dataEnd - 1] == '\n') dataEnd--;
        if (dataEnd > dataStart && data[dataEnd - 1] == '\r') dataEnd--;
        return new PdfReadStream(dictionary, new ReadOnlyMemory<byte>(data, dataStart, dataEnd - dataStart));
    }

    private PdfReadValue ReferenceOrNumber(PdfToken token)
    {
        int restore = _lexer.Position;
        var generation = _lexer.Next();
        if (generation.Kind == PdfTokenKind.Integer && _lexer.Next().IsKeyword("R"))
            return new PdfReadReference(ToInt(token), ToInt(generation));

        _lexer.Position = restore;
        return new PdfReadNumber(token.Text!, isInteger: true);
    }

    // Returns the stream length, or -1 when it is missing, not an integer, or cannot be resolved.
    private long StreamLength(PdfReadDictionary dictionary)
    {
        var value = dictionary["Length"];
        if (value is PdfReadReference reference && _resolve is not null)
        {
            try
            {
                value = _resolve(reference.ObjectNumber);
            }
            catch (PdfReadException)
            {
                return -1;
            }
        }
        return value is PdfReadNumber { IsInteger: true } number ? number.IntegerValue : -1;
    }

    private static int ToInt(PdfToken token) =>
        int.TryParse(token.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : throw PdfReadException.Damaged($"Invalid object number '{token.Text}' at offset {token.Start}.");

    #endregion

    #endregion
}

/// <summary>
/// An indirect object read from a file.
/// </summary>
/// <param name="Number">The object number.</param>
/// <param name="Generation">The generation number.</param>
/// <param name="Value">The object's value; a <see cref="PdfReadStream"/> for a stream.</param>
internal sealed record PdfIndirectObject(int Number, int Generation, PdfReadValue Value);
