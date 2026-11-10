using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Models.Internal.Html.Css;

/// <summary>
/// One complex selector (WR-038, AC-06): compound selectors of type, <c>*</c>, <c>.class</c>, <c>#id</c>,
/// <c>[attr]</c>, <c>[attr=value]</c>, <c>:first-child</c>, <c>:last-child</c>, and <c>:link</c>, joined by
/// descendant or child (<c>&gt;</c>) combinators. Dynamic pseudo-classes such as <c>:hover</c> never match in print.
/// </summary>
internal sealed class CssSelector
{
    #region Fields and Constants

    private static readonly HashSet<string> DynamicPseudoClasses = new(StringComparer.Ordinal)
    {
        "hover", "focus", "active", "visited", "focus-within", "focus-visible", "target", "checked", "disabled", "enabled"
    };

    // Compounds from the leftmost to the rightmost; Combinators[i] joins Compounds[i] and Compounds[i + 1].
    private readonly List<Compound> _compounds;
    private readonly List<char> _combinators;

    #endregion

    #region Constructors and Finalizers

    private CssSelector(List<Compound> compounds, List<char> combinators, bool neverMatches)
    {
        _compounds = compounds;
        _combinators = combinators;
        NeverMatches = neverMatches;
        int ids = compounds.Sum(c => c.Ids.Count);
        int classes = compounds.Sum(c => c.Classes.Count + c.Attributes.Count + c.PseudoCount);
        int types = compounds.Count(c => c.Tag is not null);
        Specificity = ids * 10000 + classes * 100 + types;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses a selector list. A list with any selector the library does not support is rejected as a whole, as CSS
    /// rejects it.
    /// </summary>
    /// <param name="text">The selector list.</param>
    /// <param name="selectors">The selectors.</param>
    /// <returns>True when every selector in the list is supported.</returns>
    public static bool TryParseList(string text, out List<CssSelector> selectors)
    {
        selectors = new List<CssSelector>();
        foreach (var part in text.Split(','))
        {
            if (!TryParse(part.Trim(), out var selector)) return false;
            selectors.Add(selector!);
        }
        return selectors.Count > 0;
    }

    /// <summary>
    /// Returns whether the selector matches an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>True when it matches.</returns>
    public bool Matches(HtmlElement element)
    {
        if (NeverMatches || !_compounds[_compounds.Count - 1].Matches(element)) return false;
        if (_compounds.Count == 1) return true;

        // Each (element, compound) pair is tried at most once (A06): without memory, descendant combinators would
        // retry every combination of ancestors, which grows combinatorially with nesting depth.
        var failed = new HashSet<(HtmlElement, int)>();
        return MatchFrom(element, _compounds.Count - 1, failed);
    }

    #endregion

    #region Private Methods

    // Whether the compounds from 0 to index match, the compound at index matching this element. Recursion depth is the
    // compound count, not the nesting depth: ancestors are walked in a loop.
    private bool MatchFrom(HtmlElement element, int index, HashSet<(HtmlElement, int)> failed)
    {
        if (index == 0) return true;
        if (failed.Contains((element, index))) return false;

        bool matched = false;
        if (_combinators[index - 1] == '>')
        {
            matched = element.Parent is { } parent && _compounds[index - 1].Matches(parent) && MatchFrom(parent, index - 1, failed);
        }
        else
        {
            for (var ancestor = element.Parent; ancestor is not null && !matched; ancestor = ancestor.Parent)
            {
                if (_compounds[index - 1].Matches(ancestor) && MatchFrom(ancestor, index - 1, failed)) matched = true;
            }
        }
        if (!matched) failed.Add((element, index));
        return matched;
    }

    private static bool TryParse(string text, out CssSelector? selector)
    {
        selector = null;
        if (text.Length == 0) return false;

        var compounds = new List<Compound>();
        var combinators = new List<char>();
        bool neverMatches = false;
        int i = 0;
        char pending = '\0';
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]) || text[i] == '>')
            {
                bool child = false;
                while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == '>'))
                {
                    if (text[i] == '>') { if (child) return false; child = true; }
                    i++;
                }
                if (compounds.Count == 0) return false;
                pending = child ? '>' : ' ';
                continue;
            }
            if (text[i] is '+' or '~') return false;

            var compound = new Compound();
            if (!compound.Parse(text, ref i, ref neverMatches)) return false;
            if (compounds.Count > 0)
            {
                if (pending == '\0') return false;
                combinators.Add(pending);
            }
            compounds.Add(compound);
            pending = '\0';
        }
        if (compounds.Count == 0 || pending == '>') return false;
        selector = new CssSelector(compounds, combinators, neverMatches);
        return true;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets whether the selector uses a dynamic pseudo-class and so never matches in print.
    /// </summary>
    public bool NeverMatches { get; }

    /// <summary>
    /// Gets the specificity as ids * 10000 + classes, attributes, and pseudo-classes * 100 + types.
    /// </summary>
    public int Specificity { get; }

    #endregion

    #region Nested Types

    private sealed class Compound
    {
        #region Methods

        #region Public Methods

        public bool Matches(HtmlElement element)
        {
            if (Tag is not null && Tag != element.Name) return false;
            foreach (var id in Ids)
            {
                if (element.Attribute("id") != id) return false;
            }
            if (Classes.Count > 0)
            {
                var classes = (element.Attribute("class") ?? string.Empty).Split(new[] { ' ', '\t', '\n', '\f' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in Classes)
                {
                    if (Array.IndexOf(classes, name) < 0) return false;
                }
            }
            foreach (var (name, value) in Attributes)
            {
                string? actual = element.Attribute(name);
                if (actual is null || (value is not null && actual != value)) return false;
            }
            if (Link && !(element.Name == "a" && element.Attribute("href") is not null)) return false;
            if (FirstChild || LastChild)
            {
                if (element.Parent is not { } parent) return false;
                var siblings = parent.ElementChildren;
                if (FirstChild && siblings[0] != element) return false;
                if (LastChild && siblings[siblings.Count - 1] != element) return false;
            }
            return true;
        }

        public bool Parse(string text, ref int i, ref bool neverMatches)
        {
            int start = i;
            if (text[i] == '*')
            {
                i++;
            }
            else if (IsNameStart(text[i]))
            {
                Tag = ReadName(text, ref i).ToLowerInvariant();
            }

            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('>' or '+' or '~'))
            {
                char c = text[i];
                if (c == '#')
                {
                    i++;
                    string id = ReadName(text, ref i);
                    if (id.Length == 0) return false;
                    Ids.Add(id);
                }
                else if (c == '.')
                {
                    i++;
                    string name = ReadName(text, ref i);
                    if (name.Length == 0) return false;
                    Classes.Add(name);
                }
                else if (c == '[')
                {
                    int close = text.IndexOf(']', i);
                    if (close < 0) return false;
                    string inner = text.Substring(i + 1, close - i - 1).Trim();
                    i = close + 1;
                    int eq = inner.IndexOf('=');
                    if (eq < 0)
                    {
                        if (inner.Length == 0 || !inner.All(ch => IsNameChar(ch))) return false;
                        Attributes.Add((inner.ToLowerInvariant(), null));
                        continue;
                    }
                    if (eq > 0 && "~|^$*".IndexOf(inner[eq - 1]) >= 0) return false;
                    string name = inner.Substring(0, eq).Trim().ToLowerInvariant();
                    string value = inner.Substring(eq + 1).Trim();
                    if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0]) value = value.Substring(1, value.Length - 2);
                    Attributes.Add((name, value));
                }
                else if (c == ':')
                {
                    i++;
                    if (i < text.Length && text[i] == ':') return false; // pseudo-elements are not supported
                    string pseudo = ReadName(text, ref i).ToLowerInvariant();
                    if (i < text.Length && text[i] == '(') return false; // functional pseudo-classes are not supported
                    PseudoCount++;
                    switch (pseudo)
                    {
                        case "first-child": FirstChild = true; break;
                        case "last-child": LastChild = true; break;
                        case "link": case "any-link": Link = true; break;
                        default:
                            if (!DynamicPseudoClasses.Contains(pseudo)) return false;
                            neverMatches = true;
                            break;
                    }
                }
                else
                {
                    return false;
                }
            }
            return i > start;
        }

        #endregion

        #region Private Methods

        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' || c > 127;

        private static bool IsNameStart(char c) => char.IsLetter(c) || c is '_' or '-' || c > 127;

        private static string ReadName(string text, ref int i)
        {
            var sb = new StringBuilder();
            while (i < text.Length && (IsNameChar(text[i]) || text[i] == '\\'))
            {
                if (text[i] == '\\' && i + 1 < text.Length) i++;
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        #endregion

        #endregion

        #region Properties, Indexers

        public List<(string Name, string? Value)> Attributes { get; } = new();

        public List<string> Classes { get; } = new();

        public bool FirstChild { get; private set; }

        public List<string> Ids { get; } = new();

        public bool LastChild { get; private set; }

        public bool Link { get; private set; }

        public int PseudoCount { get; private set; }

        public string? Tag { get; private set; }

        #endregion
    }

    #endregion
}
