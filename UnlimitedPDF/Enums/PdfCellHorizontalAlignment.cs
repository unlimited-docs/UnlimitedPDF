namespace UnlimitedPDF;

/// <summary>
/// Enum represents horizontal alignment options for content within a PDF cell.
/// </summary>
public enum PdfCellHorizontalAlignment
{
    /// <summary>
    /// Aligns content to the left side of the cell.
    /// </summary>
    Left,

    /// <summary>
    /// Aligns content to the center of the cell.
    /// </summary>
    Center,

    /// <summary>
    /// Aligns content to the right side of the cell.
    /// </summary>
    Right,

    /// <summary>
    /// Aligns content to the side its direction reads from: the left side for left-to-right text, the right side for
    /// right-to-left text; images follow the table's or document's direction (WR-041). This is the default.
    /// </summary>
    Start,

    /// <summary>
    /// Aligns content to the side its direction reads towards: the right side for left-to-right text, the left side
    /// for right-to-left text; images follow the table's or document's direction (WR-041).
    /// </summary>
    End
}
