namespace UnlimitedPDF.Tests;

/// <summary>
/// Page-coordinate text (WR-003, WR-023): <see cref="PdfPage.AddText"/> takes lengths in points as <c>double</c> and
/// writes them as invariant PDF numbers, so whole-number calls keep their exact bytes.
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
