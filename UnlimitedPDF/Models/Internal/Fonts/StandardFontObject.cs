namespace UnlimitedPDF.Models.Internal.Fonts;

/// <summary>
/// The font dictionary of one standard font used by HTML text (WR-038): a Type1 font that is not embedded, with the
/// WinAnsi encoding. Each font has one fixed page resource name, /HF1 to /HF12.
/// </summary>
internal sealed class StandardFontObject
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the font dictionary.
    /// </summary>
    /// <param name="font">The font.</param>
    public StandardFontObject(StandardFont font)
    {
        Font = font;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the page resource name of a font, without the slash, for example "HF5".
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The name.</returns>
    public static string ResourceName(StandardFont font) => "HF" + ((int)font + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the font dictionary.
    /// </summary>
    /// <returns>The dictionary syntax.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Font");
        sb.AppendLine("/Subtype /Type1");
        sb.AppendLine($"/BaseFont /{StandardFonts.BaseFontName(Font)}");
        sb.AppendLine("/Encoding /WinAnsiEncoding");
        sb.AppendLine(">>");
        return sb.ToString();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the font.
    /// </summary>
    public StandardFont Font { get; }

    #endregion
}
