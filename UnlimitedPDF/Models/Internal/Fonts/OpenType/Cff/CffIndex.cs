namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// A CFF INDEX: a count, an offset size, count + 1 offsets (1-based, relative to the byte before the data), and the
/// data. Every offset is checked against the data when the INDEX is read (A06).
/// </summary>
internal sealed class CffIndex
{
    #region Fields and Constants

    private readonly int[] _starts;

    #endregion

    #region Constructors and Finalizers

    private CffIndex(FontBytes data, int[] starts, int end)
    {
        Data = data;
        _starts = starts;
        End = end;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads an INDEX starting at <paramref name="offset"/>, which must lie inside the CFF table.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <param name="offset">The INDEX's first byte.</param>
    /// <param name="limit">The end of the CFF table; nothing may extend past it.</param>
    /// <param name="what">The INDEX's name, for messages.</param>
    /// <param name="wideCount">True for CFF2, whose INDEX count is 32 bits.</param>
    /// <returns>The INDEX.</returns>
    public static CffIndex Read(FontBytes data, int offset, int limit, string what, bool wideCount = false)
    {
        if (offset < 0 || offset >= limit) throw FontBytes.Corrupt($"has a CFF {what} outside the CFF table");
        long count = wideCount ? data.U32(offset) : data.U16(offset);
        int header = wideCount ? 4 : 2;
        if (count == 0) return new CffIndex(data, new[] { offset + header }, offset + header);
        if (count > 65535) throw FontBytes.Corrupt($"has a CFF {what} with too many entries");

        int offSize = data.U8(offset + header);
        if (offSize is < 1 or > 4) throw FontBytes.Corrupt($"has a CFF {what} with an invalid offset size");
        int offsets = offset + header + 1;
        long dataStart = offsets + (count + 1) * offSize - 1; // offsets are 1-based
        if (dataStart > limit) throw FontBytes.Corrupt($"has a truncated CFF {what}");

        var starts = new int[count + 1];
        long previous = 1;
        for (int i = 0; i <= count; i++)
        {
            long value = 0;
            for (int b = 0; b < offSize; b++) value = (value << 8) | data.U8(offsets + i * offSize + b);
            if (value < previous || (i == 0 && value != 1) || dataStart + value > limit)
                throw FontBytes.Corrupt($"has invalid offsets in the CFF {what}");
            starts[i] = (int)(dataStart + value);
            previous = value;
        }
        return new CffIndex(data, starts, starts[count]);
    }

    /// <summary>
    /// Returns where entry <paramref name="index"/> lies in the font data.
    /// </summary>
    /// <param name="index">The 0-based entry.</param>
    /// <returns>The offset and length.</returns>
    public (int Offset, int Length) Entry(int index) => (_starts[index], _starts[index + 1] - _starts[index]);

    /// <summary>
    /// Returns a copy of entry <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The 0-based entry.</param>
    /// <returns>The entry's bytes.</returns>
    public byte[] Bytes(int index)
    {
        var (offset, length) = Entry(index);
        return Data.Slice(offset, length);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public int Count => _starts.Length - 1;

    public FontBytes Data { get; }

    /// <summary>
    /// Gets the offset of the first byte after the INDEX.
    /// </summary>
    public int End { get; }

    #endregion
}
