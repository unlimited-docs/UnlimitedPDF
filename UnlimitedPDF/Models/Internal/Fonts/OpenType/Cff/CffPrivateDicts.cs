namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// Reads the Private DICTs, and the Local Subr INDEXes they name, for one load of a CFF or CFF2 table (A06). Up to 256
/// Font DICTs may name one Private DICT or one Local Subr INDEX: each is read once and shared. Distinct ones lie in
/// separate parts of a well-formed table, so together they cannot hold more bytes or entries than the table; reading
/// past that means they overlap, and the font is corrupt. A load's work is then linear in the table's size.
/// </summary>
internal sealed class CffPrivateDicts
{
    #region Fields and Constants

    private const int OpSubrs = 19;

    private readonly FontBytes _data;
    private readonly int _table;
    private readonly int _limit;
    private readonly bool _wideCount;
    private readonly Dictionary<(int Offset, int Size), (CffDict Dict, CffIndex? Subrs)> _dicts = new();
    private readonly Dictionary<int, CffIndex> _subrs = new();
    private long _bytesLeft;
    private long _entriesLeft;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Starts reading the Private DICTs of the table at <paramref name="table"/>.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The table's offset; Private DICT offsets are relative to it.</param>
    /// <param name="limit">The end of the table.</param>
    /// <param name="wideCount">True for CFF2, whose INDEX count is 32 bits.</param>
    public CffPrivateDicts(FontBytes data, int table, int limit, bool wideCount)
    {
        _data = data;
        _table = table;
        _limit = limit;
        _wideCount = wideCount;
        _bytesLeft = _entriesLeft = limit - table;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the Private DICT of <paramref name="size"/> bytes at <paramref name="offset"/> and its local
    /// subroutines, if any, reading each only the first time it is named. The Subrs offset is relative to the Private
    /// DICT's start.
    /// </summary>
    /// <param name="size">The Private DICT's size, as its Font DICT or Top DICT states it.</param>
    /// <param name="offset">The Private DICT's offset from the table's start.</param>
    /// <returns>The Private DICT and its Local Subr INDEX, or null when it has none.</returns>
    public (CffDict Dict, CffIndex? Subrs) Read(int size, int offset)
    {
        if (size < 0 || offset < 0 || (long)_table + offset + size > _limit) throw FontBytes.Corrupt("has a CFF Private DICT outside the table");
        if (_dicts.TryGetValue((offset, size), out var known)) return known;
        if ((_bytesLeft -= size) < 0) throw FontBytes.Corrupt("has CFF Private DICTs that overlap");

        int start = _table + offset;
        var dict = CffDict.Read(_data, start, size);
        CffIndex? subrs = null;
        if (dict.Has(OpSubrs))
        {
            long at = (long)start + dict.Int(OpSubrs);
            if (at < _table || at >= _limit) throw FontBytes.Corrupt("has a CFF Local Subr INDEX outside the table");
            if (!_subrs.TryGetValue((int)at, out subrs))
            {
                subrs = CffIndex.Read(_data, (int)at, _limit, "Local Subr INDEX", _wideCount);
                if ((_entriesLeft -= subrs.Count + 1) < 0) throw FontBytes.Corrupt("has CFF Local Subr INDEXes that overlap");
                _subrs[(int)at] = subrs;
            }
        }
        return _dicts[(offset, size)] = (dict, subrs);
    }

    #endregion

    #endregion
}
