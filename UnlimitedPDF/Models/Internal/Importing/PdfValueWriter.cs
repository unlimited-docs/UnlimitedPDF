namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Writes values read from an existing PDF as PDF syntax with renumbered references. The output is ASCII: strings are
/// written in hexadecimal and names escape every byte outside the regular characters, so copied bytes survive
/// exactly and copied text can never end a string or add an operator (A05).
/// </summary>
internal static class PdfValueWriter
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the syntax of <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value; a stream writes as its dictionary.</param>
    /// <param name="map">Returns the new object number for a source object number, or null when the reference is
    /// dropped (it is then written as <c>null</c>).</param>
    /// <returns>The ASCII bytes.</returns>
    public static byte[] Write(PdfReadValue value, Func<int, int?> map)
    {
        var sb = new StringBuilder();
        Append(sb, value, map);
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Appends the syntax of <paramref name="value"/> to <paramref name="sb"/>.
    /// </summary>
    internal static void Append(StringBuilder sb, PdfReadValue value, Func<int, int?> map)
    {
        switch (value)
        {
            case PdfReadNull:
                sb.Append("null");
                break;
            case PdfReadBoolean boolean:
                sb.Append(boolean.Value ? "true" : "false");
                break;
            case PdfReadNumber number:
                sb.Append(number.Text);
                break;
            case PdfReadString text:
                AppendHex(sb, text.Bytes);
                break;
            case PdfReadName name:
                AppendName(sb, name.Value);
                break;
            case PdfReadReference reference:
                if (map(reference.ObjectNumber) is int mapped) sb.Append(mapped).Append(" 0 R");
                else sb.Append("null");
                break;
            case PdfReadArray array:
                sb.Append('[');
                for (int i = 0; i < array.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    Append(sb, array[i], map);
                }
                sb.Append(']');
                break;
            case PdfReadDictionary dictionary:
                AppendDictionary(sb, dictionary.Keys.Select(key => (key, dictionary[key]!)), map);
                break;
            case PdfReadStream stream:
                AppendDictionary(sb, stream.Dictionary.Keys.Select(key => (key, stream.Dictionary[key]!)), map);
                break;
        }
    }

    /// <summary>
    /// Appends a dictionary made of <paramref name="entries"/>.
    /// </summary>
    internal static void AppendDictionary(StringBuilder sb, IEnumerable<(string Key, PdfReadValue Value)> entries, Func<int, int?> map)
    {
        sb.Append("<<");
        foreach (var (key, value) in entries)
        {
            sb.Append(' ');
            AppendName(sb, key);
            sb.Append(' ');
            Append(sb, value, map);
        }
        sb.Append(" >>");
    }

    /// <summary>
    /// Appends <paramref name="bytes"/> as a hexadecimal string.
    /// </summary>
    internal static void AppendHex(StringBuilder sb, byte[] bytes)
    {
        sb.Append('<');
        foreach (byte value in bytes) sb.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        sb.Append('>');
    }

    /// <summary>
    /// Appends a name, escaping every byte that is not a regular printable character as <c>#xx</c>.
    /// </summary>
    internal static void AppendName(StringBuilder sb, string name)
    {
        sb.Append('/');
        foreach (char c in name)
        {
            bool regular = c > 0x20 && c < 0x7F && c != '#' && !PdfLexer.IsDelimiter((byte)c);
            if (regular) sb.Append(c);
            else sb.Append('#').Append(((int)c & 0xFF).ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    #endregion

    #endregion
}
