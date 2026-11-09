using System.IO.Compression;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The "Split PDFs" example in README.md and nuget/README.md compiles and runs as written, and both readmes show the
/// same code (PR-012, AC-16).
/// </summary>
public sealed class ReadmeSplitExampleTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"readme-split-{Guid.NewGuid():N}");

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Readme_split_sample_runs_as_written()
    {
        // The readme's code, with its relative paths resolved under a temporary folder.
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "report.pdf"), ReaderFixtureBuilder.DocumentBytes(10));
        string At(string path) => Path.Combine(_root, path);

        var report = PdfFile.FromFile(At("report.pdf"));

        // One file per page, into a folder: pages/report-01.pdf, pages/report-02.pdf, ...
        PdfOperations.Split(report, 1, At("pages"));

        // One file per page range, in the order given. Ranges may overlap and leave pages out.
        PdfOperations.Split(report, new[] { new PdfPageRange(1, 3), new PdfPageRange(8, 10) }, At("chapters"));

        // Or write each part to a stream, for example a zip entry. Streams are left open, and a zip writes one entry at a
        // time, so close each entry when the next part asks for its stream.
        using (var zip = new ZipArchive(File.Create(At("report-parts.zip")), ZipArchiveMode.Create))
        {
            Stream? entry = null;
            PdfOperations.Split(report, 4, part =>
            {
                entry?.Dispose();
                return entry = zip.CreateEntry(part.FileName).Open();
            });
            entry?.Dispose();
        }

        Assert.Equal(Enumerable.Range(1, 10).Select(n => $"report-{n:00}.pdf"), Directory.GetFiles(At("pages")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "report-1.pdf", "report-2.pdf" }, Directory.GetFiles(At("chapters")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        using var read = ZipFile.OpenRead(At("report-parts.zip"));
        Assert.Equal(new[] { "report-1.pdf", "report-2.pdf", "report-3.pdf" }, read.Entries.Select(e => e.FullName));
    }

    [Fact]
    public void Both_readmes_show_the_same_split_section_and_list_split_in_1_0_0()
    {
        string readme = Section(File.ReadAllText(RepositoryFile("README.md")));
        string nuget = Section(File.ReadAllText(RepositoryFile(Path.Combine("nuget", "README.md"))));

        Assert.Equal(readme, nuget);
        Assert.Contains("PdfOperations.Split(report, 1, \"pages\");", readme, StringComparison.Ordinal);
        Assert.Contains("entry?.Dispose();", readme, StringComparison.Ordinal);
        foreach (var file in new[] { "README.md", Path.Combine("nuget", "README.md") })
        {
            string text = File.ReadAllText(RepositoryFile(file));
            Assert.Contains("- New: split PDFs (`PdfOperations.Split`", text, StringComparison.Ordinal);
            Assert.True(text.IndexOf("## Merge PDFs", StringComparison.Ordinal) < text.IndexOf("## Split PDFs", StringComparison.Ordinal), file);
        }
    }

    #endregion

    #region Private Methods

    private static string RepositoryFile(string relative, [System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    private static string Section(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = text.IndexOf("## Split PDFs", StringComparison.Ordinal);
        int end = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0, "The Split PDFs section is missing.");
        return text[start..end];
    }

    #endregion

    #endregion
}
