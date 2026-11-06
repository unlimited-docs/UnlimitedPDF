namespace UnlimitedPDF.Enums;

/// <summary>
/// How a line of header, footer, or body text is placed across the width of its area.
/// </summary>
public enum PdfTextAlignment
{
    /// <summary>
    /// The text starts the given offset after the area's left edge.
    /// </summary>
    Left,

    /// <summary>
    /// The text is centered in the area, shifted by the given offset.
    /// </summary>
    Center,

    /// <summary>
    /// The text ends the given offset before the area's right edge.
    /// </summary>
    Right
}
