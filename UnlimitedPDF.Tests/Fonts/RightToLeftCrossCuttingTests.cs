using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Text;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Cross-cutting checks of right-to-left support (WR-041; V-08, V-13, V-16, V-18): an 80,000-character line mixing
/// Arabic, Hebrew, Latin, numbers, brackets, nested embeddings and isolates, and unmatched terminators lays out within
/// the #45 time bound through the whole pipeline and keeps its text; right-to-left documents save to identical bytes
/// twice; the Unicode tables are compiled in, so laying out text reads no file.
/// </summary>
public class RightToLeftCrossCuttingTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void A_long_mixed_line_lays_out_within_the_time_bound_and_keeps_its_text()
    {
        const string letters = "سلامبی שלום abc 12()[]";
        var font = RightToLeftScriptTests.Font(letters);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 5_000; i++) sb.Append(i % 2 == 0 ? '‫' : '⁦');
        while (sb.Length < 70_000) sb.Append("سلام بی (abc 12) שלום [x] ");
        sb.Append(new string('⁩', 5_000)).Append(new string('‬', 5_000));
        string line = sb.ToString(0, 80_000);
        TextPipeline.Layout("سلام (a) ש", font, Array.Empty<PdfFont>(), 12);
        var clock = Stopwatch.StartNew();

        var layout = TextPipeline.Layout(line, font, Array.Empty<PdfFont>(), 12)!;

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
        Assert.Equal(line, layout.Text);
        Assert.True(layout.Reordered);
    }

    [Fact]
    public void A_right_to_left_document_saves_to_the_same_bytes_twice()
    {
        byte[] Save()
        {
            var font = RightToLeftScriptTests.Font("سلامבי שלום abc 12");
            var document = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
            document.Header.Height = 30;
            document.Header.AddText("שלום 12", font, 0, 0);
            document.Body.AddText("سلام abc 12", font);
            var table = document.Body.AddTable(300);
            table.SetText(0, 0, "שלום", font);
            table.SetText(0, 1, "abc", font);
            var options = new PdfHtmlLoadOptions();
            options.Fonts.Add("Test", font);
            document.Body.AddHtml(PdfHtml.FromString("<p dir='rtl' style='font-family: Test'>שלום <bdi>abc 12</bdi></p>", options));
            return PdfPageReader.SaveBytes(document);
        }

        var first = Save();
        var second = Save();

        PdfStructureChecker.AssertValid(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Laying_out_right_to_left_text_reads_no_file()
    {
        // The bidirectional and joining tables are generated C# (PR-005, PR-018): nothing under the text pipeline opens
        // a file or a stream.
        string folder = Path.Combine(BidiDataTestsRoot(), "UnlimitedPDF", "Models", "Internal", "Text");
        foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain("File.", source);
            Assert.DoesNotContain("FileStream", source);
            Assert.DoesNotContain("HttpClient", source);
        }
    }

    #endregion

    #region Private Methods

    private static string BidiDataTestsRoot([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    #endregion

    #endregion
}
