using System.Globalization;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Tables: column widths, colors, borders, padding, alignment, and the row height rule.
/// </summary>
public static class TablesSample
{
    #region Fields and Constants

    private static readonly string[][] _products =
    [
        ["Wireless Keyboard", "Accessories", "1,250.00"],
        ["USB-C Docking Station", "Accessories", "4,500.00"],
        ["27-inch Monitor", "Displays", "18,900.00"],
        ["Ergonomic Laptop Stand", "Accessories", "1,100.00"],
        ["Noise-cancelling Headset", "Audio", "6,499.00"]
    ];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The demo options.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(TablesInput input)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Product price list", 50, 790, 18);

        // AddTable takes the table's top-left corner (y is the top edge) and its total width.
        double width = input.Column1Width + input.Column2Width + input.Column3Width;
        var table = page.AddTable(50, 760, width);
        table.SetColumnWidths(input.Column1Width, input.Column2Width, input.Column3Width);
        table.RowHeight = input.RowHeight;
        table.RowHeightMode = input.RowHeightMode;

        var header = table.AddRow();
        string[] headings = ["Product", "Category", "Price (INR)"];
        for (int column = 0; column < headings.Length; column++)
        {
            var cell = header.GetCell(column);
            cell.Text = headings[column];
            Style(cell, input, ToPdfColor(input.HeaderBackground));
        }

        foreach (var product in _products)
        {
            var row = table.AddRow();
            for (int column = 0; column < product.Length; column++)
            {
                var cell = row.GetCell(column);
                cell.Text = product[column];
                Style(cell, input, ToPdfColor(input.BodyBackground));
                cell.HorizontalAlignment = input.HorizontalAlignment;
            }
        }

        return document;
    }

    #endregion

    #region Private Methods

    private static void Style(PdfCell cell, TablesInput input, PdfColor background)
    {
        cell.BackgroundColor = background;
        cell.Padding = input.Padding;
        cell.VerticalAlignment = input.VerticalAlignment;

        var borderColor = ToPdfColor(input.BorderColor);
        foreach (var border in new[] { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
        {
            border.Color = borderColor;
            border.Width = input.BorderWidth;
        }
    }

    // Turns a CSS hex color such as "#2357e8" into a PdfColor with 0 to 1 components.
    private static PdfColor ToPdfColor(string hex)
    {
        var value = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new PdfColor(((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
    }

    #endregion

    #endregion
}
