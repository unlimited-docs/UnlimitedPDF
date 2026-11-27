using UnlimitedPDF.Models.Internal.Text.Bidi;

namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// Which right-to-left characters the library shapes (WR-039): Arabic script, Hebrew, and Thaana. A right-to-left
/// letter of any other script (Syriac, N'Ko, Samaritan, Mandaic, Adlam, Hanifi Rohingya, and others) is drawn in its
/// font in bidirectional order but without its script's shaping, so it is reported like a missing character.
/// </summary>
internal static class RightToLeftSupport
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether <paramref name="codePoint"/> is in a right-to-left script the library shapes.
    /// </summary>
    public static bool IsShaped(int codePoint) => codePoint is
        >= 0x0590 and <= 0x05FF or >= 0xFB1D and <= 0xFB4F // Hebrew
        or >= 0x0780 and <= 0x07BF // Thaana
        or >= 0x0600 and <= 0x06FF or >= 0x0750 and <= 0x077F or >= 0x0870 and <= 0x089F or >= 0x08A0 and <= 0x08FF
        or >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF; // Arabic

    /// <summary>
    /// Returns whether <paramref name="codePoint"/> is a right-to-left letter (bidirectional class R or AL) in a script
    /// the library does not shape. Bidirectional marks and controls are not letters.
    /// </summary>
    public static bool IsUnshaped(int codePoint) =>
        !BidiParagraph.IsFormatCharacter(codePoint) && !IsShaped(codePoint) && BidiData.ClassOf(codePoint) is BidiClass.R or BidiClass.AL;

    /// <summary>
    /// Returns the text elements of <paramref name="text"/> that hold a right-to-left letter the library does not shape,
    /// each once, in order.
    /// </summary>
    public static IEnumerable<string> UnshapedElements(string text) =>
        HasUnshaped(text) ? EachUnshapedElement(text) : Array.Empty<string>();

    /// <summary>
    /// Returns whether <paramref name="text"/> holds a right-to-left letter the library does not shape.
    /// </summary>
    public static bool HasUnshaped(string text)
    {
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            int codePoint = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
            if (codePoint >= 0x0590 && IsUnshaped(codePoint)) return true;
        }
        return false;
    }

    #endregion

    #region Private Methods

    // Walks the text elements only for text that has such a letter; most text has none.
    private static IEnumerable<string> EachUnshapedElement(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (HasUnshaped(element) && seen.Add(element)) yield return element;
        }
    }

    #endregion

    #endregion
}
