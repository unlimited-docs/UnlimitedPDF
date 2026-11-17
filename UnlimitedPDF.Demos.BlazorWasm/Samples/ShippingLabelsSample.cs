using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Shipping labels: address labels on A4 sheets of three columns by seven rows, each with the sender, the recipient,
/// and a tracking number, outlined for cutting.
/// </summary>
public static class ShippingLabelsSample
{
    #region Fields and Constants

    private const int Columns = 3;
    private const int Rows = 7;

    private static readonly string[] Cities = ["Hyderabad 500001", "Chennai 600002", "Pune 411001", "Mumbai 400001", "Delhi 110001", "Kolkata 700001", "Jaipur 302001"];
    private static readonly string[] Names = ["Anita Rao", "Vikram Iyer", "Meera Nair", "Rohan Gupta", "Sana Khan", "Arjun Das", "Kavya Menon"];
    private static readonly string[] Streets = ["14 Lake View Road", "221 Park Street", "9 Station Lane", "77 Hill Avenue", "3 Temple Road", "58 River Side"];

    private static readonly PdfColor _outline = new(0.75, 0.78, 0.83);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the label sheets.
    /// </summary>
    /// <param name="input">The sender and how many labels (kept between 1 and <see cref="ShippingLabelsInput.MaxCount"/>).</param>
    /// <returns>The document, ready to save: one A4 page per 21 labels.</returns>
    public static PdfDocument Build(ShippingLabelsInput input)
    {
        int count = Math.Clamp(input.Count, 1, ShippingLabelsInput.MaxCount);
        var (pageWidth, pageHeight) = PdfPageSize.A4.ToDimensions();
        // The sheet keeps a margin printers can reach: 20 pt at the sides and 40 pt at the top and bottom.
        const double side = 20, edge = 40;
        double width = (pageWidth - 2 * side) / Columns, height = (pageHeight - 2 * edge) / Rows;
        string sender = input.Sender.Length > 48 ? input.Sender[..47] + "…" : input.Sender;

        var document = new PdfDocument();
        PdfPage? page = null;
        for (int i = 0; i < count; i++)
        {
            int slot = i % (Columns * Rows);
            if (slot == 0) page = document.AddPage();

            double x = side + slot % Columns * width;
            double top = pageHeight - edge - slot / Columns * height;
            Outline(page!, x, top - height, width, height);

            page!.AddText(sender, x + 12, top - 18, 6);
            page.AddText(Names[i % Names.Length], x + 12, top - 42, 12);
            page.AddText(Streets[i % Streets.Length], x + 12, top - 58, 9);
            page.AddText(Cities[i % Cities.Length], x + 12, top - 71, 9);
            page.AddText($"Tracking: CR{100000 + i * 37:000000}IN", x + 12, top - 96, 8);
        }

        return document;
    }

    #endregion

    #region Private Methods

    // A thin outline as four narrow rectangles, so the sheet can be cut along it.
    private static void Outline(PdfPage page, double x, double y, double width, double height)
    {
        page.AddRectangle(x, y, width, 0.5, _outline);
        page.AddRectangle(x, y + height - 0.5, width, 0.5, _outline);
        page.AddRectangle(x, y, 0.5, height, _outline);
        page.AddRectangle(x + width - 0.5, y, 0.5, height, _outline);
    }

    #endregion

    #endregion
}
