using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// KPI report: one page with four headline figures and a bar chart of monthly revenue, the bars drawn as rectangles
/// scaled to the largest month.
/// </summary>
public static class KpiReportSample
{
    #region Fields and Constants

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly PdfColor _bar = new(0.0, 0.42, 0.55);
    private static readonly PdfColor _grid = new(0.85, 0.87, 0.91);
    private static readonly PdfColor _navy = new(0.0, 0.16, 0.45);
    private static readonly PdfColor _tile = new(0.93, 0.95, 0.98);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="input">The company, the period, and the monthly revenue (at most 12 months are drawn).</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(KpiReportInput input)
    {
        var months = input.MonthlyRevenue.Take(12).Select(value => Math.Max(0, value)).ToList();
        decimal total = months.Sum();
        decimal best = months.Count == 0 ? 0 : months.Max();
        decimal growth = months.Count >= 2 && months[0] > 0 ? (months[^1] - months[0]) / months[0] * 100 : 0;

        var document = new PdfDocument();
        var page = document.AddPage();
        // A light band with a navy rule under it: the text is drawn in black, so the band stays light.
        page.AddRectangle(0, 772, 595, 70, _tile);
        page.AddRectangle(0, 770, 595, 3, _navy);
        page.AddText(input.Company, 50, 806, 20);
        page.AddText($"Key performance indicators, {input.Period}", 50, 784, 11);

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
            page.AddRectangle(x, 660, 116, 80, _tile);
            page.AddText(figures[i].Value, x + 10, 700, 20);
            page.AddText(figures[i].Label, x + 10, 674, 8);
        }

        // The bar chart: a baseline, light grid lines, and one bar per month scaled to the best month.
        page.AddText("Monthly revenue (lakh INR)", 50, 620, 13);
        const double chartLeft = 70, chartBottom = 360, chartHeight = 220, chartWidth = 470;
        for (int line = 0; line <= 4; line++)
        {
            double y = chartBottom + chartHeight * line / 4;
            page.AddRectangle(chartLeft, y, chartWidth, line == 0 ? 1 : 0.5, _grid);
            page.AddText((best * line / 4).ToString("N0"), 40, y - 3, 7);
        }

        double slot = chartWidth / Math.Max(1, months.Count);
        for (int i = 0; i < months.Count; i++)
        {
            double height = best == 0 ? 0 : (double)(months[i] / best) * chartHeight;
            double x = chartLeft + i * slot + slot * 0.2;
            if (height > 0) page.AddRectangle(x, chartBottom, slot * 0.6, height, _bar);
            page.AddText(months[i].ToString("N0"), x, chartBottom + height + 4, 7);
            page.AddText(Months[i], x, chartBottom - 14, 8);
        }

        page.AddText("Figures are fictional demo data.", 50, 60, 8);
        return document;
    }

    #endregion

    #endregion
}
