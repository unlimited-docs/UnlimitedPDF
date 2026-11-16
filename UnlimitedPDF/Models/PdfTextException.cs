namespace UnlimitedPDF;

/// <summary>
/// The exception <see cref="PdfDocument.Save(Stream)"/> and the other save methods throw when
/// <see cref="PdfDocument.FailOnMissingCharacters"/> is set and text holds a character that no font can draw, which
/// would otherwise be drawn as '?' (D-015). Nothing is written when it is thrown.
/// </summary>
public class PdfTextException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextException"/> class.
    /// </summary>
    /// <param name="character">The character (one text element, which may be several code units) no font has.</param>
    /// <param name="text">The text that holds it.</param>
    /// <param name="pageNumber">The 1-based number of the saved page the text is on.</param>
    [AddedVersion("1.1.0")]
    [Description("Initializes a new instance of the PdfTextException class.")]
    public PdfTextException(string character, string text, int pageNumber)
        : base(Describe(character, text, pageNumber))
    {
        Character = character;
        Text = text;
        PageNumber = pageNumber;
    }

    #endregion

    #region Methods

    #region Private Methods

    private static string Describe(string character, string text, int pageNumber)
    {
        string codes = string.Join(" ", EnumerateCodePoints(character ?? string.Empty).Select(cp => "U+" + cp.ToString("X4", CultureInfo.InvariantCulture)));
        return string.Format(CultureInfo.InvariantCulture, "No font has the character {0} '{1}' in the text \"{2}\" on page {3}. Add a font that has it to PdfDocument.FontFallback, or remove it from the text.", codes, character, text, pageNumber);
    }

    private static IEnumerable<int> EnumerateCodePoints(string text)
    {
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            yield return char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the character no font has: one text element, which may be several code units (for example an emoji).
    /// </summary>
    public string Character { get; }

    /// <summary>
    /// Gets the 1-based number of the saved page the text is on.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the text that holds the character.
    /// </summary>
    public string Text { get; }

    #endregion
}
