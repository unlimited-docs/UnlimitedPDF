namespace UnlimitedPDF.Models.Internal.Html.Css;

/// <summary>
/// A CSS length unit (WR-038).
/// </summary>
internal enum CssUnit
{
    Px,
    Pt,
    Pc,
    In,
    Cm,
    Mm,
    Em,
    Rem,
    Ex,
    Percent
}

/// <summary>
/// A CSS length or percentage as written.
/// </summary>
/// <param name="Value">The number.</param>
/// <param name="Unit">The unit.</param>
internal readonly record struct CssLength(double Value, CssUnit Unit)
{
    /// <summary>
    /// Gets zero points.
    /// </summary>
    public static CssLength Zero => new(0, CssUnit.Pt);

    /// <summary>
    /// Returns the length in points.
    /// </summary>
    /// <param name="fontSize">The element's font size in points, for <c>em</c> and <c>ex</c>.</param>
    /// <param name="rootFontSize">The root font size in points, for <c>rem</c>.</param>
    /// <param name="percentBase">What 100% is, in points.</param>
    /// <returns>The length in points.</returns>
    public double ToPoints(double fontSize, double rootFontSize, double percentBase) => Unit switch
    {
        CssUnit.Px => Value * 0.75,
        CssUnit.Pt => Value,
        CssUnit.Pc => Value * 12,
        CssUnit.In => Value * 72,
        CssUnit.Cm => Value * 72 / 2.54,
        CssUnit.Mm => Value * 72 / 25.4,
        CssUnit.Em => Value * fontSize,
        CssUnit.Rem => Value * rootFontSize,
        CssUnit.Ex => Value * fontSize / 2,
        _ => Value * percentBase / 100
    };
}

/// <summary>
/// Parses CSS values (WR-038, AC-07): lengths in <c>px pt pc in cm mm em rem ex %</c> (1 px = 0.75 pt, WR-023), and
/// colors as the CSS named colors, <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>, and
/// <c>rgba()</c>, with alpha blended onto white.
/// </summary>
internal static class CssValues
{
    #region Fields and Constants

    // The CSS named colors, name:rrggbb.
    private const string NamedColorList =
        "aliceblue:f0f8ff antiquewhite:faebd7 aqua:00ffff aquamarine:7fffd4 azure:f0ffff beige:f5f5dc bisque:ffe4c4 " +
        "black:000000 blanchedalmond:ffebcd blue:0000ff blueviolet:8a2be2 brown:a52a2a burlywood:deb887 cadetblue:5f9ea0 " +
        "chartreuse:7fff00 chocolate:d2691e coral:ff7f50 cornflowerblue:6495ed cornsilk:fff8dc crimson:dc143c cyan:00ffff " +
        "darkblue:00008b darkcyan:008b8b darkgoldenrod:b8860b darkgray:a9a9a9 darkgreen:006400 darkgrey:a9a9a9 " +
        "darkkhaki:bdb76b darkmagenta:8b008b darkolivegreen:556b2f darkorange:ff8c00 darkorchid:9932cc darkred:8b0000 " +
        "darksalmon:e9967a darkseagreen:8fbc8f darkslateblue:483d8b darkslategray:2f4f4f darkslategrey:2f4f4f " +
        "darkturquoise:00ced1 darkviolet:9400d3 deeppink:ff1493 deepskyblue:00bfff dimgray:696969 dimgrey:696969 " +
        "dodgerblue:1e90ff firebrick:b22222 floralwhite:fffaf0 forestgreen:228b22 fuchsia:ff00ff gainsboro:dcdcdc " +
        "ghostwhite:f8f8ff gold:ffd700 goldenrod:daa520 gray:808080 green:008000 greenyellow:adff2f grey:808080 " +
        "honeydew:f0fff0 hotpink:ff69b4 indianred:cd5c5c indigo:4b0082 ivory:fffff0 khaki:f0e68c lavender:e6e6fa " +
        "lavenderblush:fff0f5 lawngreen:7cfc00 lemonchiffon:fffacd lightblue:add8e6 lightcoral:f08080 lightcyan:e0ffff " +
        "lightgoldenrodyellow:fafad2 lightgray:d3d3d3 lightgreen:90ee90 lightgrey:d3d3d3 lightpink:ffb6c1 " +
        "lightsalmon:ffa07a lightseagreen:20b2aa lightskyblue:87cefa lightslategray:778899 lightslategrey:778899 " +
        "lightsteelblue:b0c4de lightyellow:ffffe0 lime:00ff00 limegreen:32cd32 linen:faf0e6 magenta:ff00ff maroon:800000 " +
        "mediumaquamarine:66cdaa mediumblue:0000cd mediumorchid:ba55d3 mediumpurple:9370db mediumseagreen:3cb371 " +
        "mediumslateblue:7b68ee mediumspringgreen:00fa9a mediumturquoise:48d1cc mediumvioletred:c71585 " +
        "midnightblue:191970 mintcream:f5fffa mistyrose:ffe4e1 moccasin:ffe4b5 navajowhite:ffdead navy:000080 " +
        "oldlace:fdf5e6 olive:808000 olivedrab:6b8e23 orange:ffa500 orangered:ff4500 orchid:da70d6 palegoldenrod:eee8aa " +
        "palegreen:98fb98 paleturquoise:afeeee palevioletred:db7093 papayawhip:ffefd5 peachpuff:ffdab9 peru:cd853f " +
        "pink:ffc0cb plum:dda0dd powderblue:b0e0e6 purple:800080 rebeccapurple:663399 red:ff0000 rosybrown:bc8f8f " +
        "royalblue:4169e1 saddlebrown:8b4513 salmon:fa8072 sandybrown:f4a460 seagreen:2e8b57 seashell:fff5ee " +
        "sienna:a0522d silver:c0c0c0 skyblue:87ceeb slateblue:6a5acd slategray:708090 slategrey:708090 snow:fffafa " +
        "springgreen:00ff7f steelblue:4682b4 tan:d2b48c teal:008080 thistle:d8bfd8 tomato:ff6347 turquoise:40e0d0 " +
        "violet:ee82ee wheat:f5deb3 white:ffffff whitesmoke:f5f5f5 yellow:ffff00 yellowgreen:9acd32";

    private static readonly Dictionary<string, PdfColor> NamedColors = BuildNamedColors();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses a color. <c>transparent</c> and fully transparent colors give <paramref name="transparent"/> true.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <param name="color">The color, blended onto white.</param>
    /// <param name="transparent">Whether the color is fully transparent.</param>
    /// <returns>True when the value is a color.</returns>
    public static bool TryParseColor(string text, out PdfColor color, out bool transparent)
    {
        color = PdfColor.Black;
        transparent = false;
        string value = text.Trim().ToLowerInvariant();
        if (value == "transparent")
        {
            transparent = true;
            color = PdfColor.White;
            return true;
        }
        if (NamedColors.TryGetValue(value, out var named))
        {
            color = named;
            return true;
        }
        if (value.StartsWith("#", StringComparison.Ordinal)) return TryHex(value.Substring(1), out color, out transparent);
        if ((value.StartsWith("rgb(", StringComparison.Ordinal) || value.StartsWith("rgba(", StringComparison.Ordinal)) && value.EndsWith(")", StringComparison.Ordinal))
        {
            int open = value.IndexOf('(');
            var parts = value.Substring(open + 1, value.Length - open - 2)
                .Replace("/", " / ").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "/").ToArray();
            if (parts.Length is not (3 or 4)) return false;
            var channels = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (!TryChannel(parts[i], out channels[i])) return false;
            }
            double alpha = 1;
            if (parts.Length == 4 && !TryAlpha(parts[3], out alpha)) return false;
            color = Blend(channels[0], channels[1], channels[2], alpha);
            transparent = alpha <= 0;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Parses a length or percentage. A bare zero is zero points; other unitless numbers are not lengths.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <param name="length">The length.</param>
    /// <param name="allowPercent">Whether a percentage is accepted.</param>
    /// <returns>True when the value is a length.</returns>
    public static bool TryParseLength(string text, out CssLength length, bool allowPercent = true)
    {
        length = CssLength.Zero;
        string value = text.Trim().ToLowerInvariant();
        if (value.Length == 0) return false;

        int end = 0;
        while (end < value.Length && (char.IsDigit(value[end]) || value[end] is '.' or '-' or '+')) end++;
        if (end == 0 || !double.TryParse(value.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
            double.IsNaN(number) || double.IsInfinity(number))
            return false;

        string unit = value.Substring(end);
        CssUnit? parsed = unit switch
        {
            "px" => CssUnit.Px,
            "pt" => CssUnit.Pt,
            "pc" => CssUnit.Pc,
            "in" => CssUnit.In,
            "cm" => CssUnit.Cm,
            "mm" => CssUnit.Mm,
            "em" => CssUnit.Em,
            "rem" => CssUnit.Rem,
            "ex" => CssUnit.Ex,
            "%" when allowPercent => CssUnit.Percent,
            "" when number == 0 => CssUnit.Pt,
            _ => null
        };
        if (parsed is null) return false;
        length = new CssLength(number, parsed.Value);
        return true;
    }

    /// <summary>
    /// Parses a plain number.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <param name="number">The number.</param>
    /// <returns>True when the value is a finite number.</returns>
    public static bool TryParseNumber(string text, out double number) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number);

    /// <summary>
    /// Splits a value into space-separated parts, keeping parentheses and quotes together.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The parts.</returns>
    public static List<string> SplitSpaces(string value)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        char quote = '\0';
        foreach (char c in value)
        {
            if (quote != '\0')
            {
                sb.Append(c);
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '"' or '\'') quote = c;
            if (c == '(') depth++;
            if (c == ')') depth = Math.Max(0, depth - 1);
            if (char.IsWhiteSpace(c) && depth == 0)
            {
                if (sb.Length > 0) { parts.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) parts.Add(sb.ToString());
        return parts;
    }

    #endregion

    #region Private Methods

    private static PdfColor Blend(double r, double g, double b, double alpha)
    {
        alpha = Math.Max(0, Math.Min(1, alpha));
        return new PdfColor(Round(r * alpha + (1 - alpha)), Round(g * alpha + (1 - alpha)), Round(b * alpha + (1 - alpha)));
    }

    private static Dictionary<string, PdfColor> BuildNamedColors()
    {
        var colors = new Dictionary<string, PdfColor>(StringComparer.Ordinal);
        foreach (var pair in NamedColorList.Split(' '))
        {
            int colon = pair.IndexOf(':');
            TryHex(pair.Substring(colon + 1), out var color, out _);
            colors[pair.Substring(0, colon)] = color;
        }
        return colors;
    }

    // Keeps channel values short in the output (PR-006 numbers are written with up to six decimals).
    private static double Round(double value) => Math.Round(value, 4);

    private static bool TryAlpha(string text, out double alpha)
    {
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            bool ok = TryParseNumber(text.TrimEnd('%'), out double percent);
            alpha = percent / 100;
            return ok;
        }
        return TryParseNumber(text, out alpha);
    }

    private static bool TryChannel(string text, out double channel)
    {
        bool ok;
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            ok = TryParseNumber(text.TrimEnd('%'), out double percent);
            channel = percent / 100;
        }
        else
        {
            ok = TryParseNumber(text, out double value);
            channel = value / 255;
        }
        channel = Math.Max(0, Math.Min(1, channel));
        return ok;
    }

    private static bool TryHex(string digits, out PdfColor color, out bool transparent)
    {
        color = PdfColor.Black;
        transparent = false;
        if (!digits.All(Uri.IsHexDigit)) return false;
        if (digits.Length is 3 or 4) digits = string.Concat(digits.Select(c => new string(c, 2)));
        if (digits.Length is not (6 or 8)) return false;
        double Channel(int start) => int.Parse(digits.Substring(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture) / 255.0;
        double alpha = digits.Length == 8 ? Channel(6) : 1;
        color = Blend(Channel(0), Channel(2), Channel(4), alpha);
        transparent = alpha <= 0;
        return true;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of CSS named colors known.
    /// </summary>
    public static int NamedColorCount => NamedColors.Count;

    #endregion
}
