using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos home page: the capability rows, their code samples, the document gallery, and the "How do I...?"
/// questions read the demo list and real builders, so what the page says stays true (26111101 V-03 to V-10, PR-012).
/// </summary>
public class HomePageTests
{
    #region Fields and Constants

    private static readonly string[] Capabilities = ["Create", "Merge", "Split", "HTML to PDF"];

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Capability_categories_have_an_id_an_icon_a_sample_and_their_own_tone()
    {
        var categories = SampleCatalog.Categories;
        var capabilities = categories.Where(c => Capabilities.Contains(c.Title)).ToList();

        Assert.Equal(Capabilities, capabilities.Select(c => c.Title));
        Assert.Equal(new[] { "create", "merge", "split", "html-to-pdf" }, capabilities.Select(c => c.Id));
        Assert.Equal(new[] { "blue", "teal", "amber", "purple" }, capabilities.Select(c => c.Tone));
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
        Assert.All(SampleCatalog.Categories.Where(c => c.Title != "Merge"), c => Assert.Null(c.Groups));
    }

    [Fact]
    public void Each_capability_demo_has_its_own_icon_and_every_icon_is_drawn()
    {
        var capabilityDemos = CapabilityDemos().ToList();
        var drawn = Regex.Matches(File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/Icon.razor")), "case \"([a-z-]+)\":")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(15, capabilityDemos.Count);
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
        Assert.True(Assert.Single(PdfPageReader.Read(created.ToArray())).Shows("Hello, PDF!"));
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
    public void The_gallery_has_one_tile_per_document_in_catalogue_order()
    {
        var documents = SampleCatalog.Categories.Single(c => c.Id == "documents").Demos;

        Assert.Equal(documents, DocumentGallery.Documents.Select(d => d.Demo));
        Assert.Equal(documents.Count, DocumentGallery.Documents.Select(d => d.Picture).Distinct(StringComparer.Ordinal).Count());
        Assert.All(DocumentGallery.Documents, d => Assert.False(string.IsNullOrWhiteSpace(d.Summary)));
        Assert.All(DocumentGallery.Documents, d => Assert.Equal(d.Picture, d.Demo.Route.Split('/')[^1]));
    }

    [Fact]
    public void Each_tile_shows_the_page_count_and_size_of_the_document_its_demo_builds()
    {
        foreach (var entry in DocumentGallery.Documents)
        {
            var document = entry.Build(Images(entry.Images));
            var pdf = PdfPageReader.SaveBytes(document);

            Assert.True(entry.PageCount == PdfFile.FromBytes(pdf).PageCount, $"{entry.Demo.Title}: {PdfFile.FromBytes(pdf).PageCount} pages, tile says {entry.PageCount}");
            Assert.Equal(entry.PageSize, document.PageSize);
        }
    }

    [Fact]
    public void Each_gallery_builder_calls_its_sample_with_a_new_input()
    {
        // Guards the gallery's ten builders against drifting from the samples' own default inputs. That a download has
        // the bytes the demo page produces is checked in the browser (V-14), where the page builds it.
        foreach (var entry in DocumentGallery.Documents)
        {
            var images = Images(entry.Images);
            var expected = PdfPageReader.SaveBytes(DemoDefault(entry.Demo, images));

            Assert.True(expected.AsSpan().SequenceEqual(PdfPageReader.SaveBytes(entry.Build(images))), $"{entry.Demo.Title}: the bytes differ");
        }
    }

    [Fact]
    public void Each_gallery_document_has_a_tile_picture_and_a_quick_look_picture()
    {
        foreach (var entry in DocumentGallery.Documents)
        {
            foreach (var (suffix, width) in new[] { ("320", 320), ("960", 960) })
            {
                var path = RepositoryFile($"UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/{entry.Picture}-{suffix}.jpg");
                Assert.True(File.Exists(path), $"{entry.Demo.Title}: {Path.GetFileName(path)} is missing");
                Assert.Equal(width, PdfImage.FromBytes(File.ReadAllBytes(path)).PixelWidth);
            }

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
        var demos = SampleCatalog.Categories.SelectMany(c => c.Demos).ToList();
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
    public void The_capability_rows_and_the_gallery_together_hold_every_demo_once()
    {
        var onPage = CapabilityDemos().Concat(DocumentGallery.Documents.Select(d => d.Demo)).ToList();
        var all = SampleCatalog.Categories.SelectMany(c => c.Demos).ToList();

        Assert.Equal(all.Count, onPage.Count);
        Assert.Equal(all.ToHashSet(), onPage.ToHashSet());
    }

    [Fact]
    public void The_home_page_links_each_row_demo_and_each_tile_to_its_demo()
    {
        // The page renders a row per category with a sample and the gallery; the row links every demo (or every
        // group's demos, which are the same demos, checked above) and each tile links its document's demo.
        var home = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Pages/Home.razor"));
        var row = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/CapabilityRow.razor"));
        var tiles = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/DocumentTiles.razor"));

        Assert.Contains("SampleCatalog.Categories.Where(category => category.Snippet is not null)", home, StringComparison.Ordinal);
        Assert.Contains("<CapabilityRow Category=\"category\" />", home, StringComparison.Ordinal);
        Assert.Contains("<DocumentTiles />", home, StringComparison.Ordinal);
        Assert.Contains("@DemoLinks(group.Demos)", row, StringComparison.Ordinal);
        Assert.Contains("@DemoLinks(Category.Demos)", row, StringComparison.Ordinal);
        Assert.Contains("href=\"@demo.Route\"", row, StringComparison.Ordinal);
        Assert.Contains("DocumentGallery.Documents[index]", tiles, StringComparison.Ordinal);
        Assert.Contains("href=\"@document.Demo.Route\"", tiles, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_samples_the_page_shows_are_embedded_in_the_app()
    {
        // The page reads the samples from the app's embedded copy of HomeSnippets.cs (F-07); without the embed entry it
        // fails at run time while every other test passes.
        var project = System.Xml.Linq.XDocument.Load(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/UnlimitedPDF.Demos.BlazorWasm.csproj"));
        var row = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/Components/CapabilityRow.razor"));

        Assert.Contains(project.Descendants("EmbeddedResource"), e =>
            (string?)e.Attribute("Include") == @"Samples\HomeSnippets.cs" && (string?)e.Attribute("LogicalName") == "Samples.HomeSnippets.cs");
        Assert.Contains("SampleSource.Read(\"HomeSnippets.cs\")", row, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static IEnumerable<SampleDemo> CapabilityDemos() =>
        SampleCatalog.Categories.Where(c => Capabilities.Contains(c.Title)).SelectMany(c => c.Demos);

    // The sample called directly with a new input, as its demo page does on first load.
    private static PdfDocument DemoDefault(SampleDemo demo, SampleImages images) => demo.Title switch
    {
        "Invoice" => InvoiceSample.Build(new InvoiceInput(), images),
        "Quotation" => QuotationSample.Build(new QuotationInput(), images),
        "Purchase order" => PurchaseOrderSample.Build(new PurchaseOrderInput(), images),
        "Delivery note" => DeliveryNoteSample.Build(new DeliveryNoteInput(), images),
        "Account statement" => AccountStatementSample.Build(new AccountStatementInput(), images),
        "Payslip" => PayslipSample.Build(new PayslipInput(), images),
        "Medical report" => MedicalReportSample.Build(new MedicalReportInput(), images),
        "Receipt" => ReceiptSample.Build(new ReceiptInput(), images),
        "Certificate" => CertificateSample.Build(new CertificateInput(), images),
        "Employee directory" => EmployeeDirectorySample.Build(new EmployeeDirectoryInput()),
        _ => throw new ArgumentOutOfRangeException(nameof(demo), demo.Title, "Not a gallery document.")
    };

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
