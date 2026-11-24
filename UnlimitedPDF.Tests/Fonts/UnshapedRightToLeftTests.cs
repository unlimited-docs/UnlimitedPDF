using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Right-to-left letters of scripts the library does not shape (WR-039; V-11, QF-26112401 F-2): Syriac, N'Ko, and
/// Samaritan are reported by <c>FindMissingCharacters</c>, fail a save with <c>FailOnMissingCharacters</c> before
/// anything is written, and raise the HTML <c>MissingCharacter</c> warning, even when a font has their glyphs; without
/// the flag they are drawn in bidirectional order and copy as typed. Arabic, Hebrew, and Thaana are not reported when a
/// font has them, and are reported without one.
/// </summary>
public class UnshapedRightToLeftTests
{
    #region Fields and Constants

    private const string Syriac = "ܫܠܡܐ";
    private const string Nko = "ߒߞߏ";
    private const string Samaritan = "ࠀࠁࠂ";

    #endregion

    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(Syriac)]
    [InlineData(Nko)]
    [InlineData(Samaritan)]
    public void An_unshaped_script_is_reported_even_when_a_font_has_it(string text)
    {
        var font = RightToLeftScriptTests.Font(text + " ");
        var document = new PdfDocument();

        var missing = document.FindMissingCharacters("Name: " + text, font);

        Assert.Equal(StringInfoElements(text), missing);
    }

    [Theory]
    [InlineData("שלום")]
    [InlineData("سلام")]
    [InlineData("ހެލޯ")]
    public void A_shaped_script_is_reported_only_without_a_font(string text)
    {
        var document = new PdfDocument();

        Assert.Empty(document.FindMissingCharacters(text, RightToLeftScriptTests.Font(text)));
        Assert.Equal(StringInfoElements(text), document.FindMissingCharacters(text));
    }

    [Fact]
    public void Bidirectional_marks_are_not_reported()
    {
        var font = RightToLeftScriptTests.Font("abc");

        Assert.Empty(new PdfDocument().FindMissingCharacters("‏abc‎؜⁧abc⁩", font));
    }

    [Theory]
    [MemberData(nameof(RightToLeftScriptTests.EntryPoints), MemberType = typeof(RightToLeftScriptTests))]
    public void Fail_on_missing_characters_stops_the_save_before_anything_is_written(string entryPoint)
    {
        var font = RightToLeftScriptTests.Font(Syriac + " Name:");
        var document = new PdfDocument { FailOnMissingCharacters = true };
        Add(document, entryPoint, "Name: " + Syriac, font);
        using var stream = new MemoryStream();

        var error = Assert.Throws<PdfTextException>(() => document.Save(stream));

        Assert.Equal("ܫ", error.Character);
        Assert.Equal(1, error.PageNumber);
        Assert.Contains("U+072B", error.Message);
        Assert.Contains("right-to-left script that is not shaped", error.Message);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Without_the_flag_unshaped_text_is_drawn_right_to_left_and_copies_as_typed()
    {
        var font = RightToLeftScriptTests.Font(Syriac + " Name:");
        var pdf = RightToLeftScriptTests.Save("page", "Name: " + Syriac, font);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal("Name: " + Syriac, RightToLeftScriptTests.PageText(pdf));
        Rendering.RenderTestFiles.AssertDrawnWithTheSameText(pdf);
    }

    [Fact]
    public void Html_warns_for_an_unshaped_script_once()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Script Sans", RightToLeftScriptTests.Font(Syriac + " "));
        var html = PdfHtml.FromString($"<p style='font-family: Script Sans'>{Syriac} {Syriac}</p>", options);
        var document = new PdfDocument();
        document.Body.AddHtml(html);

        PdfPageReader.SaveBytes(document);

        var warnings = html.Warnings.Where(w => w.Kind == PdfHtmlWarningKind.MissingCharacter).ToList();
        Assert.Equal(StringInfoElements(Syriac), warnings.Select(w => w.Source));
        Assert.All(warnings, w => Assert.Contains("right-to-left script that is not shaped", w.Message));
    }

    [Fact]
    public void Html_with_fail_on_missing_characters_stops_the_save()
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Script Sans", RightToLeftScriptTests.Font(Syriac + " "));
        var document = new PdfDocument { FailOnMissingCharacters = true };
        document.Body.AddHtml(PdfHtml.FromString($"<p style='font-family: Script Sans'>{Syriac}</p>", options));

        Assert.Equal("ܫ", Assert.Throws<PdfTextException>(() => PdfPageReader.SaveBytes(document)).Character);
    }

    #endregion

    #region Private Methods

    private static string[] StringInfoElements(string text)
    {
        var list = new List<string>();
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext()) list.Add(e.GetTextElement());
        return list.ToArray();
    }

    private static void Add(PdfDocument document, string entryPoint, string text, PdfFont font)
    {
        switch (entryPoint)
        {
            case "page":
                document.AddPage().AddText(text, font, 50, 700);
                break;
            case "header":
                document.Header.Height = 40;
                document.Header.AddText(text, font, 0, 0);
                document.AddPage();
                break;
            case "flow":
                document.Body.AddText(text, font);
                break;
            case "block":
                document.Body.AddBlock().AddText(text, font);
                break;
            case "cell":
                document.AddPage().AddTable(50, 700, 400).SetText(0, 0, text, font);
                break;
        }
    }

    #endregion

    #endregion
}
