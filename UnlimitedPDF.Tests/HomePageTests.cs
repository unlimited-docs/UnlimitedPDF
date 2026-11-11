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
    public void Each_gallery_download_has_the_bytes_of_its_demo_with_the_default_input()
    {
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
    public void Each_gallery_picture_was_captured_from_the_current_sources_of_its_demo()
    {
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(RepositoryFile("UnlimitedPDF.Tests/Fixtures/document-gallery.json")));
        var recorded = manifest.RootElement.GetProperty("documents").EnumerateArray().ToList();

        Assert.Equal(DocumentGallery.Documents.Select(d => d.Picture), recorded.Select(r => r.GetProperty("picture").GetString()));
        foreach (var record in recorded)
        {
            var sources = record.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToList();
            var picture = record.GetProperty("picture").GetString();

            Assert.True(
                record.GetProperty("sha256").GetString() == SourceHash(sources),
                $"{picture}: the demo's sources changed since its pictures were captured. Capture assets/originals/gallery/{picture}.png again from the demo, "
                + "run assets/New-DerivedImages.ps1, and update the hash in UnlimitedPDF.Tests/Fixtures/document-gallery.json.");
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
    public void Every_demo_is_one_click_from_the_home_page()
    {
        var onPage = CapabilityDemos().Concat(DocumentGallery.Documents.Select(d => d.Demo)).ToList();
        var all = SampleCatalog.Categories.SelectMany(c => c.Demos).ToList();

        Assert.Equal(all.Count, onPage.Count);
        Assert.Equal(all.ToHashSet(), onPage.ToHashSet());
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

    // Only the images a document names, as small generated JPEGs (PR-011), so a missing name fails the build.
    private static SampleImages Images(IEnumerable<string> names)
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 120, Height = 60 }.Build());
        return new SampleImages(names.ToDictionary(name => name, _ => image));
    }

    // SHA-256 over each source's path and text, line endings as LF, so a CRLF checkout gives the same hash.
    private static string SourceHash(IEnumerable<string> sources)
    {
        using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var source in sources)
        {
            var text = File.ReadAllText(RepositoryFile("UnlimitedPDF.Demos.BlazorWasm/" + source)).Replace("\r\n", "\n", StringComparison.Ordinal);
            sha.AppendData(Encoding.UTF8.GetBytes(source + "\n" + text + "\n"));
        }

        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
