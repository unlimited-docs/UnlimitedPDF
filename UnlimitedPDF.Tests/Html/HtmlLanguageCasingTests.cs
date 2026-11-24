using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Layout;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// <c>text-transform</c> follows the <c>lang</c> attribute for Turkish and Azerbaijani dotted and dotless i
/// (QF-26112401, F-5); every other language keeps the invariant casing.
/// </summary>
public class HtmlLanguageCasingTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("tr", "istanbul ılık", "İSTANBUL ILIK")]
    [InlineData("tr-TR", "iyi", "İYİ")]
    [InlineData("az", "bakı şəhəri", "BAKI ŞƏHƏRİ")]
    [InlineData("en", "istanbul", "ISTANBUL")]
    [InlineData(null, "istanbul", "ISTANBUL")]
    [InlineData("trk", "istanbul", "ISTANBUL")]
    public void Uppercase_follows_the_language(string? language, string text, string expected) =>
        Assert.Equal(expected, HtmlLayoutEngine.ApplyTransform(text, CssTextTransform.Uppercase, language));

    [Theory]
    [InlineData("tr", "İSTANBUL ILIK", "istanbul ılık")]
    [InlineData("az", "BAKI", "bakı")]
    [InlineData("en", "ISTANBUL", "istanbul")]
    public void Lowercase_follows_the_language(string? language, string text, string expected) =>
        Assert.Equal(expected, HtmlLayoutEngine.ApplyTransform(text, CssTextTransform.Lowercase, language));

    [Theory]
    [InlineData("tr", "izmir iskenderun", "İzmir İskenderun")]
    [InlineData("en", "izmir iskenderun", "Izmir Iskenderun")]
    public void Capitalize_follows_the_language(string? language, string text, string expected) =>
        Assert.Equal(expected, HtmlLayoutEngine.ApplyTransform(text, CssTextTransform.Capitalize, language));

    [Fact]
    public void The_language_is_inherited_from_the_nearest_lang_attribute()
    {
        var styles = CssTests.Styles("<html lang='tr'><body><p>a</p><p lang='en'>b <span>c</span></p><p lang=''>d</p></body></html>", out var tree);

        Assert.Equal("tr", styles[CssTests.Find(tree, "p", 0)].Language);
        Assert.Equal("en", styles[CssTests.Find(tree, "span", 0)].Language);
        Assert.Equal(string.Empty, styles[CssTests.Find(tree, "p", 2)].Language);
    }

    [Fact]
    public void An_uppercase_turkish_heading_draws_a_dotted_capital_i()
    {
        var html = PdfHtml.FromString("<html lang='tr'><body><h1 style='text-transform: uppercase'>istanbul</h1></body></html>");
        using var output = new MemoryStream();

        PdfOperations.ConvertHtml(html, output);

        // İ is in no standard font's encoding, so without a font of its own it is reported as missing: proof it was drawn.
        Assert.Contains(html.Warnings, w => w.Kind == PdfHtmlWarningKind.MissingCharacter && w.Message.Contains("U+0130"));
    }

    #endregion

    #endregion
}
