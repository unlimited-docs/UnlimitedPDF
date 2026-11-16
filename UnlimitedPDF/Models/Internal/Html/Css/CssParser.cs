namespace UnlimitedPDF.Models.Internal.Html.Css;

/// <summary>
/// One CSS declaration with its property in lowercase.
/// </summary>
/// <param name="Property">The property.</param>
/// <param name="Value">The value text, trimmed, without <c>!important</c>.</param>
/// <param name="Important">Whether the declaration is <c>!important</c>.</param>
internal sealed record CssDeclaration(string Property, string Value, bool Important);

/// <summary>
/// One style rule: its selectors and declarations, its position in the cascade, and its line.
/// </summary>
/// <param name="Selectors">The selectors.</param>
/// <param name="Declarations">The declarations, shorthands expanded.</param>
/// <param name="Order">The rule's position among all rules of its origin.</param>
/// <param name="Line">The 1-based line of the HTML input the stylesheet starts on.</param>
internal sealed record CssRule(IReadOnlyList<CssSelector> Selectors, IReadOnlyList<CssDeclaration> Declarations, int Order, int Line);

/// <summary>
/// Parses stylesheets and <c>style</c> attributes (WR-038, AC-06): comments, strings, and nested blocks are handled;
/// <c>@media</c> blocks apply when their media list includes <c>print</c> or <c>all</c> (or no media type); other
/// at-rules are skipped, <c>@font-face</c> and <c>@import</c> with a warning. A rule whose selector the library does not
/// support is dropped as CSS drops it, and an invalid declaration is skipped. Shorthands are expanded into longhands.
/// </summary>
internal static class CssParser
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether a media list (a <c>media</c> attribute or <c>@media</c> prelude) applies to print.
    /// </summary>
    /// <param name="media">The media list, or null for none.</param>
    /// <returns>True when it applies.</returns>
    public static bool AppliesToPrint(string? media)
    {
        if (string.IsNullOrWhiteSpace(media)) return true;
        foreach (var query in media!.Split(','))
        {
            var words = query.Trim().ToLowerInvariant().Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;
            bool negated = words[0] == "not";
            string type = words.SkipWhile(w => w is "not" or "only").FirstOrDefault() ?? "all";
            if (type.StartsWith("(", StringComparison.Ordinal)) type = "all";
            bool matches = type is "print" or "all";
            if (negated ? !matches : matches) return true;
        }
        return false;
    }

    /// <summary>
    /// Parses the declarations of a <c>style</c> attribute or a rule block.
    /// </summary>
    /// <param name="text">The declarations.</param>
    /// <returns>The declarations, shorthands expanded, in order.</returns>
    public static List<CssDeclaration> ParseDeclarations(string text)
    {
        var declarations = new List<CssDeclaration>();
        foreach (var part in SplitTopLevel(StripComments(text), ';'))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0) continue;
            string property = part.Substring(0, colon).Trim().ToLowerInvariant();
            string value = part.Substring(colon + 1).Trim();
            bool important = false;
            int bang = value.LastIndexOf('!');
            if (bang >= 0 && value.Substring(bang + 1).Trim().Equals("important", StringComparison.OrdinalIgnoreCase))
            {
                important = true;
                value = value.Substring(0, bang).Trim();
            }
            if (property.Length == 0 || value.Length == 0) continue;
            CssShorthands.Expand(property, value, important, declarations);
        }
        return declarations;
    }

    /// <summary>
    /// Parses a stylesheet.
    /// </summary>
    /// <param name="css">The CSS text.</param>
    /// <param name="line">The HTML line the stylesheet starts on, for warnings.</param>
    /// <param name="order">The next rule order number; advanced past the rules added.</param>
    /// <param name="rules">Receives the rules.</param>
    /// <param name="warnings">Receives at-rule warnings.</param>
    /// <param name="maxDepth">The deepest nesting of <c>@media</c> blocks, the HTML nesting limit (A06).</param>
    /// <exception cref="PdfHtmlException">Blocks are nested deeper than <paramref name="maxDepth"/>.</exception>
    public static void ParseStyleSheet(string css, int line, ref int order, List<CssRule> rules, HtmlWarnings warnings, int maxDepth = 256)
    {
        string text = StripComments(css);
        int i = 0;
        ParseRules(text, ref i, line, ref order, rules, warnings, untilBrace: false, depth: 0, maxDepth);
    }

    #endregion

    #region Private Methods

    // Returns the index of the brace that closes the block opened just before start, respecting strings.
    private static int FindClose(string text, int start)
    {
        int depth = 1;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '"' or '\'') { i = SkipString(text, i); continue; }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return text.Length;
    }

    private static void ParseRules(string text, ref int i, int line, ref int order, List<CssRule> rules, HtmlWarnings warnings, bool untilBrace, int depth, int maxDepth)
    {
        // Each nested block is one level of recursion, so the depth is capped like element nesting (A06).
        if (depth > maxDepth)
            throw new PdfHtmlException(PdfHtmlErrorReason.NestingTooDeep,
                string.Format(CultureInfo.InvariantCulture, "Stylesheet blocks are nested deeper than the limit of {0} (line {1}).", maxDepth, line));
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) return;
            if (text[i] == '}' && untilBrace) { i++; return; }
            if (string.CompareOrdinal(text, i, "<!--", 0, 4) == 0) { i += 4; continue; }
            if (string.CompareOrdinal(text, i, "-->", 0, 3) == 0) { i += 3; continue; }

            // The prelude runs to the block or, for statement at-rules, to a semicolon.
            int start = i;
            while (i < text.Length && text[i] != '{' && text[i] != ';' && !(untilBrace && text[i] == '}'))
            {
                if (text[i] is '"' or '\'') i = SkipString(text, i);
                i++;
            }
            string prelude = text.Substring(start, Math.Min(i, text.Length) - start).Trim();
            if (i >= text.Length || text[i] == ';' || text[i] == '}')
            {
                if (i < text.Length && text[i] == ';') i++;
                if (prelude.StartsWith("@import", StringComparison.OrdinalIgnoreCase))
                    warnings.Add(PdfHtmlWarningKind.UnsupportedProperty, "@import", "@import is not supported; the imported stylesheet was not loaded.", line);
                continue;
            }

            i++; // past '{'
            if (prelude.StartsWith("@", StringComparison.Ordinal))
            {
                string name = new string(prelude.Skip(1).TakeWhile(c => char.IsLetter(c) || c == '-').ToArray()).ToLowerInvariant();
                if (name == "media")
                {
                    var inner = new List<CssRule>();
                    ParseRules(text, ref i, line, ref order, inner, warnings, untilBrace: true, depth + 1, maxDepth);
                    if (AppliesToPrint(prelude.Substring(6))) rules.AddRange(inner);
                    continue;
                }
                // The rule is skipped whole: its src URLs are never resolved or read (D-011, A01).
                if (name == "font-face")
                    warnings.Add(PdfHtmlWarningKind.FontFaceIgnored, "@font-face", "@font-face is ignored and its font files are not read; register fonts with PdfHtmlLoadOptions.Fonts.", line);
                i = Math.Min(text.Length, FindClose(text, i) + 1);
                continue;
            }

            int close = FindClose(text, i);
            string block = text.Substring(i, close - i);
            i = Math.Min(text.Length, close + 1);
            if (!CssSelector.TryParseList(prelude, out var selectors)) continue;
            var declarations = ParseDeclarations(block);
            if (declarations.Count > 0) rules.Add(new CssRule(selectors, declarations, order++, line));
        }
    }

    private static int SkipString(string text, int start)
    {
        char quote = text[start];
        for (int i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == quote || text[i] == '\n') return i;
        }
        return text.Length - 1;
    }

    // Splits on a separator outside strings and parentheses.
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '"' or '\'') { i = SkipString(text, i); continue; }
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (c == separator && depth == 0)
            {
                parts.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        parts.Add(text.Substring(start));
        return parts;
    }

    private static string StripComments(string text)
    {
        if (text.IndexOf("/*", StringComparison.Ordinal) < 0) return text;
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '"' or '\'')
            {
                int end = SkipString(text, i);
                sb.Append(text, i, end - i + 1);
                i = end;
                continue;
            }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 1;
                sb.Append(' ');
                continue;
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    #endregion

    #endregion
}
