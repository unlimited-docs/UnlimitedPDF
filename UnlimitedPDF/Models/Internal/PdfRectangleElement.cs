using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// A filled rectangle placed at page coordinates. It never paginates (WR-031).
/// </summary>
internal sealed class PdfRectangleElement
{
    #region Constructors and Finalizers

    public PdfRectangleElement(double x, double y, double width, double height, PdfColor color)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Color = color;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the operators that fill the rectangle.
    /// </summary>
    /// <returns>The operators.</returns>
    public override string ToString() => LayoutDrawing.Rectangle(X, Y, Width, Height, Color);

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the fill color.
    /// </summary>
    public PdfColor Color { get; }

    /// <summary>
    /// Gets the height in points.
    /// </summary>
    public double Height { get; }

    /// <summary>
    /// Gets the width in points.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Gets the x-coordinate of the bottom-left corner, in points.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Gets the y-coordinate of the bottom-left corner, in points.
    /// </summary>
    public double Y { get; }

    #endregion
}
