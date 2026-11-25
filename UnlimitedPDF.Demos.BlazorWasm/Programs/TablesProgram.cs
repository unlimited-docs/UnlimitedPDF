// Tables: column widths, colors, borders, padding, alignment, and the row height rule.
// A complete console program: it writes tables.pdf.
//
// Run it:
//   1. dotnet new console -n TablesExample
//   2. cd TablesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open tables.pdf in the TablesExample folder.

using System.Globalization;
using UnlimitedPDF;

namespace TablesExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open tables.pdf.
        double column1Width = 220;
        double column2Width = 140;
        double column3Width = 130;
        double rowHeight = 30;
        PdfRowHeightMode rowHeightMode = PdfRowHeightMode.Exactly;
        string headerBackground = "#e8eefc";
        string bodyBackground = "#ffffff";
        string borderColor = "#5b6b88";
        double borderWidth = 0.75;
        double padding = 6;
        PdfCellHorizontalAlignment horizontalAlignment = PdfCellHorizontalAlignment.Left;
        PdfCellVerticalAlignment verticalAlignment = PdfCellVerticalAlignment.Middle;
        string[][] products =
        [
            ["Wireless Keyboard", "Accessories", "1,250.00"],
            ["USB-C Docking Station", "Accessories", "4,500.00"],
            ["27-inch Monitor", "Displays", "18,900.00"],
            ["Ergonomic Laptop Stand", "Accessories", "1,100.00"],
            ["Noise-cancelling Headset", "Audio", "6,499.00"],
        ];

        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Product price list", 50, 790, 18);

        // AddTable takes the table's top-left corner (y is the top edge) and its total width.
        double width = column1Width + column2Width + column3Width;
        var table = page.AddTable(50, 760, width);
        table.SetColumnWidths(column1Width, column2Width, column3Width);
        table.RowHeight = rowHeight;
        table.RowHeightMode = rowHeightMode;

        var header = table.AddRow();
        string[] headings = ["Product", "Category", "Price (INR)"];
        for (int column = 0; column < headings.Length; column++)
        {
            var cell = header.GetCell(column);
            cell.Text = headings[column];
            Style(cell, ToPdfColor(headerBackground));
        }

        foreach (var product in products)
        {
            var row = table.AddRow();
            for (int column = 0; column < product.Length; column++)
            {
                var cell = row.GetCell(column);
                cell.Text = product[column];
                Style(cell, ToPdfColor(bodyBackground));
                cell.HorizontalAlignment = horizontalAlignment;
            }
        }

        document.Save("tables.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("tables.pdf")}");

        // Gives a cell its background, padding, vertical alignment, and four borders.
        void Style(PdfCell cell, PdfColor background)
        {
            cell.BackgroundColor = background;
            cell.Padding = padding;
            cell.VerticalAlignment = verticalAlignment;

            var color = ToPdfColor(borderColor);
            var borders = new[]
            {
                cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom,
            };
            foreach (var border in borders)
            {
                border.Color = color;
                border.Width = borderWidth;
            }
        }
    }

    // Turns a CSS hex color such as "#2357e8" into a PdfColor with 0 to 1 components.
    private static PdfColor ToPdfColor(string hex)
    {
        var value = int.Parse(
            hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new PdfColor(
            ((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
    }
}
