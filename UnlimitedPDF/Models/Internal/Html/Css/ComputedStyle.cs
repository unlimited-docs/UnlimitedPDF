namespace UnlimitedPDF.Models.Internal.Html.Css;

internal enum CssDisplay
{
    Inline,
    Block,
    InlineBlock,
    ListItem,
    None,
    Table,
    TableRowGroup,
    TableHeaderGroup,
    TableFooterGroup,
    TableRow,
    TableCell,
    TableCaption,
    TableColumn,
    TableColumnGroup
}

internal enum CssTextAlign
{
    Left,
    Right,
    Center,
    Justify
}

internal enum CssWhiteSpace
{
    Normal,
    NoWrap,
    Pre,
    PreWrap,
    PreLine
}

internal enum CssVerticalAlign
{
    Baseline,
    Top,
    Middle,
    Bottom,
    Sub,
    Super,
    TextTop,
    TextBottom
}

internal enum CssTextTransform
{
    None,
    Uppercase,
    Lowercase,
    Capitalize
}

internal enum CssBreak
{
    Auto,
    Page,
    Avoid
}

internal enum CssListStyleType
{
    Disc,
    Circle,
    Square,
    Decimal,
    DecimalLeadingZero,
    LowerAlpha,
    UpperAlpha,
    LowerRoman,
    UpperRoman,
    None
}

/// <summary>
/// One border side: width in points, style, and color.
/// </summary>
internal readonly record struct CssBorderSide(double Width, HtmlBorderStyle Style, PdfColor Color)
{
    /// <summary>
    /// Gets the width that takes space: zero when the style is none.
    /// </summary>
    public double UsedWidth => Style == HtmlBorderStyle.None ? 0 : Width;
}

/// <summary>
/// An element's computed style (WR-038): typed values for every supported property, with inheritance applied. Lengths
/// that depend on the containing block (percentages, <c>auto</c>) stay as <see cref="CssLength"/> or null for layout.
/// </summary>
internal sealed class ComputedStyle
{
    #region Fields and Constants

    // The root font size: 16 px.
    public const double DefaultFontSize = 12;

    // Line height for "normal", as a multiple of the font size.
    public const double NormalLineHeight = 1.2;

    private static readonly Dictionary<string, double> FontSizeKeywords = new(StringComparer.Ordinal)
    {
        ["xx-small"] = 9 * 0.75, ["x-small"] = 10 * 0.75, ["small"] = 13 * 0.75, ["medium"] = 16 * 0.75,
        ["large"] = 18 * 0.75, ["x-large"] = 24 * 0.75, ["xx-large"] = 32 * 0.75, ["xxx-large"] = 48 * 0.75
    };

    // Properties whose computed value children inherit.
    private static readonly HashSet<string> InheritedProperties = new(StringComparer.Ordinal)
    {
        "color", "font-family", "font-size", "font-weight", "font-style", "text-align", "text-indent", "text-transform",
        "line-height", "letter-spacing", "white-space", "list-style-type", "list-style-position", "border-collapse",
        "border-spacing"
    };

    private static readonly HashSet<string> SupportedProperties = new(StringComparer.Ordinal)
    {
        "color", "background-color", "font-family", "font-size", "font-weight", "font-style", "text-align",
        "text-decoration", "text-indent", "text-transform", "line-height", "letter-spacing", "white-space",
        "vertical-align", "display", "width", "height", "min-width", "max-width",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
        "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        "list-style-type", "list-style-position", "border-collapse", "border-spacing",
        "break-before", "break-after", "break-inside"
    };

    // Which border colors follow the element's color (the initial currentcolor).
    private readonly bool[] _borderColorIsCurrent = { true, true, true, true };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Computes an element's style from its cascaded declarations and its parent's style.
    /// </summary>
    /// <param name="values">The winning value of each property set on the element.</param>
    /// <param name="parent">The parent's style, or null for the root.</param>
    /// <returns>The style.</returns>
    public static ComputedStyle Compute(IReadOnlyDictionary<string, string> values, ComputedStyle? parent)
    {
        var s = parent is null ? new ComputedStyle() : parent.InheritedCopy();
        double parentFontSize = parent?.FontSize ?? DefaultFontSize;
        double root = DefaultFontSize;

        string? Get(string name)
        {
            if (!values.TryGetValue(name, out var value)) return null;
            string lower = value.Trim().ToLowerInvariant();
            if (lower == "inherit") return parent is null ? null : "\u0001inherit";
            if (lower is "initial" || (lower == "unset" && !InheritedProperties.Contains(name))) return "\u0001initial";
            if (lower == "unset") return parent is null ? null : "\u0001inherit";
            return value.Trim();
        }

        // Font size first: em values elsewhere depend on it.
        if (Get("font-size") is { } fontSize)
        {
            if (fontSize == "\u0001inherit") s.FontSize = parentFontSize;
            else if (fontSize == "\u0001initial") s.FontSize = DefaultFontSize;
            else if (TryFontSize(fontSize, parentFontSize, root, out double size)) s.FontSize = size;
        }
        double em = s.FontSize;

        foreach (var pair in values)
        {
            string name = pair.Key;
            string? raw = Get(name);
            if (raw is null || name == "font-size") continue;
            if (raw == "\u0001inherit")
            {
                if (parent is not null) s.InheritFrom(parent, name);
                continue;
            }
            if (raw == "\u0001initial")
            {
                s.Reset(name);
                continue;
            }
            s.Apply(name, raw, em, root, parent);
        }

        // Borders take the element's color when none is given (currentcolor), and have no width without a style.
        for (int i = 0; i < 4; i++)
        {
            if (s._borderColorIsCurrent[i]) s.Borders[i] = s.Borders[i] with { Color = s.Color };
        }
        if (parent is not null) { s.Underline |= parent.Underline && s.Display != CssDisplay.InlineBlock; s.LineThrough |= parent.LineThrough && s.Display != CssDisplay.InlineBlock; }
        return s;
    }

    /// <summary>
    /// Returns whether the library supports a property.
    /// </summary>
    /// <param name="property">The lowercase property.</param>
    /// <returns>True when supported.</returns>
    public static bool IsSupported(string property) => SupportedProperties.Contains(property);

    /// <summary>
    /// Returns whether a value of a supported property can be used; <c>display</c> values the library lays out
    /// differently (such as <c>flex</c>) are usable but reported.
    /// </summary>
    /// <param name="property">The lowercase property.</param>
    /// <param name="value">The value.</param>
    /// <param name="reported">Set when the value is used differently from what it says, for a warning.</param>
    /// <returns>True when the value can be used.</returns>
    public static bool IsValid(string property, string value, out bool reported)
    {
        reported = false;
        string lower = value.Trim().ToLowerInvariant();
        if (lower is "inherit" or "initial" or "unset") return true;
        var probe = new ComputedStyle();
        if (property == "display")
        {
            reported = lower is "flex" or "inline-flex" or "grid" or "inline-grid" or "contents" or "flow-root" or "run-in";
            return reported || TryDisplay(lower, out _);
        }
        if (property == "font-size") return TryFontSize(lower, DefaultFontSize, DefaultFontSize, out _);
        return probe.Apply(property, value.Trim(), DefaultFontSize, DefaultFontSize, null);
    }

    #endregion

    #region Private Methods

    private static bool TryDisplay(string value, out CssDisplay display)
    {
        CssDisplay? parsed = value switch
        {
            "inline" => CssDisplay.Inline,
            "block" => CssDisplay.Block,
            "inline-block" => CssDisplay.InlineBlock,
            "list-item" => CssDisplay.ListItem,
            "none" => CssDisplay.None,
            "table" or "inline-table" => CssDisplay.Table,
            "table-row-group" => CssDisplay.TableRowGroup,
            "table-header-group" => CssDisplay.TableHeaderGroup,
            "table-footer-group" => CssDisplay.TableFooterGroup,
            "table-row" => CssDisplay.TableRow,
            "table-cell" => CssDisplay.TableCell,
            "table-caption" => CssDisplay.TableCaption,
            "table-column" => CssDisplay.TableColumn,
            "table-column-group" => CssDisplay.TableColumnGroup,
            // Unsupported layouts fall back (D-013): flex and grid containers are laid out as blocks.
            "flex" or "grid" or "flow-root" or "contents" or "run-in" => CssDisplay.Block,
            "inline-flex" or "inline-grid" => CssDisplay.InlineBlock,
            _ => null
        };
        display = parsed ?? CssDisplay.Inline;
        return parsed is not null;
    }

    private static bool TryFontSize(string value, double parentSize, double root, out double size)
    {
        string lower = value.ToLowerInvariant();
        if (FontSizeKeywords.TryGetValue(lower, out size)) return true;
        if (lower == "smaller") { size = parentSize / 1.2; return true; }
        if (lower == "larger") { size = parentSize * 1.2; return true; }
        if (CssValues.TryParseLength(lower, out var length) && length.Value >= 0)
        {
            size = length.Unit is CssUnit.Em or CssUnit.Percent or CssUnit.Ex
                ? length.ToPoints(parentSize, root, parentSize)
                : length.ToPoints(parentSize, root, parentSize);
            return true;
        }
        size = parentSize;
        return false;
    }

    private static bool TryBorderStyle(string value, out HtmlBorderStyle style)
    {
        HtmlBorderStyle? parsed = value.ToLowerInvariant() switch
        {
            "none" or "hidden" => HtmlBorderStyle.None,
            "solid" or "groove" or "ridge" or "inset" or "outset" => HtmlBorderStyle.Solid,
            "dashed" => HtmlBorderStyle.Dashed,
            "dotted" => HtmlBorderStyle.Dotted,
            "double" => HtmlBorderStyle.Double,
            _ => null
        };
        style = parsed ?? HtmlBorderStyle.None;
        return parsed is not null;
    }

    private static bool TryBorderWidth(string value, double em, double root, out double width)
    {
        switch (value.ToLowerInvariant())
        {
            case "thin": width = 0.75; return true;
            case "medium": width = 2.25; return true;
            case "thick": width = 3.75; return true;
        }
        if (CssValues.TryParseLength(value, out var length, allowPercent: false) && length.Value >= 0)
        {
            width = length.ToPoints(em, root, 0);
            return true;
        }
        width = 0;
        return false;
    }

    private static bool TryOptionalLength(string value, bool allowAuto, out CssLength? length)
    {
        length = null;
        string lower = value.ToLowerInvariant();
        if (allowAuto && lower is "auto" or "none") return true;
        if (!CssValues.TryParseLength(lower, out var parsed)) return false;
        length = parsed;
        return true;
    }

    private static int SideIndex(string side) => side switch { "top" => 0, "right" => 1, "bottom" => 2, _ => 3 };

    // Applies one declared value; returns false when the value is not valid for the property.
    private bool Apply(string name, string value, double em, double root, ComputedStyle? parent)
    {
        string lower = value.ToLowerInvariant();
        switch (name)
        {
            case "color":
                if (!CssValues.TryParseColor(value, out var color, out _)) return lower == "currentcolor";
                Color = color;
                return true;
            case "background-color":
                if (!CssValues.TryParseColor(value, out var background, out bool transparent)) return false;
                BackgroundColor = transparent ? null : background;
                return true;
            case "font-family":
                var names = value.Split(',').Select(n => n.Trim().Trim('"', '\'')).Where(n => n.Length > 0).ToList();
                if (names.Count == 0) return false;
                FontFamily = StandardFonts.Family(names);
                FontFamilies = names.Select(PdfHtmlFontCollection.Normalize).ToList();
                return true;
            case "font-weight":
                int weight = lower switch
                {
                    "normal" => 400,
                    "bold" => 700,
                    "bolder" => (parent?.FontWeight ?? 400) < 600 ? 700 : 900,
                    "lighter" => (parent?.FontWeight ?? 400) < 600 ? 100 : 400,
                    _ => int.TryParse(lower, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= 1000 ? n : -1
                };
                if (weight < 0) return false;
                FontWeight = weight;
                return true;
            case "font-style":
                if (lower is not ("normal" or "italic" or "oblique") && !lower.StartsWith("oblique ", StringComparison.Ordinal)) return false;
                Italic = lower != "normal";
                return true;
            case "text-align":
                CssTextAlign? align = lower switch
                {
                    "left" or "start" => CssTextAlign.Left,
                    "right" or "end" => CssTextAlign.Right,
                    "center" or "-webkit-center" => CssTextAlign.Center,
                    "justify" => CssTextAlign.Justify,
                    _ => null
                };
                if (align is null) return false;
                TextAlign = align.Value;
                return true;
            case "text-decoration":
                bool underline = false, lineThrough = false, none = false;
                foreach (var part in CssValues.SplitSpaces(lower))
                {
                    if (part == "underline") underline = true;
                    else if (part == "line-through") lineThrough = true;
                    else if (part == "none") none = true;
                }
                if (!underline && !lineThrough && !none && !lower.Contains("overline")) return false;
                Underline = underline;
                LineThrough = lineThrough;
                return true;
            case "text-indent":
                if (!CssValues.TryParseLength(lower, out var indent)) return false;
                TextIndent = Absolute(indent, em, root);
                return true;
            case "text-transform":
                CssTextTransform? transform = lower switch
                {
                    "none" => CssTextTransform.None,
                    "uppercase" => CssTextTransform.Uppercase,
                    "lowercase" => CssTextTransform.Lowercase,
                    "capitalize" => CssTextTransform.Capitalize,
                    _ => null
                };
                if (transform is null) return false;
                TextTransform = transform.Value;
                return true;
            case "line-height":
                if (lower == "normal") { LineHeightFactor = NormalLineHeight; LineHeightPoints = null; return true; }
                if (CssValues.TryParseNumber(lower, out double factor) && factor >= 0) { LineHeightFactor = factor; LineHeightPoints = null; return true; }
                if (CssValues.TryParseLength(lower, out var lineHeight) && lineHeight.Value >= 0)
                {
                    LineHeightPoints = lineHeight.ToPoints(em, root, em);
                    return true;
                }
                return false;
            case "letter-spacing":
                if (lower == "normal") { LetterSpacing = 0; return true; }
                if (!CssValues.TryParseLength(lower, out var spacing, allowPercent: false)) return false;
                LetterSpacing = spacing.ToPoints(em, root, 0);
                return true;
            case "white-space":
                CssWhiteSpace? whiteSpace = lower switch
                {
                    "normal" => CssWhiteSpace.Normal,
                    "nowrap" => CssWhiteSpace.NoWrap,
                    "pre" => CssWhiteSpace.Pre,
                    "pre-wrap" or "break-spaces" => CssWhiteSpace.PreWrap,
                    "pre-line" => CssWhiteSpace.PreLine,
                    _ => null
                };
                if (whiteSpace is null) return false;
                WhiteSpace = whiteSpace.Value;
                return true;
            case "vertical-align":
                CssVerticalAlign? vertical = lower switch
                {
                    "baseline" => CssVerticalAlign.Baseline,
                    "top" => CssVerticalAlign.Top,
                    "middle" => CssVerticalAlign.Middle,
                    "bottom" => CssVerticalAlign.Bottom,
                    "sub" => CssVerticalAlign.Sub,
                    "super" => CssVerticalAlign.Super,
                    "text-top" => CssVerticalAlign.TextTop,
                    "text-bottom" => CssVerticalAlign.TextBottom,
                    _ => null
                };
                if (vertical is null) return false;
                VerticalAlign = vertical.Value;
                return true;
            case "display":
                if (!TryDisplay(lower, out var display)) return false;
                Display = display;
                return true;
            case "width":
            case "height":
            case "min-width":
            case "max-width":
                if (!TryOptionalLength(lower, allowAuto: true, out var size) || size is { Value: < 0 }) return false;
                var absolute = size is { } given ? Absolute(given, em, root) : (CssLength?)null;
                if (name == "width") Width = absolute;
                else if (name == "height") Height = absolute;
                else if (name == "min-width") MinWidth = absolute;
                else MaxWidth = absolute;
                return true;
            case "list-style-type":
                CssListStyleType? type = lower switch
                {
                    "disc" => CssListStyleType.Disc,
                    "circle" => CssListStyleType.Circle,
                    "square" => CssListStyleType.Square,
                    "decimal" => CssListStyleType.Decimal,
                    "decimal-leading-zero" => CssListStyleType.DecimalLeadingZero,
                    "lower-alpha" or "lower-latin" => CssListStyleType.LowerAlpha,
                    "upper-alpha" or "upper-latin" => CssListStyleType.UpperAlpha,
                    "lower-roman" => CssListStyleType.LowerRoman,
                    "upper-roman" => CssListStyleType.UpperRoman,
                    "none" => CssListStyleType.None,
                    _ => null
                };
                if (type is null) return false;
                ListStyleType = type.Value;
                return true;
            case "list-style-position":
                if (lower is not ("inside" or "outside")) return false;
                ListStyleInside = lower == "inside";
                return true;
            case "border-collapse":
                if (lower is not ("collapse" or "separate")) return false;
                BorderCollapse = lower == "collapse";
                return true;
            case "border-spacing":
                var spacings = CssValues.SplitSpaces(lower);
                if (spacings.Count is < 1 or > 2) return false;
                if (!CssValues.TryParseLength(spacings[0], out var h, allowPercent: false)) return false;
                var v = h;
                if (spacings.Count == 2 && !CssValues.TryParseLength(spacings[1], out v, allowPercent: false)) return false;
                BorderSpacingHorizontal = h.ToPoints(em, root, 0);
                BorderSpacingVertical = v.ToPoints(em, root, 0);
                return true;
            case "break-before":
            case "break-after":
                CssBreak? brk = lower switch
                {
                    "auto" => CssBreak.Auto,
                    "page" or "always" or "left" or "right" or "recto" or "verso" => CssBreak.Page,
                    "avoid" or "avoid-page" => CssBreak.Avoid,
                    _ => null
                };
                if (brk is null) return false;
                if (name == "break-before") BreakBefore = brk.Value; else BreakAfter = brk.Value;
                return true;
            case "break-inside":
                if (lower is not ("auto" or "avoid" or "avoid-page")) return false;
                AvoidBreakInside = lower != "auto";
                return true;
        }

        if (name.StartsWith("margin-", StringComparison.Ordinal) || name.StartsWith("padding-", StringComparison.Ordinal))
        {
            bool margin = name[0] == 'm';
            int index = SideIndex(name.Substring(margin ? 7 : 8));
            if (!TryOptionalLength(lower, allowAuto: margin, out var length) || (!margin && length is { Value: < 0 })) return false;
            if (margin) Margins[index] = length is { } m ? Absolute(m, em, root) : null;
            else Paddings[index] = Absolute(length ?? CssLength.Zero, em, root);
            return true;
        }
        if (name.StartsWith("border-", StringComparison.Ordinal))
        {
            var parts = name.Split('-');
            if (parts.Length != 3) return false;
            int index = SideIndex(parts[1]);
            switch (parts[2])
            {
                case "width":
                    if (!TryBorderWidth(lower, em, root, out double width)) return false;
                    Borders[index] = Borders[index] with { Width = width };
                    return true;
                case "style":
                    if (!TryBorderStyle(lower, out var style)) return false;
                    Borders[index] = Borders[index] with { Style = style };
                    return true;
                case "color":
                    if (lower == "currentcolor") { _borderColorIsCurrent[index] = true; return true; }
                    if (!CssValues.TryParseColor(value, out var borderColor, out bool clear)) return false;
                    _borderColorIsCurrent[index] = false;
                    Borders[index] = Borders[index] with { Color = borderColor, Style = clear ? HtmlBorderStyle.None : Borders[index].Style };
                    return true;
            }
        }
        return false;
    }

    // Font-relative lengths are fixed at the element's font size; percentages stay for layout.
    private static CssLength Absolute(CssLength length, double em, double root) => length.Unit == CssUnit.Percent
        ? length
        : new CssLength(length.ToPoints(em, root, 0), CssUnit.Pt);

    private void InheritFrom(ComputedStyle parent, string name)
    {
        var copy = parent;
        switch (name)
        {
            case "color": Color = copy.Color; break;
            case "background-color": BackgroundColor = copy.BackgroundColor; break;
            case "font-family": FontFamily = copy.FontFamily; FontFamilies = copy.FontFamilies; break;
            case "font-weight": FontWeight = copy.FontWeight; break;
            case "font-style": Italic = copy.Italic; break;
            case "text-align": TextAlign = copy.TextAlign; break;
            case "text-indent": TextIndent = copy.TextIndent; break;
            case "text-transform": TextTransform = copy.TextTransform; break;
            case "line-height": LineHeightFactor = copy.LineHeightFactor; LineHeightPoints = copy.LineHeightPoints; break;
            case "letter-spacing": LetterSpacing = copy.LetterSpacing; break;
            case "white-space": WhiteSpace = copy.WhiteSpace; break;
            case "vertical-align": VerticalAlign = copy.VerticalAlign; break;
            case "display": Display = copy.Display; break;
            case "width": Width = copy.Width; break;
            case "height": Height = copy.Height; break;
            case "min-width": MinWidth = copy.MinWidth; break;
            case "max-width": MaxWidth = copy.MaxWidth; break;
            case "list-style-type": ListStyleType = copy.ListStyleType; break;
            case "list-style-position": ListStyleInside = copy.ListStyleInside; break;
            case "border-collapse": BorderCollapse = copy.BorderCollapse; break;
            case "border-spacing": BorderSpacingHorizontal = copy.BorderSpacingHorizontal; BorderSpacingVertical = copy.BorderSpacingVertical; break;
            default:
                if (name.StartsWith("margin-", StringComparison.Ordinal)) Margins[SideIndex(name.Substring(7))] = copy.Margins[SideIndex(name.Substring(7))];
                else if (name.StartsWith("padding-", StringComparison.Ordinal)) Paddings[SideIndex(name.Substring(8))] = copy.Paddings[SideIndex(name.Substring(8))];
                else if (name.StartsWith("border-", StringComparison.Ordinal) && name.Split('-') is { Length: 3 } parts) Borders[SideIndex(parts[1])] = copy.Borders[SideIndex(parts[1])];
                break;
        }
    }

    // A copy with the inherited properties of this style and every other property at its initial value.
    private ComputedStyle InheritedCopy() => new()
    {
        Color = Color,
        FontFamily = FontFamily,
        FontFamilies = FontFamilies,
        FontSize = FontSize,
        FontWeight = FontWeight,
        Italic = Italic,
        TextAlign = TextAlign,
        TextIndent = TextIndent,
        TextTransform = TextTransform,
        LineHeightFactor = LineHeightFactor,
        LineHeightPoints = LineHeightPoints,
        LetterSpacing = LetterSpacing,
        WhiteSpace = WhiteSpace,
        ListStyleType = ListStyleType,
        ListStyleInside = ListStyleInside,
        BorderCollapse = BorderCollapse,
        BorderSpacingHorizontal = BorderSpacingHorizontal,
        BorderSpacingVertical = BorderSpacingVertical
    };

    private void Reset(string name)
    {
        var initial = new ComputedStyle();
        InheritFrom(initial, name);
        if (name == "font-size") FontSize = DefaultFontSize;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public bool AvoidBreakInside { get; private set; }

    public PdfColor? BackgroundColor { get; private set; }

    public bool BorderCollapse { get; private set; }

    /// <summary>
    /// Gets the borders: top, right, bottom, left.
    /// </summary>
    public CssBorderSide[] Borders { get; } = Enumerable.Repeat(new CssBorderSide(2.25, HtmlBorderStyle.None, PdfColor.Black), 4).ToArray();

    public double BorderSpacingHorizontal { get; private set; }

    public double BorderSpacingVertical { get; private set; }

    public CssBreak BreakAfter { get; private set; }

    public CssBreak BreakBefore { get; private set; }

    public PdfColor Color { get; private set; } = PdfColor.Black;

    public CssDisplay Display { get; private set; } = CssDisplay.Inline;

    // The font-family names in order, normalized for matching caller fonts (WR-039); empty for the initial value.
    public IReadOnlyList<string> FontFamilies { get; private set; } = Array.Empty<string>();

    public StandardFontFamily FontFamily { get; private set; } = StandardFontFamily.Times;

    public double FontSize { get; private set; } = DefaultFontSize;

    public int FontWeight { get; private set; } = 400;

    public CssLength? Height { get; private set; }

    public bool Italic { get; private set; }

    public double LetterSpacing { get; private set; }

    /// <summary>
    /// Gets the line height as a multiple of the font size, used when <see cref="LineHeightPoints"/> is null.
    /// </summary>
    public double LineHeightFactor { get; private set; } = NormalLineHeight;

    public double? LineHeightPoints { get; private set; }

    public bool LineThrough { get; private set; }

    public bool ListStyleInside { get; private set; }

    public CssListStyleType ListStyleType { get; private set; } = CssListStyleType.Disc;

    /// <summary>
    /// Gets the margins: top, right, bottom, left; null is <c>auto</c>.
    /// </summary>
    public CssLength?[] Margins { get; } = { CssLength.Zero, CssLength.Zero, CssLength.Zero, CssLength.Zero };

    public CssLength? MaxWidth { get; private set; }

    public CssLength? MinWidth { get; private set; }

    /// <summary>
    /// Gets the padding: top, right, bottom, left.
    /// </summary>
    public CssLength[] Paddings { get; } = { CssLength.Zero, CssLength.Zero, CssLength.Zero, CssLength.Zero };

    public CssTextAlign TextAlign { get; private set; }

    public CssLength TextIndent { get; private set; } = CssLength.Zero;

    public CssTextTransform TextTransform { get; private set; }

    public bool Underline { get; private set; }

    public CssVerticalAlign VerticalAlign { get; private set; }

    public CssWhiteSpace WhiteSpace { get; private set; }

    public CssLength? Width { get; private set; }

    /// <summary>
    /// Gets the line height in points.
    /// </summary>
    public double LineHeight => LineHeightPoints ?? LineHeightFactor * FontSize;

    #endregion
}
