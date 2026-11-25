// Shipping labels: address labels on A4 sheets of three by seven, outlined for cutting.
// A complete console program: it writes shipping-labels.pdf.
//
// Run it:
//   1. dotnet new console -n ShippingLabelsExample
//   2. cd ShippingLabelsExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open shipping-labels.pdf in the ShippingLabelsExample folder.

using UnlimitedPDF;

namespace ShippingLabelsExample;

public static class Program
{
    private const int Columns = 3;
    private const int Rows = 7;

    private static readonly PdfColor OutlineColor = new(0.75, 0.78, 0.83);

    public static void Main()
    {
        // The sample data: change it, run again, and open shipping-labels.pdf.
        // How many labels to print (each A4 sheet holds 21), and the sender line on every label.
        int labelCount = 21;
        string senderLine = "From: Contoso Retail, 12 MG Road, Bengaluru 560001";
        string[] names =
            ["Anita Rao", "Vikram Iyer", "Meera Nair", "Rohan Gupta", "Sana Khan", "Arjun Das",
             "Kavya Menon"];
        string[] streets =
            ["14 Lake View Road", "221 Park Street", "9 Station Lane", "77 Hill Avenue",
             "3 Temple Road", "58 River Side"];
        string[] cities =
            ["Hyderabad 500001", "Chennai 600002", "Pune 411001", "Mumbai 400001", "Delhi 110001",
             "Kolkata 700001", "Jaipur 302001"];

        int count = Math.Clamp(labelCount, 1, 210);
        var (pageWidth, pageHeight) = PdfPageSize.A4.ToDimensions();
        // The sheet keeps a margin printers can reach: 20 pt at the sides and 40 pt at the top
        // and bottom.
        const double side = 20, edge = 40;
        double width = (pageWidth - 2 * side) / Columns, height = (pageHeight - 2 * edge) / Rows;
        string sender = senderLine.Length > 48 ? senderLine[..47] + "…" : senderLine;

        var document = new PdfDocument();
        PdfPage? page = null;
        for (int i = 0; i < count; i++)
        {
            int slot = i % (Columns * Rows);
            if (slot == 0)
            {
                page = document.AddPage();
            }

            double x = side + slot % Columns * width;
            double top = pageHeight - edge - slot / Columns * height;
            Outline(page!, x, top - height, width, height);

            page!.AddText(sender, x + 12, top - 18, 6);
            page.AddText(names[i % names.Length], x + 12, top - 42, 12);
            page.AddText(streets[i % streets.Length], x + 12, top - 58, 9);
            page.AddText(cities[i % cities.Length], x + 12, top - 71, 9);
            page.AddText($"Tracking: CR{100000 + i * 37:000000}IN", x + 12, top - 96, 8);
        }

        document.Save("shipping-labels.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("shipping-labels.pdf")}");
    }

    // A thin outline as four narrow rectangles, so the sheet can be cut along it.
    private static void Outline(PdfPage page, double x, double y, double width, double height)
    {
        page.AddRectangle(x, y, width, 0.5, OutlineColor);
        page.AddRectangle(x, y + height - 0.5, width, 0.5, OutlineColor);
        page.AddRectangle(x, y, 0.5, height, OutlineColor);
        page.AddRectangle(x + width - 0.5, y, 0.5, height, OutlineColor);
    }
}
