namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// A value read from an existing PDF. Strings and names keep their bytes exactly, so copied content is never
/// re-encoded.
/// </summary>
internal abstract class PdfReadValue
{
}

/// <summary>
/// The PDF null object. A reference to a missing or free object also reads as null.
/// </summary>
internal sealed class PdfReadNull : PdfReadValue
{
    #region Constructors and Finalizers

    private PdfReadNull()
    {
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the only null value.
    /// </summary>
    public static PdfReadNull Instance { get; } = new();

    #endregion
}

/// <summary>
/// A PDF boolean.
/// </summary>
internal sealed class PdfReadBoolean : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public PdfReadBoolean(bool value)
    {
        Value = value;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the value.
    /// </summary>
    public bool Value { get; }

    #endregion
}

/// <summary>
/// A PDF integer or real number, keeping the text it was written as.
/// </summary>
internal sealed class PdfReadNumber : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the number from its token text.
    /// </summary>
    /// <param name="text">The token, for example "12" or "-.5".</param>
    /// <param name="isInteger">Whether the token has no decimal point.</param>
    public PdfReadNumber(string text, bool isInteger)
    {
        Text = text;
        IsInteger = isInteger && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the value as an integer, truncating a real number.
    /// </summary>
    public long IntegerValue => IsInteger
        ? long.Parse(Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
        : (long)Math.Truncate(Value);

    /// <summary>
    /// Gets whether the number is an integer that fits a <see cref="long"/>.
    /// </summary>
    public bool IsInteger { get; }

    /// <summary>
    /// Gets the token text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the value as a double. A token that is only a sign reads as 0.
    /// </summary>
    public double Value =>
        double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    #endregion
}

/// <summary>
/// A PDF string (literal or hexadecimal), as its decoded bytes.
/// </summary>
internal sealed class PdfReadString : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the string.
    /// </summary>
    /// <param name="bytes">The bytes after escapes or hexadecimal digits are decoded.</param>
    public PdfReadString(byte[] bytes)
    {
        Bytes = bytes;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the decoded bytes.
    /// </summary>
    public byte[] Bytes { get; }

    #endregion
}

/// <summary>
/// A PDF name without its slash. <c>#xx</c> escapes are decoded and each byte is held as one Latin-1 character, so
/// names compare and copy exactly.
/// </summary>
internal sealed class PdfReadName : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the name.
    /// </summary>
    /// <param name="value">The name without its slash, for example "Type".</param>
    public PdfReadName(string value)
    {
        Value = value;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the name without its slash.
    /// </summary>
    public string Value { get; }

    #endregion
}

/// <summary>
/// A reference to an indirect object, <c>n g R</c>.
/// </summary>
internal sealed class PdfReadReference : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the reference.
    /// </summary>
    /// <param name="objectNumber">The object number.</param>
    /// <param name="generation">The generation number.</param>
    public PdfReadReference(int objectNumber, int generation)
    {
        ObjectNumber = objectNumber;
        Generation = generation;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the generation number.
    /// </summary>
    public int Generation { get; }

    /// <summary>
    /// Gets the object number.
    /// </summary>
    public int ObjectNumber { get; }

    #endregion
}

/// <summary>
/// A PDF array.
/// </summary>
internal sealed class PdfReadArray : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the array.
    /// </summary>
    /// <param name="items">The items in order.</param>
    public PdfReadArray(IReadOnlyList<PdfReadValue> items)
    {
        Items = items;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of items.
    /// </summary>
    public int Count => Items.Count;

    /// <summary>
    /// Gets the items in order.
    /// </summary>
    public IReadOnlyList<PdfReadValue> Items { get; }

    /// <summary>
    /// Gets the item at <paramref name="index"/>.
    /// </summary>
    public PdfReadValue this[int index] => Items[index];

    #endregion
}

/// <summary>
/// A PDF dictionary. Keys are names without the slash and keep the order they were written in; a repeated key keeps
/// its first position and its last value.
/// </summary>
internal sealed class PdfReadDictionary : PdfReadValue
{
    #region Fields and Constants

    private readonly List<string> _keys = new();
    private readonly Dictionary<string, PdfReadValue> _values = new(StringComparer.Ordinal);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether the dictionary has <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key without its slash.</param>
    /// <returns>True when the key is present.</returns>
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    #endregion

    #region Internal Methods

    /// <summary>
    /// Adds or replaces an entry while parsing.
    /// </summary>
    /// <param name="key">The key without its slash.</param>
    /// <param name="value">The value.</param>
    internal void Set(string key, PdfReadValue value)
    {
        if (!_values.ContainsKey(key)) _keys.Add(key);
        _values[key] = value;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Count => _keys.Count;

    /// <summary>
    /// Gets the keys in written order.
    /// </summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>
    /// Gets the value of <paramref name="key"/>, or null when it is absent.
    /// </summary>
    public PdfReadValue? this[string key] => _values.TryGetValue(key, out var value) ? value : null;

    #endregion
}

/// <summary>
/// A PDF stream: its dictionary and its data exactly as stored, still encoded by its filters.
/// </summary>
internal sealed class PdfReadStream : PdfReadValue
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the stream.
    /// </summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="data">The stored data.</param>
    public PdfReadStream(PdfReadDictionary dictionary, ReadOnlyMemory<byte> data)
    {
        Dictionary = dictionary;
        Data = data;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the stored data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Gets the stream dictionary.
    /// </summary>
    public PdfReadDictionary Dictionary { get; }

    #endregion
}
