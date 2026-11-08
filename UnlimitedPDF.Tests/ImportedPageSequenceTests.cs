namespace UnlimitedPDF.Tests;

/// <summary>
/// Imported pages count in one page sequence, get no header, footer, or page settings, and break the body flow like
/// <c>AddPage</c> (WR-036, AC-04, AC-05).
/// </summary>
public class ImportedPageSequenceTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Flow_after_an_imported_page_starts_on_a_new_page()
    {
        var document = new PdfDocument();
        document.Body.AddText("Before");
        document.AddPages(Input(1));
        document.Body.AddText("After");

        var output = ImportedPagesTests.Read(ImportedPagesTests.Save(document));

        Assert.Equal(new[] { "Before", "Page 1", "After" }, ImportedPagesTests.PageTexts(output));
    }

    [Fact]
    public void Flow_before_an_imported_page_finishes_its_overflow_pages_first()
    {
        var document = new PdfDocument();
        for (int i = 1; i <= 90; i++) document.Body.AddText($"Line {i}", spacing: 4);
        document.AddPages(Input(1));

        var texts = ImportedPagesTests.PageTexts(ImportedPagesTests.Read(ImportedPagesTests.Save(document)));

        Assert.True(texts.Length >= 3, "The flow overflows onto a second page.");
        Assert.Contains("Line 90", texts[texts.Length - 2], StringComparison.Ordinal);
        Assert.Equal("Page 1", texts[texts.Length - 1]);
    }

    [Fact]
    public void Imported_pages_count_in_page_numbers_and_get_no_footer()
    {
        var document = new PdfDocument();
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 0);
        document.AddPage().AddText("Cover", 72, 72);
        document.AddPages(Input(2));
        document.AddPage().AddText("Back", 72, 72);

        var texts = ImportedPagesTests.PageTexts(ImportedPagesTests.Read(ImportedPagesTests.Save(document)));

        Assert.Equal(4, texts.Length);
        Assert.Contains("Page 1 of 4", texts[0], StringComparison.Ordinal);
        Assert.Equal("Page 1", texts[1]);
        Assert.Equal("Page 2", texts[2]);
        Assert.Contains("Page 4 of 4", texts[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Page_settings_target_the_saved_position_and_skip_imported_pages()
    {
        var document = new PdfDocument();
        document.Footer.Height = 20;
        document.Footer.AddText("Footer {page}", 0, 0);
        document.AddPage().AddText("One", 72, 72);
        document.AddPages(Input(1));
        document.AddPage().AddText("Three", 72, 72);
        document.AddPageSettings(2).Footer.AddText("Never drawn", 0, 0);
        document.AddPageSettings(3).Footer.IsHidden = true;

        var texts = ImportedPagesTests.PageTexts(ImportedPagesTests.Read(ImportedPagesTests.Save(document)));

        Assert.Contains("Footer 1", texts[0], StringComparison.Ordinal);
        Assert.Equal("Page 1", texts[1]);
        Assert.DoesNotContain("Footer", texts[2], StringComparison.Ordinal);
        Assert.DoesNotContain(texts, text => text.Contains("Never drawn", StringComparison.Ordinal));
    }

    [Fact]
    public void Remaining_height_after_an_imported_page_is_a_fresh_page()
    {
        var document = new PdfDocument();
        document.AddPages(Input(1));

        double remaining = document.Body.RemainingHeight;
        document.Body.AddText("Next");

        Assert.Equal(new PdfDocument().Body.RemainingHeight, remaining);
        Assert.Equal(new[] { "Page 1", "Next" }, ImportedPagesTests.PageTexts(ImportedPagesTests.Read(ImportedPagesTests.Save(document))));
    }

    [Fact]
    public void Imported_page_alone_needs_no_body_room()
    {
        // Settings that leave no body would fail a built page, but an imported page takes no flow.
        var document = new PdfDocument();
        document.Header.Height = 500;
        document.Footer.Height = 400;
        document.AddPages(Input(1));

        Assert.Single(ImportedPagesTests.Read(ImportedPagesTests.Save(document)).Pages);
    }

    #endregion

    #region Private Methods

    private static PdfFile Input(int pages) => PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(pages));

    #endregion

    #endregion
}
