using System.Runtime.CompilerServices;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The code each tool shows is its compiled sample (<c>ToolSnippets</c>, compiled here and in the site) with the
/// visitor's names and choices put in as escaped literals (26112201 AC-11; PR-012; OWASP A05).
/// </summary>
public class ToolCodeTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void With_the_default_values_each_tool_shows_its_compiled_sample_exactly()
    {
        string source = Snippets();

        Assert.Equal(SnippetText.Extract(source, "merge"), ToolCode.Merge());
        Assert.Equal(SnippetText.Extract(source, "images-to-pdf"), ToolCode.ImagesToPdf());
        Assert.Equal(SnippetText.Extract(source, "split-every"), ToolCode.SplitEvery());
        Assert.Equal(SnippetText.Extract(source, "split-ranges"), ToolCode.SplitRanges());
        Assert.Equal(SnippetText.Extract(source, "extract-pages"), ToolCode.ExtractPages());
        Assert.Equal(SnippetText.Extract(source, "remove-pages"), ToolCode.RemovePages());
        Assert.Equal(SnippetText.Extract(source, "html-to-pdf"), ToolCode.HtmlToPdf());
    }

    [Fact]
    public void The_visitors_files_and_choices_go_into_the_code()
    {
        string merge = ToolCode.Merge(["a.pdf", "b.pdf", "c.pdf"], "all.pdf");
        Assert.Equal(3, merge.Split('\n').Count(line => line.Contains("PdfFile.FromFileAsync", StringComparison.Ordinal)));
        Assert.Contains("MergeAsync(\"all.pdf\", files)", merge, StringComparison.Ordinal);

        string images = ToolCode.ImagesToPdf(["x.png"], PdfPageSize.Letter, PictureFit.EdgeToEdge, "p.pdf");
        Assert.Contains("new PdfDocument(PdfPageSize.Letter)", images, StringComparison.Ordinal);
        Assert.Contains("AddImage(PdfImage.FromFile(path), 0, 0, 612, 792,", images, StringComparison.Ordinal);

        Assert.Contains("SplitAsync(file, 10, \"parts\")", ToolCode.SplitEvery("h.pdf", 10), StringComparison.Ordinal);
        Assert.Contains("PdfPageRange.Parse(\"2-3\")", ToolCode.ExtractPages("c.pdf", " 2-3 ", "o.pdf"), StringComparison.Ordinal);
        Assert.Contains("PageSize = PdfPageSize.Letter", ToolCode.HtmlToPdf("page.html", PdfPageSize.Letter, "page.pdf"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plain.pdf", "\"plain.pdf\"")]
    [InlineData("say \"hi\".pdf", "\"say \\\"hi\\\".pdf\"")]
    [InlineData("C:\\a\\b.pdf", "\"C:\\\\a\\\\b.pdf\"")]
    [InlineData("line\nbreak\r\t\0", "\"line\\nbreak\\r\\t\\0\"")]
    [InlineData("bell\u0007", "\"bell\\u0007\"")]
    [InlineData("sep\u2028", "\"sep\\u2028\"")]
    [InlineData("रिपोर्ट.pdf", "\"रिपोर्ट.pdf\"")]
    public void Names_become_escaped_string_literals(string name, string expected)
    {
        Assert.Equal(expected, ToolCode.Literal(name));
    }

    [Fact]
    public void A_name_cannot_end_the_literal_and_add_code()
    {
        string code = ToolCode.Merge(["x.pdf\"); Process.Start(\"calc"], "m.pdf");

        Assert.Contains("FromFileAsync(\"x.pdf\\\"); Process.Start(\\\"calc\")", code, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string Snippets([CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", "Samples", "ToolSnippets.cs"));

    #endregion

    #endregion
}
