namespace UnlimitedPDF;

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
    Right,

    /// <summary>
    /// The text starts the given offset after the edge its direction reads from: the left edge for left-to-right text,
    /// the right edge for right-to-left text (WR-041). This is the default.
    /// </summary>
    Start,

    /// <summary>
    /// The text ends the given offset before the edge its direction reads towards: the right edge for left-to-right
    /// text, the left edge for right-to-left text (WR-041).
    /// </summary>
    End
}
