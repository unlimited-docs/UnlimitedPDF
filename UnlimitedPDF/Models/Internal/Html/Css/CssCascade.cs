using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Models.Internal.Html.Css;

/// <summary>
/// Computes every element's style (WR-038, AC-06): the built-in stylesheet, then HTML presentational attributes
/// (<c>width</c>, <c>height</c>, <c>border</c>, <c>cellpadding</c>, <c>cellspacing</c>, <c>bgcolor</c>, <c>align</c>,
/// <c>valign</c>, list <c>type</c>, <c>nowrap</c>), then author stylesheets and <c>style</c> attributes, ordered by
/// importance, specificity, and source order; inherited properties flow to children. Unsupported properties and
/// values are reported once per rule or attribute.
/// </summary>
internal static class CssCascade
{
    #region Fields and Constants

    private static readonly Lazy<List<CssRule>> UserAgentRules = new(() =>
    {
        var rules = new List<CssRule>();
        int order = 0;
        CssParser.ParseStyleSheet(UserAgentStyles.Css, 0, ref order, rules, new HtmlWarnings(false));
        return rules;
    });

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Computes the style of every element in the tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="authorRules">The author rules in document order.</param>
    /// <param name="warnings">Receives unsupported property and value warnings.</param>
    /// <returns>Each element's style.</returns>
    public static Dictionary<HtmlElement, ComputedStyle> Apply(HtmlDocumentTree tree, IReadOnlyList<CssRule> authorRules, HtmlWarnings warnings)
    {
        foreach (var rule in authorRules) Report(rule.Declarations, rule.Line, warnings);

        var styles = new Dictionary<HtmlElement, ComputedStyle>();
        var memo = new CssSelector.MatchMemo();
        var pending = new Stack<HtmlElement>();
        pending.Push(tree.Root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            var parent = element.Parent is { } p && styles.TryGetValue(p, out var parentStyle) ? parentStyle : null;
            styles[element] = Compute(element, parent, authorRules, warnings, memo);
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                if (element.Children[i] is HtmlElement child) pending.Push(child);
            }
        }
        return styles;
    }

    #endregion

    #region Private Methods

    private static ComputedStyle Compute(HtmlElement element, ComputedStyle? parent, IReadOnlyList<CssRule> authorRules, HtmlWarnings warnings, CssSelector.MatchMemo memo)
    {
        var entries = new List<(int Layer, int Specificity, int Order, CssDeclaration Declaration)>();

        void AddRules(IReadOnlyList<CssRule> rules, int normalLayer, int importantLayer)
        {
            foreach (var rule in rules)
            {
                int specificity = -1;
                foreach (var selector in rule.Selectors)
                {
                    if (selector.Specificity > specificity && selector.Matches(element, memo)) specificity = selector.Specificity;
                }
                if (specificity < 0) continue;
                foreach (var declaration in rule.Declarations)
                    entries.Add((declaration.Important ? importantLayer : normalLayer, specificity, rule.Order, declaration));
            }
        }

        AddRules(UserAgentRules.Value, 0, 0);
        int hintOrder = 0;
        foreach (var hint in PresentationalHints(element)) entries.Add((1, 0, hintOrder++, hint));
        AddRules(authorRules, 2, 4);
        if (element.Attribute("style") is { } inline)
        {
            var declarations = CssParser.ParseDeclarations(inline);
            Report(declarations, element.Line, warnings);
            int order = 0;
            foreach (var declaration in declarations) entries.Add((declaration.Important ? 5 : 3, 0, order++, declaration));
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries.OrderBy(e => e.Layer).ThenBy(e => e.Specificity).ThenBy(e => e.Order))
        {
            var declaration = entry.Declaration;
            if (!ComputedStyle.IsSupported(declaration.Property) || !ComputedStyle.IsValid(declaration.Property, declaration.Value, out _)) continue;
            values[declaration.Property] = declaration.Value;
        }
        return ComputedStyle.Compute(values, parent);
    }

    private static string Pixels(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length > 0 && trimmed.All(c => char.IsDigit(c) || c == '.') ? trimmed + "px" : trimmed;
    }

    // HTML attributes that browsers turn into styles at the lowest author priority.
    private static IEnumerable<CssDeclaration> PresentationalHints(HtmlElement element)
    {
        var result = new List<CssDeclaration>();
        void Add(string property, string value) => CssShorthands.Expand(property, value, false, result);
        string name = element.Name;

        if (element.Attribute("bgcolor") is { } bgcolor && name is "body" or "table" or "tr" or "td" or "th") Add("background-color", bgcolor);
        if (element.Attribute("width") is { } width && name is "table" or "td" or "th" or "col" or "img" or "hr") Add("width", Pixels(width));
        if (element.Attribute("height") is { } height && name is "table" or "td" or "th" or "tr" or "img") Add("height", Pixels(height));
        if (element.Attribute("align") is { } align)
        {
            string lower = align.Trim().ToLowerInvariant();
            if (name is "td" or "th" or "tr" or "div" or "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "caption" or "thead" or "tbody" or "tfoot")
                Add("text-align", lower);
            else if (name == "table" && lower == "center") { Add("margin-left", "auto"); Add("margin-right", "auto"); }
        }
        if (element.Attribute("valign") is { } valign && name is "td" or "th" or "tr" or "thead" or "tbody" or "tfoot") Add("vertical-align", valign);
        if (element.Attribute("nowrap") is not null && name is "td" or "th") Add("white-space", "nowrap");

        if (name == "table")
        {
            if (element.Attribute("cellspacing") is { } spacing) Add("border-spacing", Pixels(spacing));
            if (element.Attribute("border") is { } border)
            {
                string px = border.Trim().Length == 0 ? "1px" : Pixels(border);
                Add("border", px + " solid gray");
            }
        }
        if (name is "td" or "th" && Ancestor(element, "table") is { } table)
        {
            if (table.Attribute("cellpadding") is { } padding) Add("padding", Pixels(padding));
            if (table.Attribute("border") is { } border && border.Trim() != "0") Add("border", "1px solid gray");
        }
        if (element.Attribute("type") is { } type && name is "ol" or "ul" or "li")
        {
            string? listType = type.Trim() switch
            {
                "1" => "decimal",
                "a" => "lower-alpha",
                "A" => "upper-alpha",
                "i" => "lower-roman",
                "I" => "upper-roman",
                var other => other.ToLowerInvariant() is "disc" or "circle" or "square" ? other.ToLowerInvariant() : null
            };
            if (listType is not null) Add("list-style-type", listType);
        }
        return result;
    }

    private static HtmlElement? Ancestor(HtmlElement element, string name)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (current.Name == name) return current;
        }
        return null;
    }

    // Vendor-prefixed and custom properties are skipped silently; everything else unsupported is reported.
    private static void Report(IReadOnlyList<CssDeclaration> declarations, int line, HtmlWarnings warnings)
    {
        foreach (var declaration in declarations)
        {
            string property = declaration.Property;
            if (property.StartsWith("-", StringComparison.Ordinal) || property.StartsWith("mso-", StringComparison.Ordinal)) continue;
            if (!ComputedStyle.IsSupported(property))
            {
                warnings.Add(PdfHtmlWarningKind.UnsupportedProperty, property, $"The CSS property \"{property}\" is not supported.", line);
                continue;
            }
            if (!ComputedStyle.IsValid(property, declaration.Value, out bool reported))
                warnings.Add(PdfHtmlWarningKind.UnsupportedValue, property, $"The value \"{declaration.Value}\" of \"{property}\" is not supported.", line);
            else if (reported)
                warnings.Add(PdfHtmlWarningKind.UnsupportedValue, property, $"\"{property}: {declaration.Value}\" is not supported; the element is laid out as a block.", line);
        }
    }

    #endregion

    #endregion
}
