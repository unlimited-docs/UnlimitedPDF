namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Escapes text for a PDF literal string <c>( ... )</c> so caller text cannot end the string early or add operators
/// (A05, PR-006). A backslash is always escaped. Parentheses are escaped only when unbalanced, because balanced pairs
/// are valid in a literal string as they are; text that was valid before is therefore written unchanged.
/// </summary>
internal static class PdfLiteralString
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns <paramref name="text"/> escaped for use between the parentheses of a literal string.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped text, for example "Smiley :\)" for "Smiley :)".</returns>
    public static string Escape(string text)
    {
        HashSet<int>? unbalanced = null;
        var open = new Stack<int>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                open.Push(i);
            }
            else if (text[i] == ')')
            {
                if (open.Count > 0) open.Pop();
                else (unbalanced ??= new HashSet<int>()).Add(i);
            }
        }

        foreach (var index in open)
            (unbalanced ??= new HashSet<int>()).Add(index);

        if (unbalanced is null && text.IndexOf('\\') < 0) return text;

        var sb = new StringBuilder(text.Length + 8);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' || (unbalanced is not null && unbalanced.Contains(i)))
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    #endregion

    #endregion
}
