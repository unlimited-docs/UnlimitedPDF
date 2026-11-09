namespace UnlimitedPDF.Models.Internal.Fonts;

/// <summary>
/// The WinAnsi character set of the standard fonts (WR-038): ASCII, the Latin-1 range, and 27 extra characters such as
/// the euro sign and typographic quotes in codes 128 to 159. Characters outside it are written as '?'. The same table
/// decodes windows-1252 HTML input.
/// </summary>
internal static class WinAnsiEncoding
{
    #region Fields and Constants

    // The code written for a character WinAnsi does not have.
    public const byte Replacement = (byte)'?';

    // Unicode characters of codes 128 to 159; zero where WinAnsi leaves the code undefined.
    private static readonly char[] HighCodes =
    {
        '€', '\0', '‚', 'ƒ', '„', '…', '†', '‡', // 128-135
        'ˆ', '‰', 'Š', '‹', 'Œ', '\0', 'Ž', '\0', // 136-143
        '\0', '‘', '’', '“', '”', '•', '–', '—', // 144-151
        '˜', '™', 'š', '›', 'œ', '\0', 'ž', 'Ÿ' // 152-159
    };

    private static readonly Dictionary<char, byte> HighCharacters = BuildHighCharacters();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether a character has a WinAnsi code.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True when the character can be written.</returns>
    public static bool CanEncode(char c) => TryEncode(c, out _);

    /// <summary>
    /// Decodes windows-1252 bytes the way browsers do: undefined codes become the matching C1 control characters.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <param name="offset">The first byte.</param>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The text.</returns>
    public static string Decode(byte[] data, int offset, int count)
    {
        var chars = new char[count];
        for (int i = 0; i < count; i++)
        {
            byte b = data[offset + i];
            char c = b >= 128 && b <= 159 ? HighCodes[b - 128] : (char)b;
            chars[i] = c == '\0' && b != 0 ? (char)b : c;
        }
        return new string(chars);
    }

    /// <summary>
    /// Encodes text as WinAnsi codes, one per character; a character outside WinAnsi, a control character, or a
    /// surrogate pair becomes '?'.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The codes.</returns>
    public static byte[] Encode(string text)
    {
        var bytes = new List<byte>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            bytes.Add(TryEncode(c, out byte code) ? code : Replacement);
        }
        return bytes.ToArray();
    }

    /// <summary>
    /// Returns the WinAnsi code of a character.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <param name="code">The code, or '?' when there is none.</param>
    /// <returns>True when the character has a code.</returns>
    public static bool TryEncode(char c, out byte code)
    {
        if ((c >= 0x20 && c <= 0x7E) || (c >= 0xA0 && c <= 0xFF))
        {
            code = (byte)c;
            return true;
        }
        if (HighCharacters.TryGetValue(c, out code)) return true;
        code = Replacement;
        return false;
    }

    #endregion

    #region Private Methods

    private static Dictionary<char, byte> BuildHighCharacters()
    {
        var map = new Dictionary<char, byte>();
        for (int i = 0; i < HighCodes.Length; i++)
        {
            if (HighCodes[i] != '\0') map[HighCodes[i]] = (byte)(128 + i);
        }
        return map;
    }

    #endregion

    #endregion
}
