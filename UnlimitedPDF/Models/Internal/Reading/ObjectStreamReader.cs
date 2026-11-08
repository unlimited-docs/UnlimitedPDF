namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Reads the objects stored in an object stream (<c>/Type /ObjStm</c>): <c>/N</c> pairs of object number and offset,
/// then the objects from <c>/First</c> on.
/// </summary>
internal sealed class ObjectStreamReader
{
    #region Fields and Constants

    private readonly byte[] _data;
    private readonly int _first;
    private readonly List<(int Number, int Offset)> _index;

    #endregion

    #region Constructors and Finalizers

    private ObjectStreamReader(byte[] data, int first, List<(int Number, int Offset)> index)
    {
        _data = data;
        _first = first;
        _index = index;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the number of the object at <paramref name="streamIndex"/> in this stream's index.
    /// </summary>
    /// <param name="streamIndex">The position in the index.</param>
    /// <returns>The object number.</returns>
    public int NumberAt(int streamIndex) => _index[streamIndex].Number;

    /// <summary>
    /// Opens an object stream.
    /// </summary>
    /// <param name="stream">The stream object.</param>
    /// <param name="resolve">Resolves references in the stream dictionary.</param>
    /// <param name="limits">The load's limits.</param>
    /// <returns>The reader.</returns>
    /// <exception cref="PdfReadException">The stream is not a valid object stream.</exception>
    public static ObjectStreamReader Open(PdfReadStream stream, Func<PdfReadValue?, PdfReadValue> resolve, PdfReadLimits limits)
    {
        var dictionary = stream.Dictionary;
        if (dictionary["Type"] is not PdfReadName { Value: "ObjStm" })
            throw PdfReadException.Damaged("A compressed object's stream is not an object stream.");
        if (resolve(dictionary["N"]) is not PdfReadNumber { IsInteger: true } count || count.IntegerValue < 0
            || resolve(dictionary["First"]) is not PdfReadNumber { IsInteger: true } first || first.IntegerValue < 0)
            throw PdfReadException.Damaged("An object stream has no valid /N or /First.");

        byte[] data = PdfStreamDecoder.Decode(stream, resolve, limits.MaxStreamSize);
        if (first.IntegerValue > data.Length) throw PdfReadException.Damaged("An object stream's /First is past its data.");

        var lexer = new PdfLexer(data);
        var index = new List<(int Number, int Offset)>();
        for (long i = 0; i < count.IntegerValue; i++)
        {
            var number = lexer.Next();
            var offset = lexer.Next();
            if (number.Kind != PdfTokenKind.Integer || offset.Kind != PdfTokenKind.Integer || lexer.Position > first.IntegerValue)
                throw PdfReadException.Damaged("An object stream's index is invalid.");
            index.Add((int.Parse(number.Text!, CultureInfo.InvariantCulture), int.Parse(offset.Text!, CultureInfo.InvariantCulture)));
        }

        return new ObjectStreamReader(data, (int)first.IntegerValue, index);
    }

    /// <summary>
    /// Returns object <paramref name="number"/>, stored at <paramref name="streamIndex"/> in this stream.
    /// </summary>
    /// <param name="number">The object number the cross-reference expects.</param>
    /// <param name="streamIndex">The object's position in the stream's index.</param>
    /// <returns>The object's value.</returns>
    /// <exception cref="PdfReadException">The object is not there or cannot be parsed.</exception>
    public PdfReadValue Read(int number, int streamIndex)
    {
        if (streamIndex < 0 || streamIndex >= _index.Count || _index[streamIndex].Number != number)
            throw PdfReadException.Damaged($"Object {number} is not at its place in its object stream.");

        int offset = _first + _index[streamIndex].Offset;
        if (offset < _first || offset >= _data.Length) throw PdfReadException.Damaged($"Object {number} is outside its object stream.");
        return new PdfParser(new PdfLexer(_data, offset)).ParseValue();
    }

    #endregion

    #endregion
}
