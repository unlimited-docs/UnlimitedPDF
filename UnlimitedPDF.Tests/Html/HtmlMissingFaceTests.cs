namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Missing faces, synthesized styles, and <c>@font-face</c> in HTML (AC-12, AC-13, D-011, D-012, V-12, V-13): text in a
/// registered family without a face of its weight and style uses the nearest face with a <c>MissingFontFace</c>
/// warning, or, when the load opts in, a regular face stroked for bold and slanted for italic without a warning;
/// <c>@font-face</c> is ignored with a <c>FontFaceIgnored</c> warning and its sources are never resolved.
/// </summary>
public class HtmlMissingFaceTests
{
    #region Fields and Constants

    private const string Latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Bold_text_without_a_bold_face_uses_the_regular_face_and_is_reported_once()
    {
        var options = Options(Face());

        var (pdf, html) = Render("<p style='font-family: Brand'><b>One</b> and <b>two</b></p>", options);

        Assert.EndsWith("+Brand-Regular", Assert.Single(PdfInspector.Read(pdf).Fonts()).BaseFont);
        var warning = Assert.Single(html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.MissingFontFace, warning.Kind);
        Assert.Equal("brand", warning.Source);
        Assert.Contains("no bold face", warning.Message);
        Assert.Contains("regular face (weight 400)", warning.Message);
        Assert.DoesNotContain(" Tr", Content(pdf));
    }

    [Fact]
    public void Each_missing_weight_and_style_is_reported()
    {
        var options = Options(Face(), Face(weight: 700));

        var (_, html) = Render("<p style='font-family: Brand'><b>Bold</b> <i>Italic</i> <b><i>Both</i></b> <i>Again</i></p>", options);

        Assert.Equal(new[] { "no italic face", "no bold italic face" }, html.Warnings.Select(w => w.Message.Split(';')[0].Split(" has ")[1]));
        Assert.All(html.Warnings, w => Assert.Equal(PdfHtmlWarningKind.MissingFontFace, w.Kind));
    }

    [Fact]
    public void A_family_with_every_face_needed_is_not_reported()
    {
        var builder = new TestFontBuilder("Flex") { WeightAxis = (100, 400, 900) };
        builder.MapCharacters(Latin);
        var options = Options(PdfFont.FromBytes(builder.Build()));

        var (_, html) = Render("<p style='font-family: Brand'>Plain <b>bold</b> <span style='font-weight: 300'>light</span></p>", options);

        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Synthesis_strokes_bold_and_slants_italic_without_a_warning()
    {
        var options = Options(Face());
        options.SynthesizeMissingStyles = true;

        var (bold, boldHtml) = Render("<p style='font-family: Brand'><b>Bold</b></p>", options);
        var (italic, italicHtml) = Render("<p style='font-family: Brand'><i>Italic</i></p>", options);
        var (both, _) = Render("<p style='font-family: Brand'><b><i>Both</i></b></p>", options);

        // RG and w = stroke color and width (12 pt / 30), 2 Tr = fill then stroke; the Tm shear is tan(12 degrees)
        Assert.Contains("q 0 0 0 RG 0.4 w BT 0 0 0 rg 2 Tr /E1 12 Tf 1 0 0 1 ", Content(bold));
        Assert.Contains("BT 0 0 0 rg /E1 12 Tf 1 0 0.2126 1 ", Content(italic));
        Assert.Contains("2 Tr /E1 12 Tf 1 0 0.2126 1 ", Content(both));
        Assert.Empty(boldHtml.Warnings);
        Assert.Empty(italicHtml.Warnings);
        Assert.Equal("Bold", Text(bold));
    }

    [Fact]
    public void Synthesis_leaves_registered_faces_alone_and_cannot_lighten_or_straighten()
    {
        var options = Options(Face(weight: 700, italic: true));
        options.SynthesizeMissingStyles = true;

        var (pdf, html) = Render("<p style='font-family: Brand'><b><i>Both</i></b> Plain</p>", options);

        Assert.DoesNotContain(" Tr", Content(pdf));
        Assert.DoesNotContain(" 0.2126 ", Content(pdf));
        var warning = Assert.Single(html.Warnings); // only the regular text: a bold italic face cannot be made regular
        Assert.Contains("no regular face", warning.Message);
    }

    [Fact]
    public void Synthesis_also_draws_bold_text_in_fallback_fonts_and_fallback_fonts_are_never_reported()
    {
        var document = new PdfDocument();
        document.FontFallback.Add(Face());
        var options = new PdfHtmlLoadOptions { SynthesizeMissingStyles = true };

        var (synthesized, html) = Render("<p style='font-family: Unknown'><b>Bold</b></p>", options, document);
        var plainDocument = new PdfDocument();
        plainDocument.FontFallback.Add(Face());
        var (plain, plainHtml) = Render("<p style='font-family: Unknown'><b>Bold</b></p>", null, plainDocument);

        Assert.Contains("2 Tr", Content(synthesized));
        Assert.DoesNotContain(" Tr", Content(plain));
        Assert.Empty(html.Warnings);
        Assert.Empty(plainHtml.Warnings);
    }

    [Fact]
    public void A_missing_face_fails_a_strict_save()
    {
        var options = Options(Face());
        options.FailOnWarning = true;
        var document = new PdfDocument();
        document.Body.AddHtml(PdfHtml.FromString("<p style='font-family: Brand'><b>Bold</b></p>", options));

        var error = Assert.Throws<PdfHtmlException>(() => PdfPageReader.SaveBytes(document));

        Assert.Equal(PdfHtmlWarningKind.MissingFontFace, error.Warning!.Kind);
    }

    [Fact]
    public void Font_face_rules_are_ignored_with_a_warning_and_never_resolved()
    {
        int calls = 0;
        var options = new PdfHtmlLoadOptions
        {
            ResourceResolver = reference => { calls++; return null; }
        };
        const string css = "@font-face { font-family: Web; src: url(https://example.invalid/web.ttf), url(fonts/local.otf) }"
            + "@media print { @font-face { font-family: Print; src: url(print.ttf) } }"
            + "p { font-family: Web, serif }";

        var html = PdfHtml.FromString($"<style>{css}</style><p>Hello</p>", options);
        var (pdf, _) = Render(html);

        Assert.Equal(0, calls);
        Assert.Equal(2, html.Warnings.Count(w => w.Kind == PdfHtmlWarningKind.FontFaceIgnored));
        Assert.All(html.Warnings, w => Assert.Equal("@font-face", w.Source));
        Assert.Contains("PdfHtmlLoadOptions.Fonts", html.Warnings[0].Message);
        Assert.Contains("/HF5 ", Content(pdf)); // the unknown family falls back to serif
        Assert.Throws<PdfHtmlException>(() => PdfHtml.FromString($"<style>{css}</style>", new PdfHtmlLoadOptions { FailOnWarning = true }));
    }

    #endregion

    #region Private Methods

    private static string Content(byte[] pdf) => string.Join("\n", PdfPageReader.Read(pdf).Select(p => p.Content));

    private static PdfFont Face(ushort weight = 400, bool italic = false)
    {
        var builder = new TestFontBuilder("Brand") { Weight = weight, Italic = italic };
        builder.MapCharacters(Latin);
        return PdfFont.FromBytes(builder.Build());
    }

    private static PdfHtmlLoadOptions Options(params PdfFont[] faces)
    {
        var options = new PdfHtmlLoadOptions();
        foreach (var face in faces) options.Fonts.Add("Brand", face);
        return options;
    }

    private static (byte[] Pdf, PdfHtml Html) Render(string markup, PdfHtmlLoadOptions? options = null, PdfDocument? document = null) =>
        Render(PdfHtml.FromString(markup, options), document);

    private static (byte[] Pdf, PdfHtml Html) Render(PdfHtml html, PdfDocument? document = null)
    {
        document ??= new PdfDocument();
        document.Body.AddHtml(html);
        return (PdfPageReader.SaveBytes(document), html);
    }

    private static string Text(byte[] pdf)
    {
        var errors = new List<string>();
        string text = string.Concat(PdfInspector.Read(pdf).PageText(errors)).Replace("\n", string.Empty);
        Assert.Empty(errors);
        return text;
    }

    #endregion

    #endregion
}
