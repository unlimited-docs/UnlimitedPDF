using UnlimitedPDF.Enums;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Tables demo, with working defaults. Only options the library renders today are offered.
/// </summary>
public sealed class TablesInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the body cell background, as a CSS hex color such as "#ffffff".
    /// </summary>
    public string BodyBackground { get; set; } = "#ffffff";

    /// <summary>
    /// Gets or sets the border color for every side of every cell.
    /// </summary>
    public string BorderColor { get; set; } = "#5b6b88";

    /// <summary>
    /// Gets or sets the border width, in points.
    /// </summary>
    public double BorderWidth { get; set; } = 0.75;

    /// <summary>
    /// Gets or sets the first column's width, in points.
    /// </summary>
    public double Column1Width { get; set; } = 220;

    /// <summary>
    /// Gets or sets the second column's width, in points.
    /// </summary>
    public double Column2Width { get; set; } = 140;

    /// <summary>
    /// Gets or sets the third column's width, in points.
    /// </summary>
    public double Column3Width { get; set; } = 130;

    /// <summary>
    /// Gets or sets the header row background.
    /// </summary>
    public string HeaderBackground { get; set; } = "#e8eefc";

    /// <summary>
    /// Gets or sets the horizontal alignment of body cells.
    /// </summary>
    public PdfCellHorizontalAlignment HorizontalAlignment { get; set; } = PdfCellHorizontalAlignment.Left;

    /// <summary>
    /// Gets or sets the cell padding, in points.
    /// </summary>
    public double Padding { get; set; } = 6;

    /// <summary>
    /// Gets or sets the row height, in points.
    /// </summary>
    public double RowHeight { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether rows keep their height exactly or grow to fit their content.
    /// </summary>
    public PdfRowHeightRule RowHeightRule { get; set; } = PdfRowHeightRule.Exactly;

    /// <summary>
    /// Gets or sets the vertical alignment of every cell.
    /// </summary>
    public PdfCellVerticalAlignment VerticalAlignment { get; set; } = PdfCellVerticalAlignment.Middle;

    #endregion
}
