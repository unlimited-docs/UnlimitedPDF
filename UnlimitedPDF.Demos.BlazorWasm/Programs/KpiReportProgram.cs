// KPI report: headline figures and a bar chart of monthly revenue, drawn from rectangles.
// A complete console program: it writes kpi-report.pdf.
//
// Run it:
//   1. dotnet new console -n KpiReportExample
//   2. cd KpiReportExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open kpi-report.pdf in the KpiReportExample folder.

using UnlimitedPDF;

namespace KpiReportExample;

public static class Program
{
    private static readonly string[] MonthNames =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly PdfColor Bar = new(0.0, 0.42, 0.55);
    private static readonly PdfColor Grid = new(0.85, 0.87, 0.91);
    private static readonly PdfColor Navy = new(0.0, 0.16, 0.45);
    private static readonly PdfColor Tile = new(0.93, 0.95, 0.98);

    public static void Main()
    {
        // The sample data: change it, run again, and open kpi-report.pdf.
        string company = "Contoso Retail";
        string period = "Financial year 2025-26";
        // The monthly revenue in lakh rupees, one bar each, January first (at most 12 are drawn).
        decimal[] monthlyRevenue = [42, 45, 51, 48, 56, 61, 58, 64, 70, 67, 74, 82];

        var months = monthlyRevenue.Take(12).Select(value => Math.Max(0, value)).ToList();
        decimal total = months.Sum();
        decimal best = months.Count == 0 ? 0 : months.Max();
        decimal growth = months.Count >= 2 && months[0] > 0
            ? (months[^1] - months[0]) / months[0] * 100
            : 0;

        var document = new PdfDocument();
        var page = document.AddPage();
        // A light band with a navy rule under it: the text is drawn in black, so the band stays
        // light.
        page.AddRectangle(0, 772, 595, 70, Tile);
        page.AddRectangle(0, 770, 595, 3, Navy);
        page.AddText(company, 50, 806, 20);
        page.AddText($"Key performance indicators, {period}", 50, 784, 11);

        // Four headline figures, each on a tinted tile.
        (string Label, string Value)[] figures =
        [
            ("Revenue (lakh INR)", total.ToString("N0")),
            ("Best month (lakh INR)", best.ToString("N0")),
            ("Growth, first to last month", $"{growth:0.#}%"),
            ("Months reported", months.Count.ToString()),
        ];
        for (int i = 0; i < figures.Length; i++)
        {
            double x = 50 + i * 126;
            page.AddRectangle(x, 660, 116, 80, Tile);
            page.AddText(figures[i].Value, x + 10, 700, 20);
            page.AddText(figures[i].Label, x + 10, 674, 8);
        }

        // The bar chart: a baseline, light grid lines, and one bar per month scaled to the best
        // month.
        page.AddText("Monthly revenue (lakh INR)", 50, 620, 13);
        const double chartLeft = 70, chartBottom = 360, chartHeight = 220, chartWidth = 470;
        for (int line = 0; line <= 4; line++)
        {
            double y = chartBottom + chartHeight * line / 4;
            page.AddRectangle(chartLeft, y, chartWidth, line == 0 ? 1 : 0.5, Grid);
            page.AddText((best * line / 4).ToString("N0"), 40, y - 3, 7);
        }

        double slot = chartWidth / Math.Max(1, months.Count);
        for (int i = 0; i < months.Count; i++)
        {
            double height = best == 0 ? 0 : (double)(months[i] / best) * chartHeight;
            double x = chartLeft + i * slot + slot * 0.2;
            if (height > 0) page.AddRectangle(x, chartBottom, slot * 0.6, height, Bar);
            page.AddText(months[i].ToString("N0"), x, chartBottom + height + 4, 7);
            page.AddText(MonthNames[i], x, chartBottom - 14, 8);
        }

        page.AddText("Figures are fictional demo data.", 50, 60, 8);

        document.Save("kpi-report.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("kpi-report.pdf")}");
    }
}
