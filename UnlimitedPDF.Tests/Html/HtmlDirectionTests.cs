using System.Globalization;
using System.Text.RegularExpressions;
using UnlimitedPDF.Tests.Fonts;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// HTML direction (WR-038, WR-041; V-12, V-01 for HTML): the <c>dir</c> attribute (<c>ltr</c>, <c>rtl</c>, <c>auto</c>),
/// the inherited CSS <c>direction</c>, <c>bdi</c>, and <c>bdo</c>; lines ordered by the bidirectional algorithm and
/// copied in logical order; <c>text-align: start</c> and <c>end</c>, table columns, and list markers following the
/// direction; justification by spaces only and no letter spacing inside Arabic; <c>unicode-bidi</c> still unsupported;
/// the document's <c>TextDirection</c> not applying to HTML; left-to-right HTML unchanged.
/// </summary>
public class HtmlDirectionTests
{
    #region Fields and Constants

    private const string Letters = "שלוםעולם אבגדה abcJohnSmith!()12. سلامبی";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_right_to_left_page_draws_its_lines_from_the_right_and_copies_them_in_order()
    {
        var (pdf, html) = Render("<html dir='rtl'><body><p>שלום עולם</p></body></html>");

        Assert.Equal("שלום עולם", PageText(pdf));
        Assert.True(FirstX(pdf) > 300, $"x = {FirstX(pdf)}");
        Assert.Contains("/ActualText", Content(pdf));
        Assert.DoesNotContain(html.Warnings, w => w.Kind == PdfHtmlWarningKind.UnsupportedProperty);
        Rendering.RenderTestFiles.AssertDrawnWithTheSameText(pdf);
    }

    [Fact]
    public void Words_of_a_right_to_left_line_are_drawn_from_the_right()
    {
        var (pdf, _) = Render("<p dir='rtl'>שלום עולם</p>");

        // Drawn left to right: the second word (its last letter first), then the first.
        Assert.Equal("םלוע םולש", Drawn(pdf));
    }

    [Theory]
    [InlineData("<p style='direction: rtl'>שלום</p>", true)]
    [InlineData("<div style='direction: rtl'><p>שלום</p></div>", true)]
    [InlineData("<p dir='auto'>שלום abc</p>", true)]
    [InlineData("<p dir='auto'>abc שלום</p>", false)]
    [InlineData("<p dir='bogus'>שלום abc</p>", false)]
    [InlineData("<p>שלום abc</p>", false)]
    public void Direction_comes_from_dir_and_the_inherited_direction_property(string markup, bool rightToLeft)
    {
        var (pdf, _) = Render(markup);
        var (left, _) = Render("<p>abc</p>");

        Assert.Equal(rightToLeft, FirstX(pdf) > FirstX(left) + 50);
    }

    [Fact]
    public void Bdi_keeps_a_name_and_its_punctuation_together()
    {
        var (pdf, _) = Render("<p dir='rtl'>שלום <bdi>John Smith!</bdi> אבג</p>");

        Assert.Equal("גבא John Smith! םולש", Drawn(pdf));
        Assert.Equal("שלום John Smith! אבג", PageText(pdf));
    }

    [Fact]
    public void Bdo_overrides_the_order_of_its_content()
    {
        var (pdf, _) = Render("<p>ab <bdo dir='rtl'>abc</bdo></p>");

        Assert.Equal("ab cba", Drawn(pdf));
    }

    [Fact]
    public void Text_align_start_and_end_follow_the_direction()
    {
        double Start(string dir) => FirstX(Render($"<p dir='{dir}' style='text-align: start'>שלום</p>").Pdf);
        double End(string dir) => FirstX(Render($"<p dir='{dir}' style='text-align: end'>שלום</p>").Pdf);
        double Right() => FirstX(Render("<p dir='rtl' style='text-align: right'>שלום</p>").Pdf);
        double Left() => FirstX(Render("<p dir='rtl' style='text-align: left'>שלום</p>").Pdf);

        Assert.Equal(Right(), Start("rtl"), 3);
        Assert.Equal(Left(), End("rtl"), 3);
        Assert.Equal(Content(Render("<p style='text-align: left'>abc</p>").Pdf), Content(Render("<p style='text-align: start'>abc</p>").Pdf));
        Assert.Equal(Content(Render("<p style='text-align: right'>abc</p>").Pdf), Content(Render("<p style='text-align: end'>abc</p>").Pdf));
    }

    [Fact]
    public void A_right_to_left_table_puts_its_first_column_at_the_right()
    {
        var (pdf, _) = Render("<table dir='rtl'><tr><td>אבג</td><td>abc</td></tr></table>");

        var xs = TextXs(Content(pdf));
        Assert.Equal(2, xs.Count);
        Assert.True(xs[0] > xs[1], string.Join(", ", xs)); // the first cell's text is drawn right of the second's
    }

    [Fact]
    public void A_right_to_left_list_puts_its_markers_at_the_right()
    {
        var (rtl, _) = Render("<ul dir='rtl'><li>שלום</li></ul>");
        var (ltr, _) = Render("<ul><li>abc</li></ul>");

        double Marker(byte[] pdf) => double.Parse(Regex.Match(Content(pdf), @"([\d.]+) [\d.]+ m [\d. ]+ c").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(Marker(rtl) > FirstX(rtl), $"marker {Marker(rtl)}, text {FirstX(rtl)}");
        Assert.True(Marker(ltr) < FirstX(ltr));
    }

    [Fact]
    public void Arabic_takes_no_letter_spacing_between_joined_letters()
    {
        string spaced = Content(Render("<p style='letter-spacing: 3pt'>سلام</p>").Pdf);
        string plain = Content(Render("<p>سلام</p>").Pdf);

        Assert.Equal(plain, spaced);
    }

    [Fact]
    public void Unicode_bidi_is_still_unsupported()
    {
        var (_, html) = Render("<p style='unicode-bidi: isolate'>abc</p>");

        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.UnsupportedProperty && w.Source == "unicode-bidi");
    }

    [Fact]
    public void The_document_text_direction_does_not_change_html()
    {
        var (plain, _) = Render("<p>abc שלום</p>");
        var (rtlDocument, _) = Render("<p>abc שלום</p>", new PdfDocument { TextDirection = PdfTextDirection.RightToLeft });

        Assert.Equal(Content(plain), Content(rtlDocument));
    }

    [Fact]
    public void A_right_to_left_justified_paragraph_ends_its_last_line_at_the_right()
    {
        string words = string.Join(" ", Enumerable.Repeat("שלום", 40));
        var (pdf, _) = Render($"<p dir='rtl' style='text-align: justify'>{words}</p>");

        var xs = TextXs(Content(pdf));
        Assert.True(xs.Count >= 3);
        Assert.True(xs[^1] > xs[0] + 20, string.Join(", ", xs)); // full lines start at the left; the last ends at the right
        Assert.Equal(words.Replace(" ", string.Empty), PageText(pdf).Replace(" ", string.Empty));
    }

    #endregion

    #region Private Methods

    private static (byte[] Pdf, PdfHtml Html) Render(string markup, PdfDocument? document = null)
    {
        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Test", RightToLeftScriptTests.Font(Letters));
        var html = PdfHtml.FromString($"<style>body {{ font-family: Test }}</style>{markup}", options);
        document ??= new PdfDocument();
        document.Body.AddHtml(html);
        var pdf = PdfPageReader.SaveBytes(document);
        PdfStructureChecker.AssertValid(pdf);
        return (pdf, html);
    }

    private static string Content(byte[] pdf) => Assert.Single(PdfPageReader.Read(pdf)).Content;

    // HTML draws each run as a text object of its own, so lines are joined as the other HTML tests do.
    private static string PageText(byte[] pdf) => string.Concat(RightToLeftScriptTests.PageText(pdf).Split('\n'));

    private static double FirstX(byte[] pdf) => TextXs(Content(pdf))[0];

    // The x of each text matrix (Tm), in drawing order.
    private static List<double> TextXs(string content) =>
        Regex.Matches(content, @"1 0 (?:-?[\d.]+ )?1 (-?[\d.]+) -?[\d.]+ Tm").Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();

    // The characters drawn, left to right, from the glyph IDs in the content (the test font maps Letters in order).
    private static string Drawn(byte[] pdf)
    {
        var font = string.Concat(Letters.Distinct());
        var sb = new System.Text.StringBuilder();
        foreach (Match m in Regex.Matches(Content(pdf), "<([0-9A-F]{4})>"))
        {
            int glyph = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (glyph >= 1 && glyph <= font.Length) sb.Append(font[glyph - 1]);
        }
        return sb.ToString();
    }

    #endregion

    #endregion
}
