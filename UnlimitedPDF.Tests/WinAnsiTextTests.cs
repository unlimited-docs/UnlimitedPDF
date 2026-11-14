namespace UnlimitedPDF.Tests;

/// <summary>
/// Text drawn without a caller font (WR-039, D-009): ASCII-only text keeps the document font /F1 and its exact bytes;
/// text with other characters uses the WinAnsi Helvetica resource, where WinAnsi characters draw and every other
/// character becomes '?'. This holds for every programmatic entry point.
/// </summary>
public class WinAnsiTextTests
{
    #region Fields and Constants

    // "Café – 5 €" with curly quotes: é = 233 (octal 351), – = 150 (226), € = 128 (200), ‘ = 145 (221), ’ = 146 (222).
    private const string Western = "Café – 5 € ‘ok’";
    private const string WesternLiteral = @"(Caf\351 \226 5 \200 \221ok\222) Tj";

    #endregion

    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> EntryPoints() => new[]
    {
        new object[] { "page" },
        new object[] { "header" },
        new object[] { "flow" },
        new object[] { "block" },
        new object[] { "cell" }
    };

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Western_characters_draw_with_the_WinAnsi_built_in_font(string entryPoint)
    {
        var bytes = Save(entryPoint, Western);

        var page = PdfPageReader.Read(bytes).Single();
        Assert.Contains(WesternLiteral, page.Content);
        Assert.Contains("/HF1 ", page.Content);
        Assert.DoesNotContain("?", page.Content);
        Assert.Contains("/BaseFont /Helvetica", Encoding.ASCII.GetString(bytes));
        Assert.Contains("/Encoding /WinAnsiEncoding", Encoding.ASCII.GetString(bytes));
        Assert.Matches(@"/HF1 \d+ 0 R", Encoding.ASCII.GetString(bytes));
        PdfStructureChecker.AssertValid(bytes);
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Characters_outside_WinAnsi_become_question_marks(string entryPoint)
    {
        var page = PdfPageReader.Read(Save(entryPoint, "నమస్తే")).Single(); // నమస్తే

        Assert.Contains("(??????) Tj", page.Content);
        Assert.Contains("/HF1 ", page.Content);
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void Ascii_text_keeps_the_document_font(string entryPoint)
    {
        var bytes = Save(entryPoint, "Invoice #1024");

        var page = PdfPageReader.Read(bytes).Single();
        Assert.Contains("/F1 ", page.Content);
        Assert.DoesNotContain("/HF1", page.Content);
        Assert.DoesNotContain("/HF1", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Centered_western_text_is_measured_with_its_real_widths()
    {
        // î is 278/1000 em wide in Helvetica; measured as '?' (556) it would center 1.39 pt further left.
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Header.AddText("î", 0, 0, 10, PdfTextAlignment.Center); // î, width 278 in Helvetica
        document.AddPage();

        var content = PdfPageReader.Save(document).Single().Content;

        // Page width 595.28, no margins: x = (595.28 - 2.78) / 2 = 296.25.
        Assert.Contains("1 0 0 1 296.25 ", content);
    }

    #endregion

    #region Private Methods

    private static byte[] Save(string entryPoint, string text)
    {
        var document = new PdfDocument();
        switch (entryPoint)
        {
            case "page":
                document.AddPage().AddText(text, 50, 700);
                break;
            case "header":
                document.Header.Height = 40;
                document.Header.AddText(text, 0, 0);
                document.AddPage();
                break;
            case "flow":
                document.Body.AddText(text);
                break;
            case "block":
                document.Body.AddBlock().AddText(text);
                break;
            case "cell":
                document.AddPage().AddTable(50, 700, 300).GetOrCreateCell(0, 0).Text = text;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entryPoint));
        }
        return PdfPageReader.SaveBytes(document);
    }

    #endregion

    #endregion
}
