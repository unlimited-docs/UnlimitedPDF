using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos home page: the capability rows, their code samples, the scenario cards, and the "How do I...?"
/// questions read the demo list and real builders, so what the page says stays true (26111101 V-03 to V-10, PR-012).
/// </summary>
public class HomePageTests
{
    #region Fields and Constants

    private static readonly string[] Capabilities = ["Create", "Merge", "Split and extract", "HTML to PDF", "Languages"];

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Capability_categories_have_an_id_an_icon_a_sample_and_their_own_tone()
    {
        var categories = SampleCatalog.Categories;
        var capabilities = categories.Where(c => Capabilities.Contains(c.Title)).ToList();

        Assert.Equal(Capabilities, capabilities.Select(c => c.Title));
        Assert.Equal(new[] { "create", "merge", "split", "html-to-pdf", "languages" }, capabilities.Select(c => c.Id));
        Assert.Equal(new[] { "blue", "teal", "amber", "purple", "green" }, capabilities.Select(c => c.Tone));
        Assert.All(capabilities, c => Assert.False(string.IsNullOrEmpty(c.Icon)));
        Assert.All(capabilities, c => Assert.False(string.IsNullOrEmpty(c.Snippet)));
        Assert.Equal("documents", categories.Single(c => c.Title == "Real-life documents").Id);
        Assert.Equal(categories.Count, categories.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Merge_shows_its_demos_as_operations_and_packs_in_catalogue_order()
    {
        var merge = SampleCatalog.Categories.Single(c => c.Title == "Merge");

        Assert.NotNull(merge.Groups);
        Assert.Equal(new[] { "Operations", "Packs" }, merge.Groups.Select(g => g.Title));
        Assert.Equal(new[] { "Join files", "Cover + ranges", "Pick pages", "Bookmarks & numbers" }, merge.Groups[0].Demos.Select(d => d.Title));
        Assert.Equal(merge.Demos, merge.Groups.SelectMany(g => g.Demos));
        // Split and extract groups its cards the same way (D-018); no other tab does.
        var split = SampleCatalog.Categories.Single(c => c.Id == "split");
        Assert.Equal(split.Demos, split.Groups!.SelectMany(g => g.Demos));
        Assert.All(SampleCatalog.Categories.Where(c => c.Id is not ("merge" or "split")), c => Assert.Null(c.Groups));
    }

    [Fact]
    public void Each_capability_demo_has_its_own_icon_and_every_icon_is_drawn()
    {
        var capabilityDemos = CapabilityDemos().ToList();
        var drawn = Regex.Matches(File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/Icon.razor")), "case \"([a-z-]+)\":")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(17, capabilityDemos.Count);
        Assert.Equal(capabilityDemos.Count, capabilityDemos.Select(d => d.Icon).Distinct(StringComparer.Ordinal).Count());
        Assert.All(SampleCatalog.Categories.SelectMany(c => c.Demos), d => Assert.Contains(d.Icon, drawn));
        Assert.All(SampleCatalog.Categories.Where(c => c.Icon is not null), c => Assert.Contains(c.Icon!, drawn));
    }

    [Fact]
    public void Each_code_sample_is_two_to_four_lines_of_the_compiled_file()
    {
        var source = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Samples/HomeSnippets.cs"));

        foreach (var category in SampleCatalog.Categories.Where(c => c.Snippet is not null))
        {
            var lines = SnippetText.Extract(source, category.Snippet!).Split('\n');
            Assert.InRange(lines.Length, 2, 4);
            Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line), $"{category.Title}: blank line"));
            Assert.All(lines, line => Assert.False(line.StartsWith(' '), $"{category.Title}: indented '{line}'"));
            Assert.DoesNotContain(lines, line => line.Contains("// ", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Snippet_text_reads_one_sample_and_reports_a_missing_one()
    {
        const string Source = "class C\n{\n    void M()\n    {\n        // snippet:a\n        var x = 1;\n            Use(x);\n        // end\n    }\n}";

        Assert.Equal("var x = 1;\n    Use(x);", SnippetText.Extract(Source, "a"));
        Assert.Equal("var x = 1;\n    Use(x);", SnippetText.Extract(Source.Replace("\n", "\r\n", StringComparison.Ordinal), "a"));
        Assert.Contains("'b'", Assert.Throws<InvalidOperationException>(() => SnippetText.Extract(Source, "b")).Message, StringComparison.Ordinal);
        Assert.Contains("no end marker", Assert.Throws<InvalidOperationException>(() => SnippetText.Extract("// snippet:a\nx();", "a")).Message, StringComparison.Ordinal);
        Assert.Contains("empty", Assert.Throws<InvalidOperationException>(() => SnippetText.Extract("// snippet:a\n\n// end", "a")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_create_and_html_samples_write_a_page_with_their_text()
    {
        using var created = new MemoryStream();
        HomeSnippets.Create(created);
        using var converted = new MemoryStream();
        HomeSnippets.Html(converted);

        PdfStructureChecker.AssertValid(created.ToArray());
        PdfStructureChecker.AssertValid(converted.ToArray());
        Assert.True(Assert.Single(PdfPageReader.Read(created.ToArray())).Shows("Hello, table!"));
        Assert.Single(PdfPageReader.Read(converted.ToArray()));
        var text = string.Join(" ", ImportedPagesTests.PageTexts(PdfFile.FromBytes(converted.ToArray())));
        Assert.Contains("Hello", text, StringComparison.Ordinal);
        Assert.Contains("PDF", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_merge_sample_joins_both_files_in_order()
    {
        using var output = new MemoryStream();
        HomeSnippets.Merge(ReaderFixtureBuilder.DocumentBytes(2), ReaderFixtureBuilder.DocumentBytes(3), output);

        Assert.Equal(new[] { "Page 1", "Page 2", "Page 1", "Page 2", "Page 3" }, ImportedPagesTests.PageTexts(PdfFile.FromBytes(output.ToArray())));
    }

    [Fact]
    public void The_split_sample_writes_parts_of_two_pages()
    {
        var folder = Path.Combine(Path.GetTempPath(), "unlimitedpdf-home-split-" + Guid.NewGuid().ToString("N"));
        try
        {
            HomeSnippets.Split(ReaderFixtureBuilder.DocumentBytes(5), folder);

            var parts = Directory.GetFiles(folder).OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(3, parts.Count);
            Assert.Equal(new[] { 2, 2, 1 }, parts.Select(p => PdfFile.FromBytes(File.ReadAllBytes(p)).PageCount));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Each_document_picture_belongs_to_one_document_demo()
    {
        // The pictures of the scenario cards: document demos and the types the Invoice demo makes, one picture each.
        // A newer document demo without a captured picture shows its capability's icon instead.
        var documents = SampleCatalog.Categories.Single(c => c.Id == "documents").Demos.Concat(SampleCatalog.InvoiceTypes).ToHashSet();
        var tiles = DocumentGallery.Documents.Select(d => d.Demo).ToList();

        Assert.All(tiles, demo => Assert.Contains(demo, documents));
        Assert.Equal(tiles.Count, tiles.Distinct().Count());
        Assert.Equal(tiles.Count, DocumentGallery.Documents.Select(d => d.Picture).Distinct(StringComparer.Ordinal).Count());
        Assert.All(DocumentGallery.Documents, d => Assert.Equal(d.Picture, Path.GetFileNameWithoutExtension(d.Demo.FileName)));
    }

    [Fact]
    public void Each_card_picture_is_320_pixels_wide_and_no_larger_copy_is_shipped()
    {
        // The cards show 320 px copies; the larger quick look copies were removed with the quick look (T-13).
        foreach (var demo in ScenarioGallery.Scenarios)
        {
            var path = RepositoryFile($"UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/{ScenarioGallery.PictureOf(demo)}-320.jpg");
            Assert.True(File.Exists(path), $"{demo.Title}: {Path.GetFileName(path)} is missing");
            Assert.Equal(320, PdfImage.FromBytes(File.ReadAllBytes(path)).PixelWidth);
        }

        Assert.Empty(Directory.GetFiles(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery"), "*-960.jpg"));
        foreach (var entry in DocumentGallery.Documents)
        {

            Assert.True(File.Exists(RepositoryFile($"assets/originals/gallery/{entry.Picture}.png")), $"{entry.Demo.Title}: its original is missing");
        }
    }

    [Fact]
    public void Each_gallery_picture_was_captured_from_the_current_sources_and_images_of_its_demo()
    {
        foreach (var (entry, record) in GalleryRecords())
        {
            var sources = record.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToList();

            Assert.True(
                record.GetProperty("sha256").GetString() == CaptureHash(sources, entry.Images),
                $"{entry.Picture}: the demo's sources or images changed since its pictures were captured. Capture "
                + $"assets/originals/gallery/{entry.Picture}.png again from the demo, run assets/New-DerivedImages.ps1, and update the hash in "
                + "UnlimitedPDF.Tests/Fixtures/document-gallery.json.");
        }
    }

    [Fact]
    public void Each_gallery_record_lists_the_sample_and_every_input_type_it_uses()
    {
        // The sample file, then every input file whose type it names, and every one those name in turn (F-05).
        var inputs = Directory.GetFiles(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Samples/Inputs"), "*.cs").Select(Path.GetFileNameWithoutExtension).ToList();
        foreach (var (entry, record) in GalleryRecords())
        {
            var used = new List<string> { $"Samples/{entry.Demo.SourceFile}" };
            for (int i = 0; i < used.Count; i++)
            {
                var text = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + used[i]));
                foreach (var input in inputs.Where(name => Regex.IsMatch(text, $@"\b{name}\b")))
                    if (!used.Contains($"Samples/Inputs/{input}.cs")) used.Add($"Samples/Inputs/{input}.cs");
            }

            var recorded = record.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToList();
            Assert.Equal(entry.Images, record.GetProperty("images").EnumerateArray().Select(i => i.GetString()!));
            Assert.True(
                used.Order(StringComparer.Ordinal).SequenceEqual(recorded.Order(StringComparer.Ordinal)),
                $"{entry.Picture}: the record lists {string.Join(", ", recorded)}; the sample uses {string.Join(", ", used)}.");
        }
    }

    [Fact]
    public void There_are_six_to_eight_questions_and_each_opens_a_demo_that_exists()
    {
        var demos = SampleCatalog.Pages;
        var questions = HomeQuestions.Questions;

        Assert.InRange(questions.Count, 6, 8);
        Assert.Equal(questions.Count, questions.Select(q => q.Text).Distinct(StringComparer.Ordinal).Count());
        Assert.All(questions, q => Assert.Contains(q.Demo, demos));
        foreach (var question in questions.Where(q => q.Route is not null))
        {
            Assert.StartsWith(question.Demo.Route + "/", question.Href, StringComparison.Ordinal);
            Assert.NotNull(HtmlScenarios.Find(question.Href[(question.Demo.Route.Length + 1)..]));
        }
    }

    [Fact]
    public void The_documents_cards_and_the_capability_rows_hold_every_demo_once()
    {
        // D-026, QF-26112201 F-1: the real-life documents as cards and the other categories as rows, so every demo is
        // one click from the home page and none is shown twice.
        var documents = SampleCatalog.Categories.Single(c => c.Id == "documents").Demos;
        var rows = SampleCatalog.Categories.Where(c => c.Id != "documents").SelectMany(c => c.Demos).ToList();
        var shown = documents.Concat(rows).ToList();

        Assert.Equal(ScenarioGallery.Scenarios.Count, shown.Count);
        Assert.Equal(ScenarioGallery.Scenarios.ToHashSet(), shown.ToHashSet());
    }

    [Fact]
    public void Each_capability_row_links_the_free_tools_for_its_demos_and_each_tool_sits_in_one_row()
    {
        // QF-26112201 F-5: a row shows the tools that do its demos' work on the visitor's own files.
        var tools = SampleCatalog.Categories.ToDictionary(c => c.Id, c => c.Demos.Select(ToolCatalog.ForScenario).OfType<PdfTool>().Distinct().Select(t => t.Slug).ToList());

        Assert.Equal(["images-to-pdf"], tools["create"]);
        Assert.Equal(["merge"], tools["merge"]);
        Assert.Equal(["split", "extract-pages", "remove-pages"], tools["split"]);
        Assert.Equal(["html-to-pdf"], tools["html-to-pdf"]);
        Assert.Empty(tools["languages"]);
        Assert.Equal(ToolCatalog.All.Select(t => t.Slug).Order(), tools.Values.SelectMany(t => t).Order());

        var row = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/CapabilityRow.razor"));
        Assert.Contains("Category.Demos.Select(ToolCatalog.ForScenario)", row, StringComparison.Ordinal);
        Assert.Contains("<a class=\"cap-tool\" href=\"@tool.Route\">", row, StringComparison.Ordinal);
        Assert.DoesNotContain("Try your own", SampleCatalog.Categories.Single(c => c.Id == "merge").Description, StringComparison.Ordinal);
        Assert.DoesNotContain("Try your own", SampleCatalog.Categories.Single(c => c.Id == "split").Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_footer_has_four_columns_and_every_link_opens_a_page_that_exists()
    {
        // QF-26112201: C# guides, free tools, languages, and resources, on every page, in the words people search with.
        // The languages, the longest list, are one wide column with their links in two columns.
        Assert.Equal(["C# guides", ToolCatalog.HubHeading, "Languages", "Resources"], FooterLinks.Columns.Select(c => c.Title));
        Assert.Equal(["Languages"], FooterLinks.Columns.Where(c => c.IsWide).Select(c => c.Title));
        var pages = SampleCatalog.Pages.Select(d => d.Route)
            .Concat(ToolCatalog.All.Select(t => t.Route))
            .Concat([ToolCatalog.HubRoute, ScenarioGallery.Route, "faq", "getting-started"]).ToHashSet();
        var links = FooterLinks.Columns.SelectMany(c => c.Links).ToList();

        Assert.Equal(links.Count, links.Select(l => l.Href).Distinct().Count());
        Assert.All(links.Where(l => !l.External), link => Assert.Contains(link.Href.Split('?')[0], pages));
        Assert.All(links.Where(l => l.External), link => Assert.StartsWith("https://", link.Href, StringComparison.Ordinal));
        Assert.Equal(LanguageInvoices.All.Select(FooterLinks.LanguageRoute), FooterLinks.Columns.Single(c => c.Title == "Languages").Links.Take(LanguageInvoices.All.Count).Select(l => l.Href));

        var layout = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Layout/MainLayout.razor"));
        Assert.Contains("@foreach (var column in FooterLinks.Columns)", layout, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\"> (opens in a new tab)</span>", layout, StringComparison.Ordinal);
        var languages = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/Playground/Languages/FontsAndLanguages.razor"));
        Assert.Contains("[SupplyParameterFromQuery(Name = \"language\")]", languages, StringComparison.Ordinal);
    }

    [Fact]
    public void The_home_page_links_each_row_demo_and_each_popular_card_to_its_demo()
    {
        // A guard against removal: the page renders every capability row and the popular scenarios as cards, a row
        // links its demos, and a card links its demo. That the rows hold every demo is checked above.
        var home = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/Home.razor"));
        var row = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/CapabilityRow.razor"));
        var card = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/ScenarioCard.razor"));

        Assert.Contains("<CapabilityRow ", home, StringComparison.Ordinal);
        Assert.Contains("private static IEnumerable<SampleCategory> Capabilities => SampleCatalog.Categories.Where(c => c != Documents);", home, StringComparison.Ordinal);
        Assert.Contains("@foreach (var demo in Documents.Demos)", home, StringComparison.Ordinal);
        Assert.Contains("<li><a href=\"#@category.Id\">@category.Title</a></li>", home, StringComparison.Ordinal);
        Assert.Contains("<ScenarioCard Demo=\"demo\" HeadingLevel=\"3\" />", home, StringComparison.Ordinal);
        // The row calls its link fragment for a category's demos and for each group's (F-23): the link attribute alone
        // also sits in the fragment's definition, so it would pass a row that never shows its links.
        Assert.Contains(".Route\"", row, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(row, @"@DemoLinks\(\w+\.Demos\)").Count);
        Assert.Equal(2, Regex.Matches(card, "<a class=\"scenario-link\" href=\"@Demo.Route\">").Count);

        // The approved order ("Two paths", QF-26112201): hero and illustration, the developer path with the install
        // command and the first PDF, the tools path, documents, capabilities, benefits, How do I, FAQ band. The install
        // command is only in the developer path.
        Assert.Single(Regex.Matches(home, "<InstallCommand />"));
        string[] order = ["class=\"hero\"", "class=\"hero-actions\"", "class=\"showcase\"", "For developers", "<InstallCommand />", "first-pdf-code", "For your files", "Documents.Demos", "<CapabilityRow ", "class=\"benefits\"", "id=\"how-do-i\"", "class=\"faq-band\""];
        var positions = order.Select(marker => home.IndexOf(marker, StringComparison.Ordinal)).ToList();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.Order(), positions);
        Assert.Contains("No engine required", home, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_pdf_and_documents_samples_run_as_shown()
    {
        string folder = Path.Combine(Path.GetTempPath(), "unlimitedpdf-first-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            HomeSnippets.First(folder);
            Assert.Equal(1, PdfFile.FromBytes(File.ReadAllBytes(Path.Combine(folder, "hello.pdf"))).PageCount);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        using var stream = new MemoryStream();
        HomeSnippets.Documents(stream);
        var texts = ImportedPagesTests.PageTexts(PdfFile.FromBytes(stream.ToArray()));
        Assert.True(texts.Length > 1);
        Assert.All(texts, text => Assert.Contains("Amount", text, StringComparison.Ordinal));
    }

    [Fact]
    public void The_home_page_says_what_is_true_and_names_every_new_tab()
    {
        // PR-012 and WCAG 2.4.4: the claims match the library, the picture is described as the illustration it is,
        // the headings keep their levels, and every link that opens a new tab says so.
        var home = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/Home.razor"));
        var card = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/ScenarioCard.razor"));
        var project = File.ReadAllText(RepositoryFile("UnlimitedPDF/UnlimitedPDF.csproj"));

        Assert.Contains("No engine required: it runs wherever .NET 6 or later runs", home, StringComparison.Ordinal);
        Assert.Matches("alt=\"Stylized illustration titled UnlimitedPDF Demos[^\"]*certificate cards\"", home);
        Assert.DoesNotContain("made with UnlimitedPDF", home, StringComparison.Ordinal);
        Assert.Contains("<CodeView Source=\"@_firstPdf\"", home, StringComparison.Ordinal);
        Assert.Contains("SnippetText.Extract(SampleSource.Read(\"HomeSnippets.cs\"), \"first\")", home, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(home, "<h2 id=\"[a-z-]+-title\" class=\"section-title").Count + Regex.Matches(home, "<h2 id=\"faq-band-title\">").Count);
        Assert.Equal(2, Regex.Matches(home, "<h2 id=\"[a-z-]+-title\" class=\"home-path-title\">").Count);
        Assert.Contains("<h3 class=\"scenario-title\">", card, StringComparison.Ordinal);
        string license = Regex.Match(project, "<PackageLicenseExpression>([^<]+)</PackageLicenseExpression>").Groups[1].Value;
        Assert.Contains($"(\"Free and open\", \"{license} licensed", home, StringComparison.Ordinal);
        Assert.Contains("net6.0", project, StringComparison.Ordinal);

        foreach (var file in new[] { "Pages/Home.razor", "Pages/NotFound.razor", "Layout/MainLayout.razor" })
        {
            var text = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + file));
            foreach (Match link in Regex.Matches(text, "<a [^>]*target=\"_blank\"[^>]*>(?<body>.*?)</a>", RegexOptions.Singleline))
            {
                bool footer = link.Value.Contains("unlimited-docs\"", StringComparison.Ordinal) || link.Value.Contains("contributors", StringComparison.Ordinal);
                bool version = link.Value.Contains("version-link", StringComparison.Ordinal);
                if (!footer && !version) Assert.Contains("opens in a new tab", link.Groups["body"].Value, StringComparison.Ordinal);
            }
        }

        Assert.Contains("<SiteSearchBox InputId=\"not-found-search\" />", File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/NotFound.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_samples_the_page_shows_are_embedded_in_the_app()
    {
        // The page reads the samples from the app's embedded copy of HomeSnippets.cs (F-07); without the embed entry it
        // fails at run time while every other test passes.
        var project = System.Xml.Linq.XDocument.Load(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/UnlimitedPDF.Demos.BlazorWasm.csproj"));
        var row = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/CapabilityRow.razor"));

        // Either path separator is accepted; the logical name is what the page asks for.
        Assert.Contains(project.Descendants("EmbeddedResource"), e =>
            ((string?)e.Attribute("Include"))?.Replace('/', '\\') == @"Samples\HomeSnippets.cs" && (string?)e.Attribute("LogicalName") == "Samples.HomeSnippets.cs");
        Assert.Contains("\"HomeSnippets.cs\"", row, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static IEnumerable<SampleDemo> CapabilityDemos() =>
        SampleCatalog.Categories.Where(c => Capabilities.Contains(c.Title)).SelectMany(c => c.Demos);

    // Only the images a document names, loaded from the app's served copies, so the document is the one the demo builds
    // (a row's height follows its photo) and a missing name fails the build.
    private static SampleImages Images(IEnumerable<string> names) =>
        new(names.ToDictionary(name => name, name => PdfImage.FromBytes(File.ReadAllBytes(ServedImage(name)))));

    // SHA-256 over each source's path and text (line endings as LF, so a CRLF checkout gives the same hash), then each
    // served image's path and bytes, in the order DocumentGallery names them. The scheme is described in assets/ARTWORK.md.
    private static string CaptureHash(IEnumerable<string> sources, IEnumerable<string> images)
    {
        using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var source in sources)
        {
            var text = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + source)).Replace("\r\n", "\n", StringComparison.Ordinal);
            sha.AppendData(Encoding.UTF8.GetBytes(source + "\n" + text + "\n"));
        }

        foreach (var image in images)
        {
            sha.AppendData(Encoding.UTF8.GetBytes("wwwroot/images/" + image + "\n"));
            sha.AppendData(File.ReadAllBytes(ServedImage(image)));
        }

        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

    // Each gallery document with its record in Fixtures/document-gallery.json, after checking the two lists match.
    private static List<(GalleryDocument Entry, System.Text.Json.JsonElement Record)> GalleryRecords()
    {
        var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(RepositoryFile("UnlimitedPDF.Tests/Fixtures/document-gallery.json")));
        var recorded = manifest.RootElement.GetProperty("documents").EnumerateArray().ToList();
        Assert.Equal(DocumentGallery.Documents.Select(d => d.Picture), recorded.Select(r => r.GetProperty("picture").GetString()));
        return DocumentGallery.Documents.Zip(recorded).Select(pair => (pair.First, pair.Second)).ToList();
    }

    private static string ServedImage(string name) => RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/" + name);

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
