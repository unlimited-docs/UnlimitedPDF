using UnlimitedPDF.Models.Internal;
using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Html;
using UnlimitedPDF.Models.Internal.Html.Drawing;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// HTML drawing operators keep their state to themselves, use invariant numbers, and escape text (WR-038, PR-006, A05).
/// </summary>
public class HtmlOperatorTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Text_runs_write_WinAnsi_codes_as_octal_escapes()
    {
        string ops = HtmlOperators.Text("HF2", 12, PdfColor.Black, 10, 700, WinAnsiEncoding.Encode("Café €5"));
        Assert.Equal(@"q BT /HF2 12 Tf 0 0 0 rg 1 0 0 1 10 700 Tm (Caf\351 \2005) Tj ET Q", ops);
    }

    [Fact]
    public void Text_runs_set_word_and_character_spacing_only_when_used()
    {
        string ops = HtmlOperators.Text("HF1", 10.5, new PdfColor(1, 0, 0), 0, 0, WinAnsiEncoding.Encode("a b"), 1.25, 0.5);
        Assert.Equal("q BT /HF1 10.5 Tf 1 0 0 rg 1.25 Tw 0.5 Tc 1 0 0 1 0 0 Tm (a b) Tj ET Q", ops);
    }

    [Theory]
    [InlineData(") Tj /F1 99 Tf (", @"\) Tj /F1 99 Tf \(")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("(balanced)", @"\(balanced\)")]
    public void Injection_text_stays_inside_the_string(string text, string expected)
    {
        Assert.Equal(expected, PdfLiteralString.EscapeBytes(WinAnsiEncoding.Encode(text)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Numbers_are_invariant_in_every_culture(string culture)
    {
        using var scope = new CultureScope(culture);
        string ops = HtmlOperators.Fill(1.5, 2.25, 3.125, 4, new PdfColor(0.5, 0.25, 0));
        Assert.Equal("q 0.5 0.25 0 rg 1.5 2.25 3.125 4 re f Q", ops);
    }

    [Theory]
    [InlineData("Solid", "q 0 0 0 RG 2 w 0 0 m 10 0 l S Q")]
    [InlineData("Dashed", "q 0 0 0 RG 2 w [6 6] 0 d 0 0 m 10 0 l S Q")]
    [InlineData("Dotted", "q 0 0 0 RG 2 w 1 J [0 4] 0 d 0 0 m 10 0 l S Q")]
    [InlineData("None", "")]
    public void Lines_use_the_style_dash_pattern(string style, string expected)
    {
        Assert.Equal(expected, HtmlOperators.Line(0, 0, 10, 0, 2, PdfColor.Black, Enum.Parse<HtmlBorderStyle>(style)));
    }

    [Fact]
    public void Clips_and_images_save_and_restore_state()
    {
        Assert.Equal("q 0 0 100 50 re W n", HtmlOperators.ClipBegin(0, 0, 100, 50));
        Assert.Equal("Q", HtmlOperators.ClipEnd());
        Assert.Equal("q 30 0 0 20 5 6 cm /Im1 Do Q", HtmlOperators.Image("/Im1", 5, 6, 30, 20));
    }

    #endregion

    #endregion
}
