namespace UnlimitedPDF;

/// <summary>
/// A line of text in a table cell's content.
/// </summary>
public sealed class PdfCellText : PdfCellContent
{
    #region Fields and Constants

    /// <summary>
    /// The font size table text is drawn at, in points.
    /// </summary>
    internal const double DefaultFontSize = 12;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCellText"/> class.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfCellText class.")]
    public PdfCellText(string text)
        : this(text, DefaultFontSize)
    {
    }

    // Table text is always drawn at 12 pt today (WR-013); other sizes exist only to test the line-height rule.
    internal PdfCellText(string text, double fontSize)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        FontSize = fontSize;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the font size in points.
    /// </summary>
    internal double FontSize { get; }

    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; }

    #endregion
}
