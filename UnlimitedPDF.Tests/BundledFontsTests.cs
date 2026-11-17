using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The free fonts the Fonts and languages demo includes (CR-001, D-030, D-031, FD-016): every language invoice draws
/// without a missing character in both drawing modes, the files match their readme and carry their license, only that
/// page downloads them, and neither the package nor its tests carry a font file.
/// </summary>
public class BundledFontsTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(DrawMode.TextCalls)]
    [InlineData(DrawMode.Html)]
    public void Every_language_invoice_draws_without_a_missing_character_with_the_included_fonts(DrawMode mode)
    {
        var slots = LoadAll();

        foreach (var preset in LanguageInvoices.All)
        {
            var input = new FontsAndLanguagesInput { Mode = mode, FailOnMissingCharacters = true };
            input.Apply(preset);

            Assert.Empty(FontsAndLanguagesSample.MissingCharacters(input, slots.Select(s => s.Font.Font).ToList()));
            PdfDocument document;
            if (mode == DrawMode.Html)
            {
                document = FontsAndLanguagesHtmlSample.Build(input, slots.Select(s => (s.Script, s.Font.Font)).ToList(), out var html);
                Assert.DoesNotContain(html.Warnings, w => w.Kind == PdfHtmlWarningKind.MissingCharacter);
            }
            else
            {
                var fonts = slots.Select(s => s.Font.Font).ToList();
                document = FontsAndLanguagesSample.Build(input, fonts, fonts);
            }

            // FailOnMissingCharacters makes saving throw on any character drawn as '?'.
            using var output = new MemoryStream();
            document.Save(output);
            Assert.True(output.Length > 0, preset.Id);
        }
    }

    [Fact]
    public void Each_included_font_fills_its_slot_and_matches_its_readme_entry()
    {
        string readme = File.ReadAllText(FontsFile("README.md"));
        Assert.Equal([FontScript.Devanagari, FontScript.Telugu, FontScript.Tamil], BundledFonts.All.Select(f => f.Script));

        foreach (var bundled in BundledFonts.All)
        {
            byte[] bytes = File.ReadAllBytes(FontsFile(bundled.FileName));
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Contains($"| `{bundled.FileName}` | {bundled.Name} Regular |", readme, StringComparison.Ordinal);
            Assert.Contains(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"| {bytes.Length:N0} bytes | `{hash}` |"), readme, StringComparison.Ordinal);

            var font = UploadedFontLoader.Load(bundled.FileName, bytes).Font;
            Assert.NotNull(font);
            Assert.True(FontSlots.Covers(font.Font, bundled.Script), bundled.Name);
        }
    }

    [Fact]
    public void The_license_goes_with_the_fonts_and_each_slot_credits_it()
    {
        string license = File.ReadAllText(FontsFile("OFL.txt"));
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", license, StringComparison.Ordinal);
        foreach (string family in new[] { "devanagari", "telugu", "tamil" })
            Assert.Contains($"Copyright 2022 The Noto Project Authors (https://github.com/notofonts/{family})", license, StringComparison.Ordinal);

        Assert.Equal("fonts/OFL.txt", BundledFonts.LicensePath);
        string slot = DemosFile("Components/FontSlot.razor");
        Assert.Contains("@bundled.Name (included)", slot, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@BundledFonts.LicensePath\" target=\"_blank\" rel=\"noopener\">@BundledFonts.LicenseName</a>, included only for this demo.", slot, StringComparison.Ordinal);
        Assert.Contains("Use your own font: drop a .ttf or .otf", slot, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_fonts_and_languages_page_downloads_the_fonts_and_its_first_preview_waits_for_them()
    {
        string page = DemosFile("Pages/Playground/Languages/FontsAndLanguages.razor");
        Assert.Contains("_defaults = LoadDefaultsAsync();", page, StringComparison.Ordinal);
        Assert.Contains("_bundled = await BundledFonts.LoadAsync(Http);", page, StringComparison.Ordinal);
        Assert.Contains("if (_defaults is not null) await _defaults;", page, StringComparison.Ordinal);
        Assert.Contains("foreach (var (script, font) in _bundled) _slots.TryAdd(script, font);", page, StringComparison.Ordinal);
        Assert.Contains("Loading the included fonts...", page, StringComparison.Ordinal);

        // Nothing else on the site names the font folder, so the first visit downloads no font.
        string root = RepositoryFile("UnlimitedPDF.Demos.BlazorWasm");
        var others = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}fonts{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && (path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal)
                            || path.EndsWith(".html", StringComparison.Ordinal) || path.EndsWith(".css", StringComparison.Ordinal)
                            || path.EndsWith(".js", StringComparison.Ordinal)))
            .Where(path => File.ReadAllText(path).Contains("fonts/", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["Services/BundledFonts.cs"], others);
    }

    [Fact]
    public void Neither_the_package_nor_its_tests_carry_a_font_file()
    {
        foreach (string project in new[] { "UnlimitedPDF", "UnlimitedPDF.Tests" })
        {
            var fonts = Directory.EnumerateFiles(RepositoryFile(project), "*.*", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc" or ".woff" or ".woff2");
            Assert.Empty(fonts);
        }
    }

    [Fact]
    public void The_page_and_the_readmes_say_what_custom_fonts_need()
    {
        string page = DemosFile("Pages/Playground/Languages/FontsAndLanguages.razor");
        Assert.Contains("<CollapsibleSection Title=\"Before you use your own fonts\" InitiallyOpen=\"false\">", page, StringComparison.Ordinal);
        foreach (string need in new[] { "The right to embed the font in a PDF.", "<code>IgnoreEmbeddingRestrictions</code>", "<code>PdfHtmlLoadOptions.Fonts</code>", "<code>FindMissingCharacters</code>", "Scripts: Latin, Devanagari" })
            Assert.Contains(need, page, StringComparison.Ordinal);

        foreach (string readme in new[] { "README.md", "nuget/README.md" })
        {
            string text = File.ReadAllText(RepositoryFile(readme));
            Assert.Contains("Before you start, you need:", text, StringComparison.Ordinal);
            Assert.Contains("The package includes no fonts; ship the font files with your app.", text, StringComparison.Ordinal);
        }
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative) => File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + relative));

    private static string FontsFile(string name) => RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/wwwroot/fonts/" + name);

    // The included fonts, loaded as the page loads them, in slot order.
    private static List<(FontScript Script, UploadedFont Font)> LoadAll() =>
        BundledFonts.All.Select(b => (b.Script, UploadedFontLoader.Load(b.FileName, File.ReadAllBytes(FontsFile(b.FileName))).Font! with { Bundled = b })).ToList();

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
