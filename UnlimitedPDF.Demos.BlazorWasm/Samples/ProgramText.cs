namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Changes to a demo's complete console program before its code tab shows it (QF-26112501).
/// </summary>
public static class ProgramText
{
    #region Fields and Constants

    /// <summary>
    /// The line that opens the HTML in the HTML to PDF program: a raw string literal.
    /// </summary>
    public const string HtmlOpening = "        string html = \"\"\"";

    // The raw string's lines and its closing quotes are indented this far.
    private const string HtmlIndent = "            ";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Puts HTML into the HTML to PDF program in place of its sample HTML, so the program converts the HTML being
    /// edited. The raw string gets one more quote than the longest run of quotes in the HTML, so any HTML stays one
    /// string.
    /// </summary>
    /// <param name="program">The program text, with <see cref="HtmlOpening"/> and its closing quotes each on their own
    /// line.</param>
    /// <param name="html">The HTML to put in.</param>
    /// <returns>The program with the HTML; the program unchanged when it holds no such string.</returns>
    public static string WithHtml(string program, string html)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(html);

        var lines = program.Split('\n').ToList();
        int open = lines.FindIndex(line => line.TrimEnd('\r') == HtmlOpening);
        int close = open < 0 ? -1 : lines.FindIndex(open + 1, line => line.TrimEnd('\r') == HtmlIndent + "\"\"\";");
        if (close < 0)
        {
            return program;
        }

        string quotes = new('"', Math.Max(3, LongestQuoteRun(html) + 1));
        var htmlLines = html.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n').Split('\n')
            .Select(line => line.Length == 0 ? string.Empty : HtmlIndent + line.TrimEnd());

        var shaped = lines.Take(open)
            .Append(HtmlOpening[..^3] + quotes)
            .Concat(htmlLines)
            .Append(HtmlIndent + quotes + ";")
            .Concat(lines.Skip(close + 1));
        return string.Join('\n', shaped);
    }

    #endregion

    #region Private Methods

    private static int LongestQuoteRun(string text)
    {
        int longest = 0;
        int run = 0;
        foreach (char c in text)
        {
            run = c == '"' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    #endregion

    #endregion
}
