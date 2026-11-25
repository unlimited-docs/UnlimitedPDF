namespace UnlimitedPDF;

/// <summary>
/// The base direction of text: which edge a line reads from (WR-041). Characters inside a line are always ordered by
/// the Unicode Bidirectional Algorithm, so a right-to-left word in left-to-right text, or the reverse, reads correctly
/// in either direction.
/// </summary>
public enum PdfTextDirection
{
    /// <summary>
    /// The direction of the text's first strong character: right to left for Arabic, Hebrew, or Thaana letters, else
    /// left to right (also when the text has no letter at all).
    /// </summary>
    Auto,

    /// <summary>
    /// Left to right, as for English text.
    /// </summary>
    LeftToRight,

    /// <summary>
    /// Right to left, as for Arabic, Persian, Urdu, Hebrew, or Dhivehi text.
    /// </summary>
    RightToLeft
}
