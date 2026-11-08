namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Decodes PDF text strings, such as a document title: UTF-16BE or UTF-8 with a byte-order mark, otherwise
/// PDFDocEncoding.
/// </summary>
internal static class PdfTextString
{
    #region Fields and Constants

    // PDFDocEncoding differs from Latin-1 at 0x18-0x1F, 0x80-0x9F, and 0xA0 (the euro sign); 0x9F is undefined and
    // kept as is.
    private static readonly char[] High =
    {
        '•', '†', '‡', '…', '—', '–', 'ƒ', '⁄',
        '‹', '›', '−', '‰', '„', '“', '”', '‘',
        '’', '‚', '™', 'ﬁ', 'ﬂ', 'Ł', 'Œ', 'Š',
        'Ÿ', 'Ž', 'ı', 'ł', 'œ', 'š', 'ž', '\u009F',
    };

    private static readonly char[] Low = { '˘', 'ˇ', 'ˆ', '˙', '˝', '˛', '˚', '˜' };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the text <paramref name="bytes"/> encode.
    /// </summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The decoded text.</returns>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, (bytes.Length - 2) & ~1);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        var text = new StringBuilder(bytes.Length);
        foreach (byte value in bytes)
        {
            text.Append(value switch
            {
                >= 0x18 and <= 0x1F => Low[value - 0x18],
                >= 0x80 and <= 0x9F => High[value - 0x80],
                0xA0 => '€',
                _ => (char)value,
            });
        }
        return text.ToString();
    }

    #endregion

    #endregion
}
