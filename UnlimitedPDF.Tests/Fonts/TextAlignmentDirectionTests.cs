using System.Globalization;
using System.Text.RegularExpressions;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// <c>Start</c> and <c>End</c> alignment (WR-041; V-09): <c>Start</c> is every default and lands right-to-left text at
/// the right edge and left-to-right text at the left edge, byte for byte as <c>Left</c> did; <c>End</c> is the other
/// edge; offsets are measured from the aligned edge; <c>Left</c>, <c>Center</c>, and <c>Right</c> stay physical; table
/// cells follow the same rule.
/// </summary>
public class TextAlignmentDirectionTests
{
    #region Fields and Constants

    private const string Hebrew = "שלום";
    private const string English = "Hello";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void The_defaults_are_start()
    {
        Assert.Equal(PdfCellHorizontalAlignment.Start, new PdfCell().HorizontalAlignment);
        foreach (var method in new[] { typeof(PdfBodyFlow), typeof(PdfFlowBlock), typeof(PdfSectionContent) }.SelectMany(t => t.GetMethods()).Where(m => m.Name == "AddText"))
        {
            var parameter = method.GetParameters().Single(p => p.Name == "alignment");
            Assert.Equal(PdfTextAlignment.Start, parameter.DefaultValue);
        }
        Assert.Equal(new[] { "Left", "Center", "Right", "Start", "End" }, Enum.GetNames(typeof(PdfTextAlignment)));
        Assert.Equal(new[] { "Left", "Center", "Right", "Start", "End" }, Enum.GetNames(typeof(PdfCellHorizontalAlignment)));
    }

    [Fact]
    public void Left_to_right_text_with_start_draws_byte_for_byte_as_left()
    {
        var font = Font();
        Assert.Equal(Flow(English, font, PdfTextAlignment.Left, 10), Flow(English, font, PdfTextAlignment.Start, 10));
        Assert.Equal(Flow(English, font, PdfTextAlignment.Right, 10), Flow(English, font, PdfTextAlignment.End, 10));
        Assert.Equal(Flow(English, null, PdfTextAlignment.Left, 10), Flow(English, null, PdfTextAlignment.Start, 10));
    }

    [Fact]
    public void Right_to_left_text_with_start_lands_at_the_right_edge_and_end_at_the_left()
    {
        var font = Font();
        Assert.Equal(X(Flow(Hebrew, font, PdfTextAlignment.Right, 10)), X(Flow(Hebrew, font, PdfTextAlignment.Start, 10)));
        Assert.Equal(X(Flow(Hebrew, font, PdfTextAlignment.Left, 10)), X(Flow(Hebrew, font, PdfTextAlignment.End, 10)));
        Assert.True(X(Flow(Hebrew, font, PdfTextAlignment.Start, 0)) > X(Flow(Hebrew, font, PdfTextAlignment.Left, 0)));
    }

    [Fact]
    public void The_default_alignment_follows_the_line_direction()
    {
        var font = Font();
        var rtl = new PdfDocument();
        rtl.Body.AddText(Hebrew, font);
        var right = new PdfDocument();
        right.Body.AddText(Hebrew, font, alignment: PdfTextAlignment.Right);
        var english = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
        english.Body.AddText(English, font);

        Assert.Equal(X(Content(rtl)), X(Content(right)));
        Assert.Equal(X(Content(right)) - X(Content(rtl)), 0, 3);
        // An English line in a right-to-left document reads from the right too.
        Assert.True(X(Content(english)) > 300);
    }

    [Fact]
    public void Offsets_are_measured_from_the_aligned_edge()
    {
        var font = Font();
        double atEdge = X(Flow(Hebrew, font, PdfTextAlignment.Start, 0));
        double inset = X(Flow(Hebrew, font, PdfTextAlignment.Start, 20));

        Assert.Equal(atEdge - 20, inset, 3);
    }

    [Fact]
    public void Header_text_follows_the_same_rule()
    {
        var font = Font();
        var start = new PdfDocument();
        start.Header.Height = 40;
        start.Header.AddText(Hebrew, font, 0, 0);
        start.AddPage();
        var right = new PdfDocument();
        right.Header.Height = 40;
        right.Header.AddText(Hebrew, font, 0, 0, alignment: PdfTextAlignment.Right);
        right.AddPage();

        Assert.Equal(X(Content(right)), X(Content(start)));
    }

    [Fact]
    public void Table_cells_align_right_to_left_text_at_the_start_side()
    {
        var font = Font();
        string Cell(PdfCellHorizontalAlignment alignment, string text)
        {
            var document = new PdfDocument();
            var table = document.AddPage().AddTable(50, 700, 400);
            var cell = table.GetOrCreateCell(0, 0);
            cell.HorizontalAlignment = alignment;
            table.SetText(0, 0, text, font);
            return Content(document);
        }

        Assert.Equal(X(Cell(PdfCellHorizontalAlignment.Right, Hebrew)), X(Cell(PdfCellHorizontalAlignment.Start, Hebrew)));
        Assert.Equal(X(Cell(PdfCellHorizontalAlignment.Left, Hebrew)), X(Cell(PdfCellHorizontalAlignment.End, Hebrew)));
        Assert.Equal(Cell(PdfCellHorizontalAlignment.Left, English), Cell(PdfCellHorizontalAlignment.Start, English));
    }

    #endregion

    #region Private Methods

    private static PdfFont Font() => RightToLeftScriptTests.Font(Hebrew + English);

    private static string Flow(string text, PdfFont? font, PdfTextAlignment alignment, double x)
    {
        var document = new PdfDocument();
        if (font is null) document.Body.AddText(text, alignment: alignment, x: x);
        else document.Body.AddText(text, font, alignment: alignment, x: x);
        return Content(document);
    }

    private static string Content(PdfDocument document) => Assert.Single(PdfPageReader.Read(PdfPageReader.SaveBytes(document))).Content;

    // The x of the first text matrix (Tm) in the content.
    private static double X(string content)
    {
        var match = Regex.Match(content, @"1 0 (?:-?[\d.]+ )?1 (-?[\d.]+) -?[\d.]+ Tm");
        Assert.True(match.Success, content);
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    #endregion

    #endregion
}
