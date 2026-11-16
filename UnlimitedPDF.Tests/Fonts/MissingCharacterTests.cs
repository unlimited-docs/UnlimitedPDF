namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Characters no font can draw (D-015, A06, A10, WR-039): <see cref="PdfDocument.FindMissingCharacters"/> lists them,
/// they are drawn as '?' by default, and <see cref="PdfDocument.FailOnMissingCharacters"/> makes saving fail with
/// <see cref="PdfTextException"/> before anything is written.
/// </summary>
public class MissingCharacterTests
{
    #region Fields and Constants

    private const string Praying = "\U0001F64F";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void The_query_lists_each_missing_character_once_in_order()
    {
        var document = new PdfDocument();

        Assert.Equal(new[] { Praying, "త" }, document.FindMissingCharacters("Thank you " + Praying + " త " + Praying));
        Assert.Empty(document.FindMissingCharacters("Café – 5 € “quoted”")); // WinAnsi draws
        Assert.Empty(document.FindMissingCharacters(string.Empty));
    }

    [Fact]
    public void The_query_counts_the_call_font_and_the_fallback_fonts()
    {
        var telugu = Font("తె");
        var emoji = Font(Praying, family: "Emoji Test");
        var document = new PdfDocument();

        Assert.Equal(new[] { "త", Praying }, document.FindMissingCharacters("త " + Praying));
        Assert.Equal(new[] { Praying }, document.FindMissingCharacters("త " + Praying, telugu));
        document.FontFallback.Add(emoji);
        Assert.Empty(document.FindMissingCharacters("త " + Praying, telugu));
    }

    [Fact]
    public void A_cluster_is_missing_when_no_single_font_has_all_of_it()
    {
        // TA plus vowel sign E is one text element; a font with only TA cannot draw it.
        var onlyTa = Font("త");

        Assert.Equal(new[] { "తె" }, new PdfDocument().FindMissingCharacters("తె", onlyTa));
    }

    [Fact]
    public void Null_text_is_rejected()
    {
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new PdfDocument().FindMissingCharacters(null!)).ParamName);
    }

    [Fact]
    public void By_default_a_missing_character_is_drawn_as_a_question_mark()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Thank you " + Praying, 72, 700);

        var pdf = PdfPageReader.SaveBytes(document);

        Assert.Equal("Thank you ?", Assert.Single(PdfInspector.Read(pdf).PageText()));
        Assert.False(document.FailOnMissingCharacters);
    }

    public static IEnumerable<object[]> EntryPoints() => new[] { "page", "header", "flow", "block", "cell", "font" }.Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Strict_saving_fails_before_writing_anything(string entryPoint)
    {
        var document = new PdfDocument { FailOnMissingCharacters = true };
        string text = "Thank you " + Praying;
        switch (entryPoint)
        {
            case "page": document.AddPage().AddText(text, 72, 700); break;
            case "header": document.Header.Height = 30; document.Header.AddText(text, 0, 0); document.AddPage(); break;
            case "flow": document.Body.AddText(text); break;
            case "block": document.Body.AddBlock().AddText(text); break;
            case "cell": document.AddPage().AddTable(50, 700, 300).SetText(0, 0, text); break;
            case "font": document.AddPage().AddText(text, Font("Thank you"), 72, 700); break;
        }
        using var output = new MemoryStream();

        var error = Assert.Throws<PdfTextException>(() => document.Save(output));

        Assert.Equal(Praying, error.Character);
        Assert.Equal(text, error.Text);
        Assert.Equal(1, error.PageNumber);
        Assert.Contains("U+1F64F", error.Message);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void Strict_saving_names_the_page_and_leaves_no_file()
    {
        var document = new PdfDocument { FailOnMissingCharacters = true };
        document.AddPage().AddText("fine", 72, 700);
        document.AddPage().AddText("త", 72, 700);
        string path = Path.Combine(Path.GetTempPath(), "unlimitedpdf-missing-" + Guid.NewGuid().ToString("N") + ".pdf");

        var error = Assert.Throws<PdfTextException>(() => document.Save(path));

        Assert.Equal(2, error.PageNumber);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Strict_async_saving_fails_the_same_way()
    {
        var document = new PdfDocument { FailOnMissingCharacters = true };
        document.AddPage().AddText(Praying, 72, 700);
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<PdfTextException>(() => document.SaveAsync(output));

        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void Strict_saving_passes_when_every_character_can_be_drawn()
    {
        var document = new PdfDocument { FailOnMissingCharacters = true };
        document.FontFallback.Add(Font(Praying));
        document.AddPage().AddText("Café " + Praying, 72, 700);

        var pdf = PdfPageReader.SaveBytes(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal("Café " + Praying, Assert.Single(PdfInspector.Read(pdf).PageText()));
    }

    #endregion

    #region Private Methods

    private static PdfFont Font(string characters, string family = "Test Sans")
    {
        var builder = new TestFontBuilder(family);
        builder.MapCharacters(characters);
        return PdfFont.FromBytes(builder.Build());
    }

    #endregion

    #endregion
}
