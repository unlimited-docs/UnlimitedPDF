using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The "Fonts and languages" example in README.md and nuget/README.md runs as written with generated fonts in place of
/// the files it names, both readmes show the same section after "HTML to PDF" with its known limitations, and both list
/// the release under "Changes in 1.1.0" at version 1.1.0 (AC-15, V-21, PR-004, PR-012).
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
    public void Both_readmes_show_the_same_fonts_section_and_the_1_1_0_changes()
    {
        string readme = File.ReadAllText(RepositoryFile("README.md")).Replace("\r\n", "\n");
        string nuget = File.ReadAllText(RepositoryFile(Path.Combine("nuget", "README.md"))).Replace("\r\n", "\n");

        foreach (string heading in new[] { "## Fonts and languages", "## Changes in 1.1.0" })
            Assert.Equal(Section(readme, heading), Section(nuget, heading));

        string fonts = Section(readme, "## Fonts and languages");
        Assert.Contains("PdfFont.FromFile(\"fonts/BrandSans-Regular.ttf\")", fonts, StringComparison.Ordinal);
        Assert.Contains("document.FindMissingCharacters(\"Thank you 🙏\", latin); // [\"🙏\"]", fonts, StringComparison.Ordinal);
        foreach (string limit in new[] { "Right-to-left scripts", "Chinese, Japanese, and Korean are not supported", "Font collections (`.ttc`) and web fonts (WOFF, WOFF2) can't be loaded", "`@font-face` rules are ignored" })
            Assert.Contains(limit, fonts, StringComparison.Ordinal);

        foreach (string text in new[] { readme, nuget })
        {
            Assert.True(text.IndexOf("## HTML to PDF", StringComparison.Ordinal) < text.IndexOf("## Fonts and languages", StringComparison.Ordinal));
            Assert.True(text.IndexOf("## Changes in 1.1.0", StringComparison.Ordinal) < text.IndexOf("## Changes in 1.0.1", StringComparison.Ordinal));
            Assert.Contains("- [Fonts and languages](#fonts-and-languages)", text, StringComparison.Ordinal);
            Assert.Contains("- [Changes in 1.1.0](#changes-in-110)", text, StringComparison.Ordinal);
            Assert.Contains("register your own", Section(text, "## HTML to PDF"), StringComparison.Ordinal);
            Assert.Contains("dotnet add package UnlimitedPDF --version 1.1.0`", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_package_version_and_release_notes_name_1_1_0()
    {
        string project = File.ReadAllText(RepositoryFile(Path.Combine("UnlimitedPDF", "UnlimitedPDF.csproj")));

        Assert.Equal("1.1.0", Regex.Match(project, "<Version>([^<]+)</Version>").Groups[1].Value);
        Assert.Equal("1.1.0", Regex.Match(project, "<PackageVersion>([^<]+)</PackageVersion>").Groups[1].Value);
        Assert.StartsWith("1.1.0:", Regex.Match(project, "<PackageReleaseNotes>([^<]+)</PackageReleaseNotes>").Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains("#changes-in-110", project, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!)!, relative);

    private static string Section(string text, string heading)
    {
        int start = text.IndexOf(heading + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, heading);
        int end = text.IndexOf("\n## ", start + 3, StringComparison.Ordinal);
        return text.Substring(start, (end < 0 ? text.Length : end) - start);
    }

    private void Write(string path, string family, string characters, ushort weight)
    {
        var builder = new TestFontBuilder(family) { Weight = weight };
        builder.MapCharacters(characters);
        File.WriteAllBytes(Path.Combine(_root, path), builder.Build());
    }

    #endregion

    #endregion
}
