namespace UnlimitedPDF.Tests;

/// <summary>
/// The "Extract and remove pages" example in README.md and nuget/README.md compiles and runs as written, and both
/// readmes show the same section (PR-012, AC-17).
/// </summary>
public sealed class ReadmeExtractExampleTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"readme-extract-{Guid.NewGuid():N}");

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Readme_extract_sample_runs_as_written()
    {
        // The readme's code, with its relative paths resolved under a temporary folder.
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "report.pdf"), ReaderFixtureBuilder.DocumentBytes(10));
        string At(string path) => Path.Combine(_root, path);

        var report = PdfFile.FromFile(At("report.pdf"));

        // The pages you choose, in the order written: pages 10, 1, 2, and 3. A page can be chosen more than once.
        PdfOperations.Extract(report, PdfPageRange.Parse("10, 1-3"), At("summary.pdf"));

        // The PDF without the pages you choose; the pages left keep their order: pages 1 and 10.
        PdfOperations.Remove(report, PdfPageRange.Parse("2-9"), At("cover-and-sign-off.pdf"));

        // Or add the chosen pages to a document you build, under one bookmark.
        var pack = new PdfDocument();
        pack.AddPages(report, PdfPageRange.Parse("1, 10"), "Report");
        pack.Save(At("pack.pdf"));

        Assert.Equal(new[] { "Page 10", "Page 1", "Page 2", "Page 3" }, SplitFixtures.PageTexts(File.ReadAllBytes(At("summary.pdf"))));
        Assert.Equal(new[] { "Page 1", "Page 10" }, SplitFixtures.PageTexts(File.ReadAllBytes(At("cover-and-sign-off.pdf"))));
        Assert.Equal(new[] { "Page 1", "Page 10" }, SplitFixtures.PageTexts(File.ReadAllBytes(At("pack.pdf"))));
        Assert.Equal("Report", Assert.Single(SplitFixtures.Outline(File.ReadAllBytes(At("pack.pdf")))).Title);
    }

    [Fact]
    public void Both_readmes_show_the_same_extract_section_after_split()
    {
        string readme = Section(File.ReadAllText(RepositoryFile("README.md")));
        string nuget = Section(File.ReadAllText(RepositoryFile(Path.Combine("nuget", "README.md"))));

        Assert.Equal(readme, nuget);
        Assert.Contains("PdfOperations.Extract(report, PdfPageRange.Parse(\"10, 1-3\"), \"summary.pdf\");", readme, StringComparison.Ordinal);
        Assert.Contains("assets/derived/readme/extract.jpg", readme, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryFile(Path.Combine("assets", "derived", "readme", "extract.jpg"))));
        foreach (var file in new[] { "README.md", Path.Combine("nuget", "README.md") })
        {
            string text = File.ReadAllText(RepositoryFile(file));
            Assert.Contains("- [Extract and remove pages](#extract-and-remove-pages)", text, StringComparison.Ordinal);
            Assert.True(text.IndexOf("## Split PDFs", StringComparison.Ordinal) < text.IndexOf("## Extract and remove pages", StringComparison.Ordinal), file);
        }
    }

    #endregion

    #region Private Methods

    private static string RepositoryFile(string relative, [System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "..", relative);

    private static string Section(string readme)
    {
        string text = readme.Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = text.IndexOf("## Extract and remove pages", StringComparison.Ordinal);
        int end = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        return text[start..end];
    }

    #endregion

    #endregion
}
