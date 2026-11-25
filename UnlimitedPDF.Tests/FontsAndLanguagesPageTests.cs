using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The Fonts and languages page and its place on the site (CR-002, AC-17, AC-18, AC-20, AC-23; V-23, V-24, V-27): the
/// Languages category and its home-page sample, the code card that follows the drawing mode, the font slots with their
/// notes and free-font links, and the listings the site checks share with every demo.
/// </summary>
public class FontsAndLanguagesPageTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_languages_category_holds_the_demo_after_html_to_pdf()
    {
        var categories = SampleCatalog.Categories;
        var languages = categories.Single(c => c.Id == "languages");

        Assert.Equal("Languages", languages.Title);
        Assert.Equal(categories.Single(c => c.Id == "html-to-pdf"), categories[categories.ToList().IndexOf(languages) - 1]);
        Assert.Equal(new[] { SampleCatalog.FontsAndLanguages, SampleCatalog.RightToLeftDocuments }, languages.Demos); // CR-001
        Assert.Equal("playground/languages/fonts-and-languages", SampleCatalog.FontsAndLanguages.Route);
        Assert.Equal(("language", "green", "languages"), (languages.Icon, languages.Tone, languages.Snippet));
        Assert.Contains("24 languages, from Hindi and Tamil to Arabic, Urdu, Hebrew, Greek, and Russian", SampleCatalog.FontsAndLanguages.Description, StringComparison.Ordinal);
        Assert.Contains("@page \"/playground/languages/fonts-and-languages\"", DemosFile("Pages/Playground/Languages/FontsAndLanguages.razor"), StringComparison.Ordinal);
        Assert.Contains(".tone-green {", DemosFile("wwwroot/css/app.css"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_home_page_sample_draws_text_in_two_scripts_with_a_loaded_font()
    {
        var builder = new TestFontBuilder("Telugu Test");
        builder.MapCharacters("Hello, నమస్తే!");
        using var output = new MemoryStream();

        HomeSnippets.Languages(builder.Build(), output);

        PdfStructureChecker.AssertValid(output.ToArray());
        Assert.Equal("Hello, నమస్తే!", Assert.Single(PdfInspector.Read(output.ToArray()).PageText()));
    }

    [Fact]
    public void The_code_tab_shows_the_program_of_the_chosen_drawing_mode_and_the_download_names_the_language()
    {
        string page = DemosFile("Pages/Playground/Languages/FontsAndLanguages.razor");
        string frame = DemosFile("Components/SamplePage.razor.cs");

        Assert.Contains("SourceFile=\"@(_input.Mode == DrawMode.Html ? \"FontsAndLanguagesHtmlSample.cs\" : \"FontsAndLanguagesSample.cs\")\"", page, StringComparison.Ordinal);
        Assert.Contains("FileName=\"@($\"fonts-and-languages-{_input.Language}.pdf\")\"", page, StringComparison.Ordinal);
        // The code tab shows the complete console program of the chosen mode's builder (QF-26112501).
        Assert.Contains("string program = SampleSource.Read(ShownProgramFile);", frame, StringComparison.Ordinal);
        Assert.Contains("ProgramFile ?? SampleDemo.ProgramFileOf(SourceFile ?? Demo.SourceFile);", frame, StringComparison.Ordinal);
        Assert.Contains("await _preview.ShowAsync(pdfBytes, ShownFileName);", frame, StringComparison.Ordinal);

        // Both builders are compiled into the site; both modes' programs are embedded for the code tab.
        Assert.Contains("<EmbeddedResource Include=\"Programs\\*Program.cs\"", DemosFile("UnlimitedPDF.Demos.BlazorWasm.csproj"), StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Samples/FontsAndLanguagesHtmlSample.cs")));
        Assert.True(File.Exists(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Programs/FontsAndLanguagesHtmlProgram.cs")));
    }

    [Fact]
    public void The_fonts_section_says_fonts_stay_in_the_browser_and_links_free_fonts_safely()
    {
        string page = DemosFile("Pages/Playground/Languages/FontsAndLanguages.razor");
        string slot = DemosFile("Components/FontSlot.razor");

        Assert.Contains("Your fonts stay in your browser; nothing is uploaded.", page, StringComparison.Ordinal);
        Assert.Contains("allowed to embed", page, StringComparison.Ordinal);
        Assert.Contains("check its license", page, StringComparison.Ordinal);
        Assert.Contains("@foreach (var script in FontSlots.Scripts)", page, StringComparison.Ordinal);
        Assert.Contains("href=\"@FontSlots.FreeFontLink(Script)\" target=\"_blank\" rel=\"noopener noreferrer\"", slot, StringComparison.Ordinal);
        Assert.Contains(">Get a free one ", slot, StringComparison.Ordinal);
        Assert.Contains("I have the right to embed this font", slot, StringComparison.Ordinal);
        Assert.Contains("accept=\".ttf,.otf,font/ttf,font/otf\"", slot, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_wires_the_missing_note_options_and_warnings_to_the_library()
    {
        string page = DemosFile("Pages/Playground/Languages/FontsAndLanguages.razor");

        Assert.Contains("FontsAndLanguagesSample.MissingCharacters(_input, fonts)", page, StringComparison.Ordinal);
        Assert.Contains("Add a font to the @slot slot.", page, StringComparison.Ordinal);
        Assert.Contains("@bind=\"_input.FailOnMissingCharacters\"", page, StringComparison.Ordinal);
        Assert.Contains("@bind=\"_input.SynthesizeMissingStyles\"", page, StringComparison.Ordinal);
        Assert.Contains("@foreach (var warning in _html.Warnings.Take(MaxWarningsShown))", page, StringComparison.Ordinal);
        Assert.Contains("And @(_html.Warnings.Count - MaxWarningsShown) more.", page, StringComparison.Ordinal);
        Assert.Contains("UploadedFontLoader.AtWeight(s.Upload, _input.HeadingWeight)", page, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"@if \(_variable is \{ \} range\)"), page);
    }

    [Fact]
    public void The_faq_answers_on_own_fonts_and_languages_open_the_demo()
    {
        foreach (string id in new[] { "own-fonts", "languages" })
            Assert.Same(SampleCatalog.FontsAndLanguages, FaqQuestions.Questions.Single(q => q.Id == id).Demo);
    }

    [Fact]
    public void The_readme_demos_paragraph_names_the_languages_category()
    {
        string readme = File.ReadAllText(RepositoryFile("README.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = readme.IndexOf("## Demos\n", StringComparison.Ordinal);
        string demos = readme.Substring(start, readme.IndexOf("\n## ", start + 3, StringComparison.Ordinal) - start);

        Assert.Contains("Languages (Fonts and languages, with text calls or HTML)", demos, StringComparison.Ordinal);
        Assert.Contains("Hindi, Bengali, Gujarati, Punjabi, Odia, Kannada, Malayalam, Telugu, Tamil, French, Polish, Czech, Turkish, Filipino, Greek, Russian, Ukrainian, Armenian, Georgian, Arabic, Persian, Urdu, Hebrew, Dhivehi, or a mix, drawn with free fonts included for the demo or fonts you upload", demos, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative) => File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + relative));

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
