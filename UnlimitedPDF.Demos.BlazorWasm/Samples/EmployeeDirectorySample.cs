using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Employee directory: one long table that continues across pages by itself and repeats its header row on every
/// page.
/// </summary>
public static class EmployeeDirectorySample
{
    #region Fields and Constants

    private const double TableLeft = 40;
    private const double TableWidth = 515;

    private static readonly string[] _departments = ["Engineering", "Finance", "Sales", "Support", "Marketing", "Operations"];
    private static readonly string[] _firstNames = ["Aarav", "Diya", "Kabir", "Meera", "Rohan", "Sara", "Vikram", "Anika", "Dev", "Isha", "Arjun", "Nina"];
    private static readonly PdfColor _headerTint = new(0.85, 0.90, 0.96);
    private static readonly string[] _lastNames = ["Rao", "Sharma", "Iyer", "Khan", "Patel", "Das", "Menon", "Gupta", "Nair"];
    private static readonly string[] _locations = ["Hyderabad", "Bengaluru", "Pune", "Chennai", "Remote"];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The directory options.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(EmployeeDirectoryInput input)
    {
        // Margins apply to every page: tables break above the bottom one and continue below the top one.
        var document = new PdfDocument
        {
            TopMargin = Math.Max(0, input.TopMargin),
            BottomMargin = Math.Max(0, input.BottomMargin)
        };
        var page = document.AddPage();

        page.AddText(PdfText.Escape(input.CompanyName), (int)TableLeft, 800, 18);
        page.AddText("Employee directory", (int)TableLeft, 780, 12);

        var table = page.AddTable(TableLeft, 760, TableWidth);
        table.SetColumnWidths(45, 160, 120, 110, 80);
        table.RowHeight = 18;

        // The first row is the header; it repeats at the top of each new page when RepeatHeaderRows is on.
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = input.RepeatHeader;

        var header = table.AddRow();
        string[] headings = ["No.", "Name", "Department", "Location", "Extension"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).Background = _headerTint;
        }

        int count = Math.Clamp(input.EmployeeCount, 1, 1000);
        for (int i = 0; i < count; i++)
        {
            var row = table.AddRow();
            row.GetCell(0).Text = (i + 1).ToString();
            row.GetCell(1).Text = $"{_firstNames[i % _firstNames.Length]} {_lastNames[i / _firstNames.Length % _lastNames.Length]}";
            row.GetCell(2).Text = _departments[i % _departments.Length];
            row.GetCell(3).Text = _locations[i % _locations.Length];
            row.GetCell(4).Text = (2000 + i).ToString();
        }

        return document;
    }

    #endregion

    #endregion
}
