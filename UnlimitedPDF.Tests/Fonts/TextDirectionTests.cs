using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The text direction API (WR-041; V-09, V-17): <c>PdfDocument.TextDirection</c> sets the base direction of page,
/// header and footer, flow, flow block, and table cell text; <c>PdfTextOptions</c> on a text call and
/// <c>PdfCellText.Direction</c> override it; automatic takes the first strong character; existing calls keep compiling
/// and drawing as before; undefined values and null options fail fast.
/// </summary>
public class TextDirectionTests
{
    #region Fields and Constants

    // A Hebrew line that starts with a Latin word: automatic reads it left to right, a right-to-left document from the
    // right. Glyphs: i = 1, P, h, o, n, e, space, 1, 5, then mem = 10, het, yod, resh = 13.
    private const string Line = "iPhone 15 מחיר";
    private const string FirstLatin = "<0001>";
    private const string LastHebrew = "<000D>";

    #endregion

    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> EntryPoints() => RightToLeftScriptTests.EntryPoints();

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Automatic_reads_from_the_first_strong_character(string entryPoint)
    {
        Assert.Equal(FirstLatin, FirstGlyph(Save(entryPoint, new PdfDocument(), null)));
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void A_right_to_left_document_reads_a_line_that_starts_with_latin_from_the_right(string entryPoint)
    {
        var pdf = Save(entryPoint, new PdfDocument { TextDirection = PdfTextDirection.RightToLeft }, null);

        Assert.Equal(LastHebrew, FirstGlyph(pdf));
        Assert.Equal(Line, RightToLeftScriptTests.PageText(pdf));
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void The_call_direction_overrides_the_document(string entryPoint)
    {
        Assert.Equal(LastHebrew, FirstGlyph(Save(entryPoint, new PdfDocument(), PdfTextDirection.RightToLeft)));
        Assert.Equal(FirstLatin, FirstGlyph(Save(entryPoint, new PdfDocument { TextDirection = PdfTextDirection.RightToLeft }, PdfTextDirection.LeftToRight)));
        Assert.Equal(FirstLatin, FirstGlyph(Save(entryPoint, new PdfDocument { TextDirection = PdfTextDirection.RightToLeft }, PdfTextDirection.Auto)));
    }

    [Fact]
    public void Options_without_a_direction_follow_the_document()
    {
        var document = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
        document.Body.AddText(Line, Font(), new PdfTextOptions());

        Assert.Equal(LastHebrew, FirstGlyph(PdfPageReader.SaveBytes(document)));
    }

    [Fact]
    public void Page_text_starts_at_x_in_either_direction()
    {
        foreach (var direction in new[] { PdfTextDirection.LeftToRight, PdfTextDirection.RightToLeft })
        {
            var document = new PdfDocument();
            document.AddPage().AddText(Line, Font(), 72, 700, new PdfTextOptions { Direction = direction });
            var content = Assert.Single(PdfPageReader.Read(PdfPageReader.SaveBytes(document))).Content;

            Assert.Contains(" 72 700 Tm", content);
        }
    }

    [Fact]
    public void A_document_with_no_direction_set_draws_as_before()
    {
        var font = Font();
        var plain = new PdfDocument();
        plain.Body.AddText("Hello 15", font);
        var automatic = new PdfDocument { TextDirection = PdfTextDirection.Auto };
        automatic.Body.AddText("Hello 15", font, new PdfTextOptions());

        Assert.Equal(PdfTextDirection.Auto, new PdfDocument().TextDirection);
        Assert.Equal(PdfPageReader.Read(PdfPageReader.SaveBytes(plain))[0].Content, PdfPageReader.Read(PdfPageReader.SaveBytes(automatic))[0].Content);
    }

    [Fact]
    public void Existing_calls_with_literal_numbers_still_compile_and_draw()
    {
        // Calls like these appear in the readmes; a literal 0 must not become a direction.
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Header.AddText("Quarterly report", 0, 8, 14);
        document.Header.AddText("Page {page} of {pages}", 0, 4, 9, PdfTextAlignment.Right);
        document.Body.AddText("Title", 0, 18);
        document.Body.AddBlock().AddText("Block", 0);
        document.AddPage().AddText("Page", 0, 700, 12);

        PdfStructureChecker.AssertValid(PdfPageReader.SaveBytes(document));
    }

    [Fact]
    public void Undefined_directions_and_null_options_are_rejected()
    {
        var font = Font();
        var document = new PdfDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => document.TextDirection = (PdfTextDirection)7);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfTextOptions { Direction = (PdfTextDirection)(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfCellText("a") { Direction = (PdfTextDirection)3 });
        Assert.Throws<ArgumentNullException>(() => document.Body.AddText("a", (PdfTextOptions)null!));
        Assert.Throws<ArgumentNullException>(() => document.Body.AddText("a", font, (PdfTextOptions)null!));
        Assert.Throws<ArgumentNullException>(() => document.Body.AddBlock().AddText("a", (PdfTextOptions)null!));
        Assert.Throws<ArgumentNullException>(() => document.Header.AddText("a", 0, 0, (PdfTextOptions)null!));
        Assert.Throws<ArgumentNullException>(() => document.AddPage().AddText("a", 0, 0, (PdfTextOptions)null!));
        Assert.Throws<ArgumentNullException>(() => document.AddPage().AddText("a", (PdfFont)null!, 0, 0, new PdfTextOptions()));
        Assert.Null(new PdfTextOptions().Direction);
        Assert.Null(new PdfCellText("a").Direction);
    }

    #endregion

    #region Private Methods

    private static PdfFont Font() => RightToLeftScriptTests.Font(Line);

    // Draws Line at the entry point; a direction on the call when given (cell text carries it on PdfCellText).
    private static byte[] Save(string entryPoint, PdfDocument document, PdfTextDirection? direction)
    {
        var font = Font();
        var options = new PdfTextOptions { Direction = direction };
        switch (entryPoint)
        {
            case "page":
                document.AddPage().AddText(Line, font, 50, 700, options);
                break;
            case "header":
                document.Header.Height = 40;
                document.Header.AddText(Line, font, 0, 0, options);
                document.AddPage();
                break;
            case "flow":
                document.Body.AddText(Line, font, options);
                break;
            case "block":
                document.Body.AddBlock().AddText(Line, font, options);
                break;
            case "cell":
                var table = document.AddPage().AddTable(50, 700, 400);
                table.GetOrCreateCell(0, 0).SetText(new PdfCellText(Line, font) { Direction = direction });
                break;
        }
        return PdfPageReader.SaveBytes(document);
    }

    private static string FirstGlyph(byte[] pdf)
    {
        string content = Assert.Single(PdfPageReader.Read(pdf)).Content;
        var match = System.Text.RegularExpressions.Regex.Match(content, "<[0-9A-F]{4}>");
        Assert.True(match.Success, content);
        return match.Value;
    }

    #endregion

    #endregion
}
