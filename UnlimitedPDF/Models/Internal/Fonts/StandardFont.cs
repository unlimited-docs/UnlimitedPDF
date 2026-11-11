namespace UnlimitedPDF.Models.Internal.Fonts;

/// <summary>
/// The twelve standard fonts HTML text can use (WR-038). Every PDF viewer has them, so they are never embedded.
/// </summary>
internal enum StandardFont
{
    Helvetica,
    HelveticaBold,
    HelveticaOblique,
    HelveticaBoldOblique,
    TimesRoman,
    TimesBold,
    TimesItalic,
    TimesBoldItalic,
    Courier,
    CourierBold,
    CourierOblique,
    CourierBoldOblique
}

/// <summary>
/// The three standard font families.
/// </summary>
internal enum StandardFontFamily
{
    Helvetica,
    Times,
    Courier
}

/// <summary>
/// Chooses a standard font for CSS <c>font-family</c>, <c>font-weight</c>, and <c>font-style</c> values (WR-038).
/// </summary>
internal static class StandardFonts
{
    #region Fields and Constants

    // The first weight drawn bold.
    public const int BoldWeight = 600;

    // CSS family names (lowercase) the library knows; any other name is skipped in favor of the next one in the list.
    private static readonly Dictionary<string, StandardFontFamily> KnownFamilies = new(StringComparer.Ordinal)
    {
        ["serif"] = StandardFontFamily.Times,
        ["times"] = StandardFontFamily.Times,
        ["times new roman"] = StandardFontFamily.Times,
        ["times-roman"] = StandardFontFamily.Times,
        ["monospace"] = StandardFontFamily.Courier,
        ["courier"] = StandardFontFamily.Courier,
        ["courier new"] = StandardFontFamily.Courier,
        ["sans-serif"] = StandardFontFamily.Helvetica,
        ["helvetica"] = StandardFontFamily.Helvetica,
        ["arial"] = StandardFontFamily.Helvetica
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the PDF base font name, for example "Times-BoldItalic".
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The name.</returns>
    public static string BaseFontName(StandardFont font) => font switch
    {
        StandardFont.Helvetica => "Helvetica",
        StandardFont.HelveticaBold => "Helvetica-Bold",
        StandardFont.HelveticaOblique => "Helvetica-Oblique",
        StandardFont.HelveticaBoldOblique => "Helvetica-BoldOblique",
        StandardFont.TimesRoman => "Times-Roman",
        StandardFont.TimesBold => "Times-Bold",
        StandardFont.TimesItalic => "Times-Italic",
        StandardFont.TimesBoldItalic => "Times-BoldItalic",
        StandardFont.Courier => "Courier",
        StandardFont.CourierBold => "Courier-Bold",
        StandardFont.CourierOblique => "Courier-Oblique",
        _ => "Courier-BoldOblique"
    };

    /// <summary>
    /// Returns the family for a CSS <c>font-family</c> list: the first name the library knows, else Helvetica.
    /// </summary>
    /// <param name="families">The family names in order, unquoted.</param>
    /// <returns>The family.</returns>
    public static StandardFontFamily Family(IEnumerable<string> families)
    {
        foreach (var name in families)
        {
            if (KnownFamilies.TryGetValue(name.Trim().ToLowerInvariant(), out var family)) return family;
        }
        return StandardFontFamily.Helvetica;
    }

    /// <summary>
    /// Returns the font for a family, a CSS weight, and whether the style is italic or oblique.
    /// </summary>
    /// <param name="family">The family.</param>
    /// <param name="weight">The CSS weight, 1 to 1000; 600 and above is bold.</param>
    /// <param name="italic">Whether the style is italic or oblique.</param>
    /// <returns>The font.</returns>
    public static StandardFont Select(StandardFontFamily family, int weight, bool italic)
    {
        bool bold = weight >= BoldWeight;
        return family switch
        {
            StandardFontFamily.Times => bold
                ? (italic ? StandardFont.TimesBoldItalic : StandardFont.TimesBold)
                : (italic ? StandardFont.TimesItalic : StandardFont.TimesRoman),
            StandardFontFamily.Courier => bold
                ? (italic ? StandardFont.CourierBoldOblique : StandardFont.CourierBold)
                : (italic ? StandardFont.CourierOblique : StandardFont.Courier),
            _ => bold
                ? (italic ? StandardFont.HelveticaBoldOblique : StandardFont.HelveticaBold)
                : (italic ? StandardFont.HelveticaOblique : StandardFont.Helvetica)
        };
    }

    #endregion

    #endregion
}
