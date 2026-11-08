namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// The objects of one existing file, loaded on first use and cached. A reference to a free or unknown object reads as
/// null, as the PDF format specifies.
/// </summary>
internal sealed class PdfObjectTable
{
    #region Fields and Constants

    // The longest chain of references to references followed before the file is judged damaged.
    private const int MaxReferenceHops = 32;

    private readonly Dictionary<int, PdfReadValue> _cache = new();
    private readonly byte[] _data;
    private readonly IReadOnlyDictionary<int, CrossReferenceEntry> _entries;
    private readonly PdfReadLimits _limits;
    private readonly HashSet<int> _loading = new();
    private readonly Dictionary<int, ObjectStreamReader> _objectStreams = new();

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="entries">Where each object is stored, newest entry per object.</param>
    /// <param name="trailer">The newest trailer.</param>
    /// <param name="limits">The load's limits; the defaults when null.</param>
    public PdfObjectTable(byte[] data, IReadOnlyDictionary<int, CrossReferenceEntry> entries, PdfReadDictionary trailer, PdfReadLimits? limits = null)
    {
        _data = data;
        _entries = entries;
        _limits = limits ?? PdfReadLimits.Default;
        Trailer = trailer;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the value of object <paramref name="number"/>, or null when it is free or not listed.
    /// </summary>
    /// <param name="number">The object number.</param>
    /// <returns>The object's value.</returns>
    /// <exception cref="PdfReadException">The object cannot be read.</exception>
    public PdfReadValue Get(int number)
    {
        if (_cache.TryGetValue(number, out var cached)) return cached;
        if (!_entries.TryGetValue(number, out var entry) || entry.Kind == CrossReferenceEntryKind.Free) return PdfReadNull.Instance;
        if (!_loading.Add(number)) throw PdfReadException.Damaged($"Object {number} depends on itself.");

        try
        {
            var value = entry.Kind == CrossReferenceEntryKind.InUse ? LoadAt(number, entry.Offset) : LoadCompressed(number, entry);
            _cache[number] = value;
            return value;
        }
        finally
        {
            _loading.Remove(number);
        }
    }

    /// <summary>
    /// Follows references until a direct value is reached.
    /// </summary>
    /// <param name="value">A value that may be a reference.</param>
    /// <returns>The direct value; null for a reference to a free or unknown object.</returns>
    /// <exception cref="PdfReadException">The chain is too long or an object cannot be read.</exception>
    public PdfReadValue Resolve(PdfReadValue? value)
    {
        int hops = 0;
        while (value is PdfReadReference reference)
        {
            if (++hops > MaxReferenceHops) throw PdfReadException.Damaged("References form a chain that is too long.");
            value = Get(reference.ObjectNumber);
        }
        return value ?? PdfReadNull.Instance;
    }

    #endregion

    #region Private Methods

    private PdfReadValue LoadAt(int number, long offset)
    {
        if (offset < 0 || offset >= _data.Length)
            throw PdfReadException.Damaged($"Object {number} has an offset outside the file.");

        var parser = new PdfParser(new PdfLexer(_data, (int)offset), n => Resolve(Get(n)));
        var read = parser.ParseIndirectObject();
        if (read.Number != number) throw PdfReadException.Damaged($"Object {number} is not at its cross-reference offset.");
        return read.Value;
    }

    // A compressed object lives in an object stream, which must itself be stored at an offset.
    private PdfReadValue LoadCompressed(int number, CrossReferenceEntry entry)
    {
        if (!_objectStreams.TryGetValue(entry.StreamNumber, out var reader))
        {
            if (!_entries.TryGetValue(entry.StreamNumber, out var streamEntry) || streamEntry.Kind != CrossReferenceEntryKind.InUse)
                throw PdfReadException.Damaged($"Object {number} is in object stream {entry.StreamNumber}, which is not stored in the file.");
            if (Get(entry.StreamNumber) is not PdfReadStream stream)
                throw PdfReadException.Damaged($"Object {entry.StreamNumber} is not an object stream.");
            reader = ObjectStreamReader.Open(stream, Resolve, _limits);
            _objectStreams[entry.StreamNumber] = reader;
        }
        return reader.Read(number, entry.StreamIndex);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the object numbers that are in use.
    /// </summary>
    public IEnumerable<int> ObjectNumbers => _entries.Where(e => e.Value.Kind != CrossReferenceEntryKind.Free).Select(e => e.Key);

    /// <summary>
    /// Gets the newest trailer.
    /// </summary>
    public PdfReadDictionary Trailer { get; }

    #endregion
}
