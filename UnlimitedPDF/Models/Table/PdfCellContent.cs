using System.Diagnostics.CodeAnalysis;

namespace UnlimitedPDF;

/// <summary>
/// One item of a table cell's content: <see cref="PdfCellText"/> or <see cref="PdfCellImage"/>. Items are immutable,
/// so one item can be used in several cells. A string or a <see cref="PdfImage"/> converts to an item implicitly,
/// for example <c>cell.SetContent(logo, "Laptop")</c>.
/// </summary>
public abstract class PdfCellContent
{
    #region Constructors and Finalizers

    // Only the library defines item types, so every item is one the cell knows how to lay out.
    private protected PdfCellContent()
    {
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Converts an image to an image item. A null image converts to null.
    /// </summary>
    /// <param name="image">The image.</param>
    [AddedVersion("1.0.0")]
    [Description("Converts an image to an image item.")]
    [return: NotNullIfNotNull("image")]
    public static implicit operator PdfCellContent?(PdfImage? image) => image is null ? null : new PdfCellImage(image);

    /// <summary>
    /// Converts a string to a text item. A null string converts to null.
    /// </summary>
    /// <param name="text">The text.</param>
    [AddedVersion("1.0.0")]
    [Description("Converts a string to a text item.")]
    [return: NotNullIfNotNull("text")]
    public static implicit operator PdfCellContent?(string? text) => text is null ? null : new PdfCellText(text);

    #endregion

    #endregion
}
