using System.Globalization;
using System.Text.RegularExpressions;
using UnlimitedPDF.Tests.Fonts;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Right-to-left tables (WR-041; V-10, V-17): <c>PdfTable.Direction</c>, unset, follows the document's direction, with
/// automatic counting as left to right; a right-to-left table draws column 0 at its right edge with its width, each
/// later column to the left; cell text reads in the table's direction unless the text sets its own; left-to-right
/// tables draw as before.
/// </summary>
public class TableDirectionTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_right_to_left_document_lays_table_columns_out_from_the_right()
    {
        var document = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
        AddTable(document, direction: null);

        // Columns 100, 50, and 250 wide in a 400-point table at x = 50: column 0 at 350, column 1 at 300, column 2 at 50.
        Assert.Equal(new[] { 350.0, 300.0, 50.0 }, CellLefts(document).Take(3));
        Assert.Equal(new[] { 100.0, 50.0, 250.0 }, CellWidths(document).Take(3));
    }

    [Fact]
    public void Automatic_and_left_to_right_tables_lay_columns_out_from_the_left()
    {
        var automatic = new PdfDocument();
        AddTable(automatic, direction: null);
        var explicitAuto = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
        AddTable(explicitAuto, direction: PdfTextDirection.Auto);
        var leftToRight = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
        AddTable(leftToRight, direction: PdfTextDirection.LeftToRight);

        Assert.Equal(new[] { 50.0, 150.0, 200.0 }, CellLefts(automatic).Take(3));
        Assert.Equal(new[] { 50.0, 150.0, 200.0 }, CellLefts(explicitAuto).Take(3));
        Assert.Equal(new[] { 50.0, 150.0, 200.0 }, CellLefts(leftToRight).Take(3));
    }

    [Fact]
    public void A_table_direction_overrides_the_document()
    {
        var document = new PdfDocument();
        AddTable(document, direction: PdfTextDirection.RightToLeft);

        Assert.Equal(new[] { 350.0, 300.0, 50.0 }, CellLefts(document).Take(3));
    }

    [Fact]
    public void Cell_text_reads_in_the_table_direction_unless_it_sets_its_own()
    {
        var font = RightToLeftScriptTests.Font("iPhone 15 מחיר");
        string Content(PdfTextDirection? textDirection)
        {
            var document = new PdfDocument();
            var table = document.AddPage().AddTable(50, 700, 400);
            table.Direction = PdfTextDirection.RightToLeft;
            table.GetOrCreateCell(0, 0).SetText(new PdfCellText("iPhone 15 מחיר", font) { Direction = textDirection });
            return Assert.Single(PdfPageReader.Read(PdfPageReader.SaveBytes(document))).Content;
        }

        Assert.Equal("<000D>", FirstGlyph(Content(null))); // the table's right to left: resh drawn first
        Assert.Equal("<0001>", FirstGlyph(Content(PdfTextDirection.LeftToRight))); // the text's own direction
    }

    [Fact]
    public void Left_to_right_tables_draw_as_before()
    {
        var before = new PdfDocument();
        AddTable(before, direction: null);
        var explicitLeft = new PdfDocument();
        AddTable(explicitLeft, direction: PdfTextDirection.LeftToRight);

        Assert.Equal(PdfPageReader.Read(PdfPageReader.SaveBytes(before))[0].Content, PdfPageReader.Read(PdfPageReader.SaveBytes(explicitLeft))[0].Content);
    }

    [Fact]
    public void The_direction_defaults_to_null_and_rejects_undefined_values()
    {
        var table = new PdfDocument().AddPage().AddTable(0, 0, 100);

        Assert.Null(table.Direction);
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Direction = (PdfTextDirection)5);
        table.Direction = PdfTextDirection.RightToLeft;
        table.Direction = null;
        Assert.Null(table.Direction);
    }

    #endregion

    #region Private Methods

    private static void AddTable(PdfDocument document, PdfTextDirection? direction)
    {
        var table = document.AddPage().AddTable(50, 700, 400);
        table.Direction = direction;
        table.EnsureSize(1, 3);
        table.SetColumnWidths(100, 50, 250);
        table.SetText(0, 0, "Item");
        table.SetText(0, 1, "Qty");
        table.SetText(0, 2, "Price");
    }

    private static IEnumerable<double> CellLefts(PdfDocument document) => Rectangles(document).Select(r => r.X);

    private static IEnumerable<double> CellWidths(PdfDocument document) => Rectangles(document).Select(r => r.Width);

    // The filled cell backgrounds ("x y w h re f"), in drawing order.
    private static IEnumerable<(double X, double Width)> Rectangles(PdfDocument document)
    {
        string content = Assert.Single(PdfPageReader.Read(PdfPageReader.SaveBytes(document))).Content;
        foreach (Match m in Regex.Matches(content, @"(-?[\d.]+) -?[\d.]+ (-?[\d.]+) -?[\d.]+ re f"))
            yield return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private static string FirstGlyph(string content) => Regex.Match(content, "<[0-9A-F]{4}>").Value;

    #endregion

    #endregion
}
