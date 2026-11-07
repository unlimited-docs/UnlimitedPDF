namespace UnlimitedPDF.Tests;

/// <summary>
/// Page-coordinate text (WR-003, WR-023): <see cref="PdfPage.AddText"/> takes lengths in points as <c>double</c> and
/// writes them as invariant PDF numbers, so whole-number calls keep their exact bytes. Invalid numbers and null text
/// fail at the call, as they do for band text (A10).
/// </summary>
public class PageTextTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Fractional_page_text_is_written_with_invariant_numbers()
    {
        using var culture = new CultureScope("de-DE");
        var document = new PdfDocument();
        document.AddPage().AddText("x", 50.5, 780.25, 9.5);

        var content = PdfPageReader.Save(document).Single().Content;

        Assert.Contains("BT /F1 9.5 Tf 50.5 780.25 Td (x) Tj ET", content);
    }

    [Theory]
    [InlineData(double.NaN, 780, 12, "x")]
    [InlineData(double.PositiveInfinity, 780, 12, "x")]
    [InlineData(double.NegativeInfinity, 780, 12, "x")]
    [InlineData(50, double.NaN, 12, "y")]
    [InlineData(50, double.PositiveInfinity, 12, "y")]
    [InlineData(50, double.NegativeInfinity, 12, "y")]
    [InlineData(50, 780, double.NaN, "fontSize")]
    [InlineData(50, 780, double.PositiveInfinity, "fontSize")]
    [InlineData(50, 780, 0, "fontSize")]
    [InlineData(50, 780, -5, "fontSize")]
    public void Invalid_page_text_numbers_fail_at_the_call(double x, double y, double fontSize, string paramName)
    {
        var page = new PdfDocument().AddPage();

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => page.AddText("x", x, y, fontSize));

        Assert.Equal(paramName, error.ParamName);
    }

    [Fact]
    public void Null_page_text_fails_at_the_call()
    {
        var page = new PdfDocument().AddPage();

        var error = Assert.Throws<ArgumentNullException>(() => page.AddText(null!, 50, 780));

        Assert.Equal("text", error.ParamName);
    }

    [Fact]
    public void Whole_number_page_text_keeps_its_bytes()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("x", 50, 780, 20);
        page.AddText("y", 50, 760);

        var content = PdfPageReader.Save(document).Single().Content;

        Assert.Contains("BT /F1 20 Tf 50 780 Td (x) Tj ET", content);
        Assert.Contains("BT /F1 12 Tf 50 760 Td (y) Tj ET", content);
    }

    #endregion

    #endregion
}
