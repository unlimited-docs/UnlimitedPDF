namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// Resolves a text's base direction (WR-041): the call's, else the cell's or table's, else the document's; automatic
/// takes the first strong character.
/// </summary>
internal static class TextDirections
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the paragraph level the text pipeline takes: 0 left to right, 1 right to left, or null for automatic.
    /// </summary>
    /// <param name="directions">The directions from the most specific (the call) to the least (the document); the
    /// first that is set wins.</param>
    public static int? BaseLevel(params PdfTextDirection?[] directions)
    {
        foreach (var direction in directions)
        {
            if (direction is not { } value) continue;
            return value switch
            {
                PdfTextDirection.LeftToRight => 0,
                PdfTextDirection.RightToLeft => 1,
                _ => null
            };
        }
        return null;
    }

    /// <summary>
    /// Returns whether <paramref name="text"/> reads right to left at <paramref name="baseLevel"/> (null: from its first
    /// strong character).
    /// </summary>
    public static bool IsRightToLeft(string text, int? baseLevel) =>
        baseLevel == 1 || (baseLevel is null && Bidi.BidiParagraph.FirstStrongLevel(text) == 1);

    /// <summary>
    /// Returns the physical alignment <paramref name="alignment"/> stands for in a line of the given direction:
    /// <c>Start</c> and <c>End</c> become <c>Left</c> or <c>Right</c>.
    /// </summary>
    public static PdfTextAlignment Physical(PdfTextAlignment alignment, bool rightToLeft) => alignment switch
    {
        PdfTextAlignment.Start => rightToLeft ? PdfTextAlignment.Right : PdfTextAlignment.Left,
        PdfTextAlignment.End => rightToLeft ? PdfTextAlignment.Left : PdfTextAlignment.Right,
        _ => alignment
    };

    /// <summary>
    /// Returns the physical cell alignment <paramref name="alignment"/> stands for in content of the given direction.
    /// </summary>
    public static PdfCellHorizontalAlignment Physical(PdfCellHorizontalAlignment alignment, bool rightToLeft) => alignment switch
    {
        PdfCellHorizontalAlignment.Start => rightToLeft ? PdfCellHorizontalAlignment.Right : PdfCellHorizontalAlignment.Left,
        PdfCellHorizontalAlignment.End => rightToLeft ? PdfCellHorizontalAlignment.Left : PdfCellHorizontalAlignment.Right,
        _ => alignment
    };

    /// <summary>
    /// Returns <paramref name="direction"/>, or throws when it is not a defined value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public static PdfTextDirection Checked(PdfTextDirection direction, string paramName) =>
        direction is PdfTextDirection.Auto or PdfTextDirection.LeftToRight or PdfTextDirection.RightToLeft
            ? direction
            : throw new ArgumentOutOfRangeException(paramName, direction, "Not a defined text direction.");

    #endregion

    #endregion
}
