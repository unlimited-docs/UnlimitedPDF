using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Images: a fit mode inside a box, a size set by one dimension, and an image stacked with text in a table cell.
/// </summary>
public static class ImagesSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The demo options.</param>
    /// <param name="image">The image to place, loaded once with PdfImage.FromBytes and reusable in any document.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(ImagesInput input, PdfImage image)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Images", 50, 795, 20);
        page.AddText($"Source image: {image.PixelWidth} x {image.PixelHeight} px", 50, 775, 10);

        // 1. Fit inside a box, outlined so the fit is visible.
        page.AddText($"{input.Fit} in a {input.BoxWidth:0} x {input.BoxHeight:0} pt box", (int)input.X, (int)(input.Y + input.BoxHeight + 10), 11);

        // Images are placed by their bottom-left corner; Contain keeps the aspect ratio, Stretch fills the box.
        page.AddImage(image, input.X, input.Y, input.BoxWidth, input.BoxHeight,
            new PdfImagePlacementOptions { Fit = input.Fit });
        Outline(page, input.X, input.Y, input.BoxWidth, input.BoxHeight);

        // 2. One dimension only: the other follows the image's aspect ratio. PdfUnits converts other units to points.
        page.AddText("Width only: 1.5 in", 400, 440, 11);
        page.AddImage(image, 400, 330, width: PdfUnits.FromInches(1.5));

        // 3. An image stacked above its caption inside a table cell; the row grows to fit.
        if (input.ShowInTableCell)
        {
            page.AddText("Inside a table cell", 50, 300, 11);
            var table = page.AddTable(50, 290, 360);
            table.SetColumnWidths(140, 220);
            var row = table.AddRow();
            row.HeightRule = PdfRowHeightRule.AtLeast;

            var imageCell = row.GetCell(0);
            imageCell.SetContent(image, "Caption under the image");
            imageCell.HAlign = PdfCellHorizontalAlignment.Center;

            var textCell = row.GetCell(1);
            textCell.Text = "Cells hold text and images in order.";
            textCell.VAlign = PdfCellVerticalAlignment.Top;
            textCell.Background = new PdfColor(0.96, 0.97, 1.0);
        }

        return document;
    }

    #endregion

    #region Private Methods

    // Draws a thin outline on the box's edges with four filled rectangles.
    private static void Outline(PdfPageBuilder page, double x, double y, double width, double height)
    {
        const double thickness = 0.5;
        page.AddRectangle(x, y, width, thickness, PdfColor.Black);
        page.AddRectangle(x, y + height - thickness, width, thickness, PdfColor.Black);
        page.AddRectangle(x, y, thickness, height, PdfColor.Black);
        page.AddRectangle(x + width - thickness, y, thickness, height, PdfColor.Black);
    }

    #endregion

    #endregion
}
