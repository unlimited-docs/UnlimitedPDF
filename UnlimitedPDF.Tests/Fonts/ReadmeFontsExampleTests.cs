using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The "Fonts and languages" example in docs/guides/fonts-and-languages.md and nuget/README.md runs as written with
/// generated fonts in place of the files it names, the guide and the package readme show the same section with its known
/// limitations, and the changelog and the package readme list the same 1.1.0 changes at version 1.1.0 (AC-15, V-21,
/// PR-004, PR-012, QF-26112603).
/// </summary>
public sealed class ReadmeFontsExampleTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"readme-fonts-{Guid.NewGuid():N}");

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Readme_fonts_sample_runs_as_written()
    {
        // The readme's code, with its font files generated and its relative paths resolved under a temporary folder.
        Directory.CreateDirectory(Path.Combine(_root, "fonts"));
        Write("fonts/BrandSans-Regular.ttf", "Brand Sans", "InvoiceMrTtal₹0123456789 ,.", 400);
        Write("fonts/BrandSans-Bold.ttf", "Brand Sans", "InvoiceMrTtal₹0123456789 ,.", 700);
        Write("fonts/BrandDevanagari-Regular.ttf", "Brand Devanagari", "नमसतेकुल्ं", 400);
        Write("fonts/BrandTelugu-Regular.ttf", "Brand Telugu", "ధనయవాదలు్", 400);
        string At(string path) => Path.Combine(_root, path);

        var latin = PdfFont.FromFile(At("fonts/BrandSans-Regular.ttf"));
        var hindi = PdfFont.FromFile(At("fonts/BrandDevanagari-Regular.ttf"));
        var telugu = PdfFont.FromFile(At("fonts/BrandTelugu-Regular.ttf"));

        var document = new PdfDocument();
        document.FontFallback.Add(hindi); // used for characters the call's font doesn't have
        document.FontFallback.Add(telugu);

        var page = document.AddPage();
        page.AddText("Invoice", latin, 72, 760, 18);
        page.AddText("Merci, नमस्ते, ధన్యవాదాలు", latin, 72, 730); // three scripts in one line

        var missing = document.FindMissingCharacters("Thank you 🙏", latin); // ["🙏"]
        document.FailOnMissingCharacters = true;
        document.Save(At("invoice.pdf"));

        var options = new PdfHtmlLoadOptions();
        options.Fonts.Add("Brand Sans", latin);
        options.Fonts.Add("Brand Sans", PdfFont.FromFile(At("fonts/BrandSans-Bold.ttf")));
        options.Fonts.Add("Brand Devanagari", hindi);
        var html = PdfHtml.FromString("<p style=\"font-family: 'Brand Sans', 'Brand Devanagari'\">Total <b>₹ 1,250</b> कुल</p>", options);
        PdfOperations.ConvertHtml(html, At("total.pdf"));

        Assert.Equal(new[] { "🙏" }, missing);
        var invoice = File.ReadAllBytes(At("invoice.pdf"));
        PdfStructureChecker.AssertValid(invoice);
        Assert.Equal(3, PdfInspector.Read(invoice).Fonts().Count);
        Assert.Equal("Invoice\nMerci, नमस्ते, ధన్యవాదాలు", Assert.Single(PdfInspector.Read(invoice).PageText()));
        var total = File.ReadAllBytes(At("total.pdf"));
        PdfStructureChecker.AssertValid(total);
        Assert.Equal(3, PdfInspector.Read(total).Fonts().Count); // regular, bold, and Devanagari
        Assert.Empty(html.Warnings);
    }

    [Fact]
    public void Readme_right_to_left_sample_runs_as_written()
    {
        // The readme's right-to-left code, with its font generated (WR-041).
        Directory.CreateDirectory(Path.Combine(_root, "fonts"));
        Write("fonts/BrandArabic-Regular.ttf", "Brand Arabic", "فاتورةرقم42iPhone15السعرلمنتج ModelX", 400);
        string At(string path) => Path.Combine(_root, path);

        var arabic = PdfFont.FromFile(At("fonts/BrandArabic-Regular.ttf"));

        var document = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
        document.FontFallback.Add(arabic);

        document.Body.AddText("فاتورة رقم 42", arabic, fontSize: 18); // starts at the right edge
        document.Body.AddText("iPhone 15 السعر", arabic); // still reads from the right
        document.Body.AddText("Model X1", new PdfTextOptions { Direction = PdfTextDirection.LeftToRight }); // one left-to-right line

        var table = document.Body.AddTable(400); // column 0 is drawn at the right
        table.SetText(0, 0, "المنتج", arabic);
        table.SetText(0, 1, "السعر", arabic);
        document.Save(At("invoice-ar.pdf"));

        var pdf = File.ReadAllBytes(At("invoice-ar.pdf"));
        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal("فاتورة رقم 42\niPhone 15 السعر\nModel X1\nالمنتج\nالسعر", Assert.Single(PdfInspector.Read(pdf).PageText()));
    }

    [Fact]
    public void The_fonts_guide_and_the_changelog_match_the_package_readme()
    {
        string readme = RepositoryDocs.Read("README.md");
        string nuget = RepositoryDocs.Read(Path.Combine("nuget", "README.md"));
        string fonts = RepositoryDocs.GuideBody("fonts-and-languages.md");

        Assert.Equal(RepositoryDocs.WithoutLinkTargets(RepositoryDocs.SectionBody(nuget, "## Fonts and languages")), RepositoryDocs.WithoutLinkTargets(fonts));
        string changelog = RepositoryDocs.Read("CHANGELOG.md");
        foreach (string version in new[] { "1.2.0", "1.1.0" })
            Assert.Equal(RepositoryDocs.WithoutLinkTargets(RepositoryDocs.SectionBody(nuget, "## Changes in " + version)), RepositoryDocs.WithoutLinkTargets(RepositoryDocs.SectionBody(changelog, "## " + version)));

        Assert.Contains("PdfFont.FromFile(\"fonts/BrandSans-Regular.ttf\")", fonts, StringComparison.Ordinal);
        Assert.Contains("document.FindMissingCharacters(\"Thank you 🙏\", latin); // [\"🙏\"]", fonts, StringComparison.Ordinal);
        foreach (string limit in new[] { "Right-to-left scripts other than Arabic, Hebrew, and Thaana", "Chinese, Japanese, and Korean are not supported", "Font collections (`.ttc`) and web fonts (WOFF, WOFF2) can't be loaded", "`@font-face` rules are ignored", "`unicode-bidi` is not supported" })
            Assert.Contains(limit, fonts, StringComparison.Ordinal);

        Assert.Contains("register your own", RepositoryDocs.GuideBody("html-to-pdf.md"), StringComparison.Ordinal);
        Assert.Contains("(docs/guides/fonts-and-languages.md)", readme, StringComparison.Ordinal);
        Assert.True(nuget.IndexOf("## HTML to PDF", StringComparison.Ordinal) < nuget.IndexOf("## Fonts and languages", StringComparison.Ordinal));
        Assert.True(nuget.IndexOf("## Changes in 1.2.0", StringComparison.Ordinal) < nuget.IndexOf("## Changes in 1.1.0", StringComparison.Ordinal));
        Assert.True(nuget.IndexOf("## Changes in 1.1.0", StringComparison.Ordinal) < nuget.IndexOf("## Changes in 1.0.1", StringComparison.Ordinal));
        Assert.Contains("- [Fonts and languages](#fonts-and-languages)", nuget, StringComparison.Ordinal);
        Assert.Contains("- [Changes in 1.2.0](#changes-in-120)", nuget, StringComparison.Ordinal);
        Assert.Contains("- [Changes in 1.1.0](#changes-in-110)", nuget, StringComparison.Ordinal);
        foreach (string text in new[] { readme, nuget })
            Assert.Contains("dotnet add package UnlimitedPDF --version 1.2.0`", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fonts_section_lists_every_shaped_script_and_the_unsupported_ones()
    {
        // WR-039 and QF-26112401: the scripts the shaper handles, the letters the standard fonts lack, and
        // right-to-left Indian languages named as not supported.
        string fonts = RepositoryDocs.GuideBody("fonts-and-languages.md");

        Assert.Contains("- A supported script: Latin, Greek, Cyrillic, Armenian, Georgian, Devanagari, Bengali-Assamese, Gujarati, Gurmukhi, Odia, Kannada, Malayalam, Telugu, Tamil, Arabic, Hebrew, or Thaana.", fonts, StringComparison.Ordinal);
        Assert.Contains("Latin, Greek, Cyrillic, Armenian, and Georgian (accents, ligatures, kerning, each with its own script's font rules)", fonts, StringComparison.Ordinal);
        Assert.Contains("Polish `ł`, Czech `č`, Romanian `ș`, and Turkish `ğ` and `ı`", fonts, StringComparison.Ordinal);
        Assert.Contains("`text-transform` follows the `lang` attribute for Turkish and Azerbaijani", fonts, StringComparison.Ordinal);
        Assert.Contains("Devanagari, Bengali-Assamese, Gujarati, Gurmukhi, Odia, Kannada, Malayalam, Telugu, and Tamil are shaped", fonts, StringComparison.Ordinal);
        // WR-041 (65-right-to-left-scripts): the ten right-to-left languages, direction, alignment, and HTML.
        Assert.Contains("Arabic, Persian, Urdu, Pashto, Kurdish (Sorani), Sindhi, Uyghur, Malay (Jawi), Hebrew, Dhivehi", fonts, StringComparison.Ordinal);
        foreach (string api in new[] { "`PdfDocument.TextDirection`", "`PdfTextOptions.Direction`", "`PdfTable.Direction`", "`PdfCellText.Direction`", "`PdfTextAlignment.Start` and `End`", "`<bdi>`, and `<bdo>`" })
            Assert.Contains(api, fonts, StringComparison.Ordinal);
        Assert.Equal(9, UnlimitedPDF.Models.Internal.Text.IndicScript.All.Count); // the nine Indic scripts listed above
    }

    [Fact]
    public void The_package_version_and_release_notes_name_1_2_0()
    {
        string project = File.ReadAllText(RepositoryFile(Path.Combine("UnlimitedPDF", "UnlimitedPDF.csproj")));

        Assert.Equal("1.2.0", Regex.Match(project, "<Version>([^<]+)</Version>").Groups[1].Value);
        Assert.Equal("1.2.0", Regex.Match(project, "<PackageVersion>([^<]+)</PackageVersion>").Groups[1].Value);
        Assert.StartsWith("1.2.0:", Regex.Match(project, "<PackageReleaseNotes>([^<]+)</PackageReleaseNotes>").Groups[1].Value, StringComparison.Ordinal);
        // The notes link the changelog: the README keeps only the latest release's highlights (QF-26112603, QF-26112605).
        foreach (string anchor in new[] { "CHANGELOG.md#120", "CHANGELOG.md#110", "CHANGELOG.md#101", "CHANGELOG.md#100" })
            Assert.Contains(anchor, project, StringComparison.Ordinal);
        Assert.DoesNotContain("UnlimitedPDF#changes-in-", project, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!)!, relative);

    private void Write(string path, string family, string characters, ushort weight)
    {
        var builder = new TestFontBuilder(family) { Weight = weight };
        builder.MapCharacters(characters);
        File.WriteAllBytes(Path.Combine(_root, path), builder.Build());
    }

    #endregion

    #endregion
}
