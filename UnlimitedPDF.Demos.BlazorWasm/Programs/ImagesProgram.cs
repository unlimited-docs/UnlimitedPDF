// Images: a fit mode in a box, a size set by one dimension, and an image in a table cell.
// A complete console program: it writes images.pdf.
//
// Run it:
//   1. dotnet new console -n ImagesExample
//   2. cd ImagesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the ImagesExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/dock.jpg
//   6. dotnet run
// Then open images.pdf in the ImagesExample folder.

using UnlimitedPDF;

namespace ImagesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open images.pdf.
        string imageFile = "dock.jpg";
        PdfImageFit fit = PdfImageFit.Contain;
        double boxWidth = 300;
        double boxHeight = 200;
        double x = 50;
        double y = 500;
        bool showInTableCell = true;

        // The image is loaded once and is reusable in any document.
        var image = PdfImage.FromFile(imageFile);

        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Images", 50, 795, 20);
        page.AddText($"Source image: {image.PixelWidth} x {image.PixelHeight} px", 50, 775, 10);

        // 1. Fit inside a box, outlined so the fit is visible.
        page.AddText(
            $"{fit} in a {boxWidth:0} x {boxHeight:0} pt box",
            (int)x,
            (int)(y + boxHeight + 10),
            11);

        // Images are placed by their bottom-left corner; Contain keeps the aspect ratio, Stretch
        // fills the box.
        page.AddImage(image, x, y, boxWidth, boxHeight, new PdfImagePlacementOptions { Fit = fit });
        Outline(page, x, y, boxWidth, boxHeight);

        // 2. One dimension only: the other follows the image's aspect ratio. PdfUnits converts
        // other units to points.
        page.AddText("Width only: 1.5 in", 400, 440, 11);
        page.AddImage(image, 400, 330, width: PdfUnits.FromInches(1.5));

        // 3. An image stacked above its caption inside a table cell; the row grows to fit.
        if (showInTableCell)
        {
            page.AddText("Inside a table cell", 50, 300, 11);
            var table = page.AddTable(50, 290, 360);
            table.SetColumnWidths(140, 220);
            var row = table.AddRow();
            row.HeightMode = PdfRowHeightMode.AtLeast;

            var imageCell = row.GetCell(0);
            imageCell.SetContent(image, "Caption under the image");
            imageCell.HorizontalAlignment = PdfCellHorizontalAlignment.Center;

            var textCell = row.GetCell(1);
            textCell.Text = "Cells hold text and images in order.";
            textCell.VerticalAlignment = PdfCellVerticalAlignment.Top;
            textCell.BackgroundColor = new PdfColor(0.96, 0.97, 1.0);
        }

        document.Save("images.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("images.pdf")}");
    }

    // Draws a thin outline on the box's edges with four filled rectangles.
    private static void Outline(PdfPage page, double x, double y, double width, double height)
    {
        const double thickness = 0.5;
        page.AddRectangle(x, y, width, thickness, PdfColor.Black);
        page.AddRectangle(x, y + height - thickness, width, thickness, PdfColor.Black);
        page.AddRectangle(x, y, thickness, height, PdfColor.Black);
        page.AddRectangle(x + width - thickness, y, thickness, height, PdfColor.Black);
    }
}
