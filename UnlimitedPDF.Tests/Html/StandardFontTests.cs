using UnlimitedPDF.Models.Internal;
using UnlimitedPDF.Models.Internal.Fonts;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// HTML text uses the twelve standard fonts with WinAnsi characters; others are written as '?' (WR-038, AC-13).
/// </summary>
public class StandardFontTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData('é', 0xE9)]
    [InlineData('ü', 0xFC)]
    [InlineData('ñ', 0xF1)]
    [InlineData('£', 0xA3)]
    [InlineData('€', 0x80)]
    [InlineData('•', 0x95)]
    [InlineData('—', 0x97)]
    [InlineData('A', 0x41)]
    public void WinAnsi_characters_get_their_code(char c, int expected)
    {
        Assert.True(WinAnsiEncoding.TryEncode(c, out byte code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("₹")]
    [InlineData("Ω")]
    [InlineData("\u0007")]
    [InlineData("😀")]
    public void Characters_outside_WinAnsi_become_one_question_mark(string text)
    {
        Assert.Equal(new[] { (byte)'?' }, WinAnsiEncoding.Encode(text));
    }

    [Fact]
    public void Windows1252_decoding_round_trips_every_defined_code()
    {
        var codes = Enumerable.Range(32, 224).Select(i => (byte)i)
            .Where(b => b != 127 && b != 129 && b != 141 && b != 143 && b != 144 && b != 157).ToArray();
        string text = WinAnsiEncoding.Decode(codes, 0, codes.Length);
        Assert.Equal(codes, WinAnsiEncoding.Encode(text));
        Assert.Equal("€", WinAnsiEncoding.Decode(new byte[] { 0x80 }, 0, 1));
        Assert.Equal("\u0081", WinAnsiEncoding.Decode(new byte[] { 0x81 }, 0, 1));
    }

    [Fact]
    public void Helvetica_widths_match_the_existing_metrics_for_ASCII()
    {
        // Codes 39 and 96 differ by design: the existing metrics use the standard encoding's quotes.
        for (char c = ' '; c <= '~'; c++)
        {
            if (c == '\'' || c == '`') continue;
            Assert.Equal(HelveticaMetrics.Width(c.ToString(), 1000), StandardFontMetrics.Measure(StandardFont.Helvetica, c.ToString(), 1000), 6);
        }
    }

    [Theory]
    [InlineData("Helvetica", "Hello", 2278)]
    [InlineData("HelveticaOblique", "Hello", 2278)]
    [InlineData("HelveticaBold", "Hello", 2445)]
    [InlineData("TimesRoman", "Hello", 2222)]
    [InlineData("TimesBold", "Hello", 2278)]
    [InlineData("TimesItalic", "Hello", 2222)]
    [InlineData("TimesBoldItalic", "Hello", 2278)]
    [InlineData("CourierBold", "Hello", 3000)]
    [InlineData("TimesRoman", "Café", 1888)]
    [InlineData("Helvetica", "±÷·¯µ", 2335)]
    public void Text_is_measured_with_the_font_widths(string font, string text, int expected)
    {
        Assert.Equal(expected, StandardFontMetrics.Measure(Enum.Parse<StandardFont>(font), text, 1000), 6);
    }

    [Theory]
    [InlineData("Georgia, serif", "Times")]
    [InlineData("'Courier New', monospace", "Courier")]
    [InlineData("monospace", "Courier")]
    [InlineData("Arial", "Helvetica")]
    [InlineData("Unknown Sans", "Helvetica")]
    [InlineData("TIMES NEW ROMAN", "Times")]
    public void Font_family_lists_pick_the_first_known_family(string list, string expected)
    {
        var names = list.Split(',').Select(name => name.Trim().Trim('\'', '"'));
        Assert.Equal(Enum.Parse<StandardFontFamily>(expected), StandardFonts.Family(names));
    }

    [Theory]
    [InlineData("Times", 700, true, "Times-BoldItalic")]
    [InlineData("Times", 400, false, "Times-Roman")]
    [InlineData("Helvetica", 600, false, "Helvetica-Bold")]
    [InlineData("Helvetica", 500, true, "Helvetica-Oblique")]
    [InlineData("Courier", 900, true, "Courier-BoldOblique")]
    public void Weight_600_and_above_is_bold(string family, int weight, bool italic, string expected)
    {
        Assert.Equal(expected, StandardFonts.BaseFontName(StandardFonts.Select(Enum.Parse<StandardFontFamily>(family), weight, italic)));
    }

    #endregion

    #endregion
}
