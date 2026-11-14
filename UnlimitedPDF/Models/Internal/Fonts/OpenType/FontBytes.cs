namespace UnlimitedPDF.Models.Internal.Fonts.OpenType;

/// <summary>
/// Bounds-checked big-endian reads over font data (A06). Every read outside the data fails with
/// <see cref="PdfFontException"/> (<see cref="PdfFontErrorReason.CorruptData"/>) instead of an index exception.
/// </summary>
internal sealed class FontBytes
{
    #region Fields and Constants

    private readonly byte[] _data;

    #endregion

    #region Constructors and Finalizers

    public FontBytes(byte[] data)
    {
        _data = data;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a font exception for damaged data.
    /// </summary>
    /// <param name="detail">What is wrong, completing "The font ...".</param>
    /// <returns>The exception.</returns>
    public static PdfFontException Corrupt(string detail) => new(PdfFontErrorReason.CorruptData, $"The font {detail}.");

    public short I16(int offset) => unchecked((short)U16(offset));

    public int I32(int offset) => unchecked((int)U32(offset));

    /// <summary>
    /// Fails unless <paramref name="length"/> bytes starting at <paramref name="offset"/> are inside the data.
    /// </summary>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The number of bytes.</param>
    /// <param name="what">What is being read, for the message.</param>
    public void Require(long offset, long length, string what)
    {
        if (offset < 0 || length < 0 || offset + length > _data.Length) throw Corrupt($"is truncated or damaged: {what} lies outside the data");
    }

    public byte[] Slice(int offset, int length)
    {
        Require(offset, length, "a table");
        var copy = new byte[length];
        Buffer.BlockCopy(_data, offset, copy, 0, length);
        return copy;
    }

    public string Tag(int offset)
    {
        Require(offset, 4, "a tag");
        return new string(new[] { (char)_data[offset], (char)_data[offset + 1], (char)_data[offset + 2], (char)_data[offset + 3] });
    }

    public byte U8(int offset)
    {
        Require(offset, 1, "a value");
        return _data[offset];
    }

    public ushort U16(int offset)
    {
        Require(offset, 2, "a value");
        return (ushort)((_data[offset] << 8) | _data[offset + 1]);
    }

    public uint U32(int offset)
    {
        Require(offset, 4, "a value");
        return ((uint)_data[offset] << 24) | ((uint)_data[offset + 1] << 16) | ((uint)_data[offset + 2] << 8) | _data[offset + 3];
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public int Length => _data.Length;

    #endregion
}
