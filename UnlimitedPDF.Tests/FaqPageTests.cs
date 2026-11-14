using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos FAQ page: its topics, questions, demo links, code samples, and search read real code and the demo list, so
/// what the page says stays true (26111102 V-01 to V-06, PR-012).
/// </summary>
public class FaqPageTests
{
    #region Fields and Constants

    private static readonly string[] Topics =
    [
        "Getting started and platforms", "Text and fonts", "Tables and layout", "Images", "Merge and split", "HTML to PDF",
        "License and support"
    ];

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_question_is_marked_not_yet_exactly_when_its_answer_says_so()
    {
        Assert.All(FaqQuestions.Questions, q =>
            Assert.Equal(q.NotYet, q.Answer[0].StartsWith("Not yet.", StringComparison.Ordinal)));
        Assert.Contains(FaqQuestions.Questions, q => q.NotYet);
    }

    [Fact]
    public void Each_code_sample_is_two_to_eight_lines_of_the_compiled_file_and_every_sample_is_shown()
    {
        var source = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Samples/FaqSnippets.cs"));
        var named = FaqQuestions.Questions.Where(q => q.Snippet is not null).Select(q => q.Snippet!).ToList();

        Assert.Equal(named.Count, named.Distinct(StringComparer.Ordinal).Count());
        foreach (var name in named)
        {
            var lines = SnippetText.Extract(source, name).Split('\n');
            Assert.InRange(lines.Length, 2, 8);
        }

        var inFile = Regex.Matches(source, @"// snippet:([a-z0-9-]+)").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(inFile.OrderBy(n => n, StringComparer.Ordinal), named.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_answer_has_text_and_every_demo_link_opens_a_demo_that_exists()
    {
        var demos = SampleCatalog.Categories.SelectMany(c => c.Demos).ToList();

        Assert.All(FaqQuestions.Questions, q =>
        {
            Assert.NotEmpty(q.Answer);
            Assert.All(q.Answer, paragraph => Assert.False(string.IsNullOrWhiteSpace(paragraph)));
            if (q.Demo is not null) Assert.Contains(q.Demo, demos);
        });
    }

    [Fact]
    public void Questions_about_getting_started_or_the_license_have_no_code()
    {
        var plain = FaqQuestions.Groups.Where(g => g.Id is "getting-started" or "license-and-support").ToList();

        Assert.Equal(2, plain.Count);
        Assert.All(plain.SelectMany(g => g.Questions), q => Assert.Null(q.Snippet));
    }

    [Fact]
    public void Search_keeps_a_question_when_every_word_is_in_its_question_or_answer_ignoring_case()
    {
        var question = new FaqQuestion("q", "How do I merge PDFs?", ["Load each file as a PdfFile."]);

        Assert.True(FaqSearch.Matches(question, null));
        Assert.True(FaqSearch.Matches(question, ""));
        Assert.True(FaqSearch.Matches(question, "  \t "));
        Assert.True(FaqSearch.Matches(question, "merge"));
        Assert.True(FaqSearch.Matches(question, "MERGE"));
        Assert.True(FaqSearch.Matches(question, "pdffile"));
        Assert.True(FaqSearch.Matches(question, "file merge"));
        Assert.True(FaqSearch.Matches(question, "  merge   load "));
        Assert.False(FaqSearch.Matches(question, "watermark"));
        Assert.False(FaqSearch.Matches(question, "merge watermark"));
        Assert.Throws<ArgumentNullException>(() => FaqSearch.Matches(null!, "merge"));
    }

    [Fact]
    public void Search_finds_the_font_questions_on_the_page()
    {
        var found = FaqQuestions.Questions.Where(q => FaqSearch.Matches(q, "font")).Select(q => q.Id).ToList();

        Assert.Contains("own-fonts", found);
        Assert.Contains("linux-and-containers", found);
        Assert.DoesNotContain(FaqQuestions.Questions, q => FaqSearch.Matches(q, "watermark"));
    }

    [Fact]
    public void Search_words_split_on_white_space()
    {
        Assert.Empty(FaqSearch.Words(null));
        Assert.Empty(FaqSearch.Words(" \r\n"));
        Assert.Equal(new[] { "split", "pdf" }, FaqSearch.Words(" split\tpdf "));
    }

    [Fact]
    public void Text_outside_html_draws_western_characters_and_a_question_mark_for_other_scripts()
    {
        // The "question-marks" answer: without a caller font, AddText draws WinAnsi characters with the built-in
        // Helvetica (é is WinAnsi code 233, octal 351) and writes '?' for characters outside WinAnsi (WR-039).
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Café", 72, 720);
        page.AddText("త", 72, 700); // Telugu letter TA
        using var output = new MemoryStream();
        document.Save(output);

        var saved = Assert.Single(PdfPageReader.Read(output.ToArray()));
        Assert.True(saved.Shows(@"Caf\351"));
        Assert.True(saved.Shows("?"));
    }

    [Fact]
    public void The_code_samples_write_valid_pdfs()
    {
        foreach (var write in new Action<Stream>[]
                 {
                     FaqSnippets.CenterText, FaqSnippets.ConvertHtml, FaqSnippets.PageNumbers, FaqSnippets.RepeatHeader,
                     FaqSnippets.Units, FaqSnippets.WrapParagraph,
                     output => FaqSnippets.Image(Png(), output),
                     output => FaqSnippets.Merge(ReaderFixtureBuilder.DocumentBytes(2), ReaderFixtureBuilder.DocumentBytes(3), output),
                     output => FaqSnippets.PickPages(ReaderFixtureBuilder.DocumentBytes(5), output)
                 })
        {
            using var output = new MemoryStream();
            write(output);
            PdfStructureChecker.AssertValid(output.ToArray());
        }
    }

    [Fact]
    public void The_html_file_sample_converts_a_file_with_an_image_beside_it()
    {
        var folder = Path.Combine(Path.GetTempPath(), "unlimitedpdf-faq-html-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "logo.png"), Png());
            var path = Path.Combine(folder, "page.html");
            File.WriteAllText(path, "<h1>Report</h1><img src=\"logo.png\" width=\"40\">");
            using var output = new MemoryStream();

            FaqSnippets.HtmlFromFile(path, output);

            PdfStructureChecker.AssertValid(output.ToArray());
            Assert.Empty(PdfHtml.FromFile(path).Warnings);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_page_number_sample_fills_in_the_page_number_and_count()
    {
        using var output = new MemoryStream();
        FaqSnippets.PageNumbers(output);

        Assert.True(Assert.Single(PdfPageReader.Read(output.ToArray())).Shows("Page 1 of 1"));
    }

    [Fact]
    public void The_page_is_at_faq_with_its_title_and_heading_and_never_renders_markup()
    {
        // A guard against removal and against typed text or a link's fragment reaching the page as markup (A05).
        var page = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/Faq.razor"));
        var files = new[]
        {
            "Pages/Faq.razor", "Pages/Faq.razor.cs", "Components/FaqQuestionItem.razor", "Components/FaqQuestionItem.razor.cs"
        }.Select(f => File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + f)));

        Assert.StartsWith("@page \"/faq\"", page, StringComparison.Ordinal);
        Assert.Contains("<PageTitle>UnlimitedPDF FAQ: C# and .NET PDF library questions</PageTitle>", page, StringComparison.Ordinal);
        Assert.Contains("<h1>Frequently asked questions</h1>", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Topics\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", page, StringComparison.Ordinal);
        Assert.All(files, text => Assert.DoesNotContain("MarkupString", text, StringComparison.Ordinal));
    }

    [Fact]
    public void The_top_bar_and_the_home_page_link_to_the_faq()
    {
        var layout = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Layout/MainLayout.razor"));
        var home = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/Home.razor"));

        int playground = layout.IndexOf("href=\"playground\"", StringComparison.Ordinal);
        int faq = layout.IndexOf("<NavLink class=\"topnav-link\" href=\"faq\"", StringComparison.Ordinal);
        int github = layout.IndexOf("topnav-github", StringComparison.Ordinal);
        Assert.True(playground >= 0 && playground < faq && faq < github, "The FAQ link sits between Playground and GitHub.");

        int questions = home.IndexOf("HomeQuestions.Questions", StringComparison.Ordinal);
        int more = home.IndexOf("<a class=\"questions-more\" href=\"faq\">More questions in the FAQ", StringComparison.Ordinal);
        int facts = home.IndexOf("class=\"facts\"", StringComparison.Ordinal);
        Assert.True(questions >= 0 && questions < more && more < facts, "The FAQ link ends the \"How do I...?\" block.");
    }

    [Fact]
    public void The_pick_pages_and_merge_samples_take_the_pages_they_name()
    {
        using var picked = new MemoryStream();
        FaqSnippets.PickPages(ReaderFixtureBuilder.DocumentBytes(5), picked);
        using var merged = new MemoryStream();
        FaqSnippets.Merge(ReaderFixtureBuilder.DocumentBytes(2), ReaderFixtureBuilder.DocumentBytes(3), merged);

        Assert.Equal(new[] { "Page 2", "Page 3", "Page 4" }, ImportedPagesTests.PageTexts(PdfFile.FromBytes(picked.ToArray())));
        Assert.Equal(5, PdfFile.FromBytes(merged.ToArray()).PageCount);
    }

    [Fact]
    public void The_repeat_header_sample_draws_its_header_on_every_page()
    {
        using var output = new MemoryStream();
        FaqSnippets.RepeatHeader(output);

        var pages = PdfPageReader.Read(output.ToArray());
        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.True(page.Shows("Name")));
    }

    [Fact]
    public void The_split_sample_writes_parts_of_two_pages()
    {
        var folder = Path.Combine(Path.GetTempPath(), "unlimitedpdf-faq-split-" + Guid.NewGuid().ToString("N"));
        try
        {
            FaqSnippets.Split(ReaderFixtureBuilder.DocumentBytes(5), folder);

            var parts = Directory.GetFiles(folder).OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(new[] { 2, 2, 1 }, parts.Select(p => PdfFile.FromBytes(File.ReadAllBytes(p)).PageCount));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_topics_are_the_seven_groups_in_order_with_twenty_to_thirty_questions()
    {
        var groups = FaqQuestions.Groups;
        var questions = FaqQuestions.Questions.ToList();

        Assert.Equal(Topics, groups.Select(g => g.Title));
        Assert.All(groups, g => Assert.True(g.Questions.Count >= 2, g.Title));
        Assert.InRange(questions.Count, 20, 30);
        Assert.Equal(questions.Count, questions.Select(q => q.Text).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Topic_and_question_link_names_are_unique_kebab_case()
    {
        var ids = FaqQuestions.Groups.Select(g => g.Id).Concat(FaqQuestions.Questions.Select(q => q.Id)).ToList();

        Assert.All(ids, id => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", id));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    #endregion

    #region Private Methods

    private static byte[] Png() => new PngBuilder { ColorType = 6, Width = 5, Height = 5 }.Build();

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
