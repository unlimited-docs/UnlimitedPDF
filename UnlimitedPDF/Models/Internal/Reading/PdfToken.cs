namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// The kind of a <see cref="PdfToken"/>.
/// </summary>
internal enum PdfTokenKind
{
    /// <summary>
    /// No more data.
    /// </summary>
    EndOfFile,

    /// <summary>
    /// A number without a decimal point.
    /// </summary>
    Integer,

    /// <summary>
    /// A number with a decimal point.
    /// </summary>
    Real,

    /// <summary>
    /// A literal string <c>( ... )</c>.
    /// </summary>
    LiteralString,

    /// <summary>
    /// A hexadecimal string <c>&lt; ... &gt;</c>.
    /// </summary>
    HexString,

    /// <summary>
    /// A name <c>/Name</c>.
    /// </summary>
    Name,

    /// <summary>
    /// <c>[</c>.
    /// </summary>
    ArrayStart,

    /// <summary>
    /// <c>]</c>.
    /// </summary>
    ArrayEnd,

    /// <summary>
    /// <c>&lt;&lt;</c>.
    /// </summary>
    DictionaryStart,

    /// <summary>
    /// <c>&gt;&gt;</c>.
    /// </summary>
    DictionaryEnd,

    /// <summary>
    /// Any other run of regular characters, such as <c>obj</c>, <c>R</c>, or <c>true</c>.
    /// </summary>
    Keyword,
}

/// <summary>
/// One token of PDF syntax.
/// </summary>
/// <param name="Kind">The token kind.</param>
/// <param name="Start">The offset of the token's first byte.</param>
/// <param name="End">The offset just after the token.</param>
/// <param name="Text">The text of a number, name, or keyword.</param>
/// <param name="Bytes">The decoded bytes of a string.</param>
internal readonly record struct PdfToken(PdfTokenKind Kind, int Start, int End, string? Text = null, byte[]? Bytes = null)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether this is the keyword <paramref name="keyword"/>.
    /// </summary>
    /// <param name="keyword">The keyword, for example "obj".</param>
    /// <returns>True when the token is that keyword.</returns>
    public bool IsKeyword(string keyword) => Kind == PdfTokenKind.Keyword && string.Equals(Text, keyword, StringComparison.Ordinal);

    #endregion

    #endregion
}
