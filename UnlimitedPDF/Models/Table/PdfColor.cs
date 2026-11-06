namespace UnlimitedPDF;

/// <summary>
/// An RGB color. Each channel is a number from 0 (none) to 1 (full), so <c>new PdfColor(1, 1, 1)</c> is white. Use
/// <see cref="FromRgb"/> for 0–255 channels or <see cref="FromHex"/> for <c>#RRGGBB</c> strings.
/// </summary>
/// <param name="R">The red channel, from 0 to 1.</param>
/// <param name="G">The green channel, from 0 to 1.</param>
/// <param name="B">The blue channel, from 0 to 1.</param>
public record PdfColor(double R, double G, double B)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Creates a color from a hexadecimal string: six hex digits, <c>RRGGBB</c>, with or without a leading <c>#</c>,
    /// in any letter case. For example, <c>"#002A73"</c> and <c>"002a73"</c> are the same color.
    /// </summary>
    /// <param name="hex">The color as <c>#RRGGBB</c> or <c>RRGGBB</c>.</param>
    /// <returns>The color, equal to <see cref="FromRgb"/> of the same three bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hex"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="hex"/> is not six hex digits with an optional leading
    /// <c>#</c>.</exception>
    [AddedVersion("1.0.0")]
    [Description("Creates a color from a #RRGGBB or RRGGBB hexadecimal string.")]
    public static PdfColor FromHex(string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));

        string digits = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
        if (digits.Length != 6 || !digits.All(IsHexDigit))
            throw new ArgumentException($"The color \"{hex}\" is not six hex digits (#RRGGBB or RRGGBB).", nameof(hex));

        return FromRgb(ParseByte(digits, 0), ParseByte(digits, 2), ParseByte(digits, 4));
    }

    /// <summary>
    /// Creates a color from 0–255 channels. For example, <c>FromRgb(0, 172, 140)</c> is teal.
    /// </summary>
    /// <param name="r">The red channel, from 0 to 255.</param>
    /// <param name="g">The green channel, from 0 to 255.</param>
    /// <param name="b">The blue channel, from 0 to 255.</param>
    /// <returns>The color, with each channel divided by 255.</returns>
    [AddedVersion("1.0.0")]
    [Description("Creates a color from red, green, and blue channels from 0 to 255.")]
    public static PdfColor FromRgb(byte r, byte g, byte b) => new(r / 255.0, g / 255.0, b / 255.0);

    #endregion

    #region Private Methods

    private static bool IsHexDigit(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static byte ParseByte(string digits, int start) =>
        byte.Parse(digits.Substring(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets black (0, 0, 0).
    /// </summary>
    public static PdfColor Black => new(0, 0, 0);

    /// <summary>
    /// Gets a light gray (0.9, 0.9, 0.9).
    /// </summary>
    public static PdfColor LightGray => new(0.9, 0.9, 0.9);

    /// <summary>
    /// Gets white (1, 1, 1).
    /// </summary>
    public static PdfColor White => new(1, 1, 1);

    #endregion
}
