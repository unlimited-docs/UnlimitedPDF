namespace UnlimitedPDF.Models.Internal.Html.Css;

/// <summary>
/// Expands CSS shorthands into longhands (WR-038): <c>margin</c>, <c>padding</c>, <c>border</c> and its side and
/// part forms, <c>font</c>, <c>background</c> (its color part; images stay as <c>background-image</c> so they are
/// reported), <c>list-style</c>, and the legacy <c>page-break-*</c> properties as <c>break-*</c>.
/// </summary>
internal static class CssShorthands
{
    #region Fields and Constants

    private static readonly string[] Sides = { "top", "right", "bottom", "left" };

    private static readonly HashSet<string> BorderStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "hidden", "solid", "dashed", "dotted", "double", "groove", "ridge", "inset", "outset"
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds the longhand declarations of one declaration.
    /// </summary>
    /// <param name="property">The lowercase property.</param>
    /// <param name="value">The value.</param>
    /// <param name="important">Whether it is important.</param>
    /// <param name="output">Receives the declarations.</param>
    public static void Expand(string property, string value, bool important, List<CssDeclaration> output)
    {
        void Add(string name, string v) => output.Add(new CssDeclaration(name, v, important));
        bool keyword = value.ToLowerInvariant() is "inherit" or "initial" or "unset";

        switch (property)
        {
            case "margin":
            case "padding":
                foreach (var (side, v) in Boxed(value, keyword)) Add($"{property}-{side}", v);
                return;
            case "border-width":
            case "border-style":
            case "border-color":
                string part = property.Substring(7);
                foreach (var (side, v) in Boxed(value, keyword)) Add($"border-{side}-{part}", v);
                return;
            case "border":
                foreach (var side in Sides) ExpandBorderSide(side, value, keyword, Add);
                return;
            case "border-top":
            case "border-right":
            case "border-bottom":
            case "border-left":
                ExpandBorderSide(property.Substring(7), value, keyword, Add);
                return;
            case "background":
                ExpandBackground(value, keyword, Add);
                return;
            case "font":
                ExpandFont(value, keyword, Add);
                return;
            case "list-style":
                ExpandListStyle(value, keyword, Add);
                return;
            case "page-break-before":
            case "page-break-after":
                Add(property.Substring(5), value.Trim().ToLowerInvariant() switch
                {
                    "always" or "left" or "right" => "page",
                    var other => other
                });
                return;
            case "page-break-inside":
                Add("break-inside", value);
                return;
            case "text-decoration-line":
                Add("text-decoration", value);
                return;
            default:
                Add(property, value);
                return;
        }
    }

    #endregion

    #region Private Methods

    // One to four values for top, right, bottom, left.
    private static IEnumerable<(string Side, string Value)> Boxed(string value, bool keyword)
    {
        var parts = keyword ? new List<string> { value } : CssValues.SplitSpaces(value);
        if (parts.Count == 0 || parts.Count > 4) yield break;
        string top = parts[0];
        string right = parts.Count > 1 ? parts[1] : top;
        string bottom = parts.Count > 2 ? parts[2] : top;
        string left = parts.Count > 3 ? parts[3] : right;
        yield return ("top", top);
        yield return ("right", right);
        yield return ("bottom", bottom);
        yield return ("left", left);
    }

    private static void ExpandBackground(string value, bool keyword, Action<string, string> add)
    {
        if (keyword)
        {
            add("background-color", value);
            return;
        }
        string color = "transparent";
        foreach (var part in CssValues.SplitSpaces(value))
        {
            string lower = part.ToLowerInvariant();
            if (lower.StartsWith("url(", StringComparison.Ordinal) || lower.Contains("gradient("))
                add("background-image", part);
            else if (CssValues.TryParseColor(part, out _, out _))
                color = part;
        }
        add("background-color", color);
    }

    private static void ExpandBorderSide(string side, string value, bool keyword, Action<string, string> add)
    {
        if (keyword)
        {
            add($"border-{side}-width", value);
            add($"border-{side}-style", value);
            add($"border-{side}-color", value);
            return;
        }
        string width = "medium", style = "none", color = "currentcolor";
        foreach (var part in CssValues.SplitSpaces(value))
        {
            if (BorderStyles.Contains(part)) style = part;
            else if (part.ToLowerInvariant() is "thin" or "medium" or "thick" || CssValues.TryParseLength(part, out _, allowPercent: false)) width = part;
            else color = part;
        }
        add($"border-{side}-width", width);
        add($"border-{side}-style", style);
        add($"border-{side}-color", color);
    }

    // font: [style] [variant] [weight] size[/line-height] family, family...
    private static void ExpandFont(string value, bool keyword, Action<string, string> add)
    {
        if (keyword)
        {
            foreach (var name in new[] { "font-style", "font-weight", "font-size", "line-height", "font-family" }) add(name, value);
            return;
        }
        var parts = CssValues.SplitSpaces(value);
        string style = "normal", weight = "normal", lineHeight = "normal";
        int i = 0;
        for (; i < parts.Count; i++)
        {
            string lower = parts[i].ToLowerInvariant();
            if (lower is "italic" or "oblique") style = lower;
            else if (lower is "bold" or "bolder" or "lighter" || (lower.Length == 3 && lower.All(char.IsDigit))) weight = lower;
            else if (lower is "normal" or "small-caps") continue;
            else break;
        }
        if (i >= parts.Count) return;
        string size = parts[i++];
        int slash = size.IndexOf('/');
        if (slash >= 0)
        {
            lineHeight = size.Substring(slash + 1);
            size = size.Substring(0, slash);
            if (lineHeight.Length == 0 && i < parts.Count) lineHeight = parts[i++];
        }
        else if (i < parts.Count && parts[i].StartsWith("/", StringComparison.Ordinal))
        {
            lineHeight = parts[i].Length > 1 ? parts[i].Substring(1) : (i + 1 < parts.Count ? parts[++i] : "normal");
            i++;
        }
        if (i >= parts.Count) return; // a family is required
        add("font-style", style);
        add("font-weight", weight);
        add("font-size", size);
        add("line-height", lineHeight);
        add("font-family", string.Join(" ", parts.Skip(i)));
    }

    private static void ExpandListStyle(string value, bool keyword, Action<string, string> add)
    {
        if (keyword)
        {
            add("list-style-type", value);
            add("list-style-position", value);
            return;
        }
        foreach (var part in CssValues.SplitSpaces(value))
        {
            string lower = part.ToLowerInvariant();
            if (lower is "inside" or "outside") add("list-style-position", lower);
            else if (lower.StartsWith("url(", StringComparison.Ordinal)) add("list-style-image", part);
            else add("list-style-type", lower);
        }
    }

    #endregion

    #endregion
}
