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
