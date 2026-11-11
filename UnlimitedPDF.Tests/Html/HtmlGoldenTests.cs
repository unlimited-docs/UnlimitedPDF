using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Real-life HTML documents (D-005): each converts with no warning, stays byte-identical across cultures and runs, and
/// matches its golden file (WR-038, AC-26, V-21).
/// </summary>
public class HtmlGoldenTests
{
    #region Methods

    #region Public Methods

    public static IEnumerable<object[]> Documents() => new[]
    {
        new object[] { "html-invoice" },
        new object[] { "html-statement" },
        new object[] { "html-report" },
        new object[] { "html-newsletter" },
        new object[] { "html-letter" },
        new object[] { "html-resume" }
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void Real_life_documents_match_their_golden_files(string name)
    {
        byte[] actual;
        using (new CultureScope(""))
            actual = Convert(name, out var warnings, out _);
        PdfStructureChecker.AssertValid(actual);
        GoldenFiles.CaptureIfRequested(name, actual);
        var path = GoldenFiles.PathFor(name);
        Assert.True(File.Exists(path), $"Golden file '{path}' is missing. Capture it with {GoldenFiles.CaptureVariable}=1.");
        Assert.Equal(File.ReadAllBytes(path), actual);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Real_life_documents_use_only_the_supported_subset_and_are_deterministic(string name)
    {
        byte[] first, second, german;
        IReadOnlyList<PdfHtmlWarning> warnings;
        int pages;
        using (new CultureScope("")) first = Convert(name, out warnings, out pages);
        using (new CultureScope("")) second = Convert(name, out _, out _);
        using (new CultureScope("de-DE")) german = Convert(name, out _, out _);
        Assert.True(warnings.Count == 0, string.Join("; ", warnings.Select(w => w.Kind + " " + w.Source + " " + w.Message)));
        Assert.Equal(first, second);
        Assert.Equal(first, german);
        Assert.True(name != "html-statement" || pages >= 2, "The statement runs over several pages.");
    }

    [Fact]
    public void Manual_check_pack_is_written_when_requested()
    {
        // V-25: set UNLIMITEDPDF_VISUAL_CHECK_DIR to a folder to get each document's HTML, its images, and its PDF, for
        // the six viewer targets (WR-025) and the side-by-side check against the browser's print preview.
        string? folder = Environment.GetEnvironmentVariable(VisualCheckDocument.OutputVariable);
        if (string.IsNullOrEmpty(folder)) return;

        string pack = Path.Combine(folder, "html-to-pdf");
        Directory.CreateDirectory(pack);
        File.WriteAllBytes(Path.Combine(pack, "logo.png"), new PngBuilder { ColorType = 6, Width = 60, Height = 20 }.Build());
        File.WriteAllBytes(Path.Combine(pack, "banner.png"), new PngBuilder { ColorType = 2, Width = 80, Height = 20 }.Build());
        foreach (var name in Documents().Select(d => (string)d[0]))
        {
            string source = Source(name);
            File.WriteAllText(Path.Combine(pack, name + ".html"), source, new UTF8Encoding(false));
            var html = PdfHtml.FromFile(Path.Combine(pack, name + ".html"));
            PdfOperations.ConvertHtml(html, Path.Combine(pack, name + ".pdf"), new PdfHtmlConvertOptions { HeadingBookmarks = name == "html-report" });
            Assert.Empty(html.Warnings);
        }
    }

    #endregion

    #region Internal Methods

    internal static string Source(string name) => name switch
    {
        "html-invoice" => HtmlTemplates.Invoice,
        "html-statement" => HtmlTemplates.Statement(),
        "html-report" => HtmlTemplates.Report,
        "html-newsletter" => HtmlTemplates.Newsletter,
        "html-letter" => HtmlTemplates.Letter,
        _ => HtmlTemplates.Resume
    };

    internal static byte[] Convert(string name, out IReadOnlyList<PdfHtmlWarning> warnings, out int pages)
    {
        var html = PdfHtml.FromString(Source(name), new PdfHtmlLoadOptions { ResourceResolver = Images, FailOnWarning = false });
        using var stream = new MemoryStream();
        PdfOperations.ConvertHtml(html, stream, new PdfHtmlConvertOptions { HeadingBookmarks = name == "html-report" });
        warnings = html.Warnings;
        byte[] bytes = stream.ToArray();
        pages = PdfPageReader.Read(bytes).Count;
        return bytes;
    }

    // Project-authored images generated in code (PR-011). JPEG data is embedded unchanged, so the bytes are the same on
    // every runtime; PNG data is recompressed with the runtime's zlib, whose output differs between .NET versions.
    internal static Stream? Images(string reference) => reference switch
    {
        "logo.png" => new MemoryStream(new JpegBuilder { Width = 60, Height = 20 }.Build()),
        "banner.png" => new MemoryStream(new JpegBuilder { Width = 80, Height = 20 }.Build()),
        _ => null
    };

    #endregion

    #endregion
}
