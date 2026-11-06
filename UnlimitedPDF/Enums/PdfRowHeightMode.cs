namespace UnlimitedPDF;

/// <summary>
/// How a table row's height relates to its content.
/// </summary>
public enum PdfRowHeightMode
{
    /// <summary>
    /// The row keeps its height, and image content is scaled down to fit.
    /// </summary>
    Exactly,

    /// <summary>
    /// The height is a minimum, and the row grows to fit its content.
    /// </summary>
    AtLeast
}
