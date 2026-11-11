using UnlimitedPDF.Models.Internal.Fonts;

namespace UnlimitedPDF.Models.Internal.Html.Parsing;

/// <summary>
/// Turns HTML bytes into text (WR-038): a byte-order mark wins, then a <c>&lt;meta charset&gt;</c> or
/// <c>http-equiv</c> declaration in the first 1024 bytes, else UTF-8. UTF-8 and windows-1252 (with its common labels)
/// are supported; any other declared character set is read as UTF-8 with a warning.
/// </summary>
internal static class HtmlDecoder
{
    #region Fields and Constants

    // How far the declaration prescan looks, as browsers do.
    private const int PrescanLength = 1024;

    private static readonly HashSet<string> Utf8Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        "utf-8", "utf8", "unicode-1-1-utf-8", "unicode11utf8", "unicode20utf8", "x-unicode20utf8",
        // A declared UTF-16 in the markup itself cannot be right (the prescan read ASCII), so browsers use UTF-8.
        "utf-16", "utf-16le", "utf-16be", "unicode", "ucs-2"
    };

    private static readonly HashSet<string> Windows1252Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-1252", "cp1252", "x-cp1252", "iso-8859-1", "iso8859-1", "iso_8859-1", "iso-ir-100", "latin1", "l1",
        "us-ascii", "ascii", "ansi_x3.4-1968", "cp819", "ibm819", "csisolatin1", "iso_8859-1:1987", "iso88591"
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Decodes HTML bytes.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <param name="length">The number of bytes to decode.</param>
    /// <param name="warnings">Receives an <see cref="PdfHtmlWarningKind.UnsupportedValue"/> warning for an unknown character set.</param>
    /// <returns>The text.</returns>
    public static string Decode(byte[] data, int length, HtmlWarnings warnings)
    {
        if (length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return new UTF8Encoding(false, false).GetString(data, 3, length - 3);
        if (length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode.GetString(data, 2, length - 2);
        if (length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(data, 2, length - 2);

        string? label = Prescan(data, Math.Min(length, PrescanLength));
        if (label is not null && Windows1252Labels.Contains(label)) return WinAnsiEncoding.Decode(data, 0, length);
        if (label is not null && !Utf8Labels.Contains(label))
            warnings.Add(PdfHtmlWarningKind.UnsupportedValue, "charset=" + label, $"The character set \"{label}\" is not supported; the HTML is read as UTF-8.");
        return new UTF8Encoding(false, false).GetString(data, 0, length);
    }

    /// <summary>
    /// Returns the character set a <c>meta</c> element declares in the given bytes, or null.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <param name="length">How many bytes to look at.</param>
    /// <returns>The label, trimmed, or null.</returns>
    public static string? Prescan(byte[] data, int length)
    {
        string head = Encoding.ASCII.GetString(data, 0, length);
        int position = 0;
        while ((position = head.IndexOf("<meta", position, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            int end = head.IndexOf('>', position);
            if (end < 0) break;
            string tag = head.Substring(position, end - position);
            position = end;

            string? charset = AttributeValue(tag, "charset");
            if (!string.IsNullOrWhiteSpace(charset)) return charset!.Trim();

            string? httpEquiv = AttributeValue(tag, "http-equiv");
            string? content = AttributeValue(tag, "content");
            if (httpEquiv is not null && content is not null && httpEquiv.Trim().Equals("content-type", StringComparison.OrdinalIgnoreCase))
            {
                int at = content.IndexOf("charset", StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                string rest = content.Substring(at + 7).TrimStart();
                if (!rest.StartsWith("=", StringComparison.Ordinal)) continue;
                rest = rest.Substring(1).Trim().Trim('"', '\'');
                int stop = rest.IndexOfAny(new[] { ';', ' ', '"', '\'' });
                return (stop >= 0 ? rest.Substring(0, stop) : rest).Trim();
            }
        }
        return null;
    }

    #endregion

    #region Private Methods

    // A simple attribute reader for the prescan: name=value with optional quotes, case-insensitive name.
    private static string? AttributeValue(string tag, string name)
    {
        int i = 5;
        while (i < tag.Length)
        {
            while (i < tag.Length && (char.IsWhiteSpace(tag[i]) || tag[i] == '/')) i++;
            int start = i;
            while (i < tag.Length && !char.IsWhiteSpace(tag[i]) && tag[i] != '=' && tag[i] != '/') i++;
            string attribute = tag.Substring(start, i - start);
            while (i < tag.Length && char.IsWhiteSpace(tag[i])) i++;
            string value = string.Empty;
            if (i < tag.Length && tag[i] == '=')
            {
                i++;
                while (i < tag.Length && char.IsWhiteSpace(tag[i])) i++;
                if (i < tag.Length && (tag[i] == '"' || tag[i] == '\''))
                {
                    char quote = tag[i++];
                    int close = tag.IndexOf(quote, i);
                    if (close < 0) close = tag.Length;
                    value = tag.Substring(i, close - i);
                    i = close + 1;
                }
                else
                {
                    int valueStart = i;
                    while (i < tag.Length && !char.IsWhiteSpace(tag[i])) i++;
                    value = tag.Substring(valueStart, i - valueStart);
                }
            }
            if (attribute.Length == 0) { i++; continue; }
            if (attribute.Equals(name, StringComparison.OrdinalIgnoreCase)) return value;
        }
        return null;
    }

    #endregion

    #endregion
}
