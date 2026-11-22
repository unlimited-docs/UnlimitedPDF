using System.Globalization;
using System.Text;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// The C# a tool shows: the compiled sample in <c>Samples/ToolSnippets.cs</c>, with the visitor's file names and
/// choices put in as escaped literals. With the default values each method returns that sample's text exactly, and tests
/// keep the two in step (PR-012). Names are shown as text, never run (OWASP A05).
/// </summary>
public static class ToolCode
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the Extract pages code.
    /// </summary>
    /// <param name="file">The PDF's file name.</param>
    /// <param name="pages">The pages, as typed.</param>
    /// <param name="output">The result's file name.</param>
    /// <returns>The code.</returns>
    public static string ExtractPages(string file = "contract.pdf", string pages = "1, 3-5", string output = "contract-extract.pdf") =>
        Lines(
            $"var file = await PdfFile.FromFileAsync({Literal(file)});",
            $"await PdfOperations.ExtractAsync(file, PdfPageRange.Parse({Literal(pages.Trim())}), {Literal(output)});");

    /// <summary>
    /// Returns the HTML to PDF code.
    /// </summary>
    /// <param name="htmlFile">The HTML file's name.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="output">The result's file name.</param>
    /// <returns>The code.</returns>
    public static string HtmlToPdf(string htmlFile = "invoice.html", PdfPageSize pageSize = PdfPageSize.A4, string output = "invoice.pdf") =>
        Lines(
            $"var html = await PdfHtml.FromFileAsync({Literal(htmlFile)}, new PdfHtmlLoadOptions {{ BaseFolder = \".\" }});",
            $"await PdfOperations.ConvertHtmlAsync(html, {Literal(output)}, new PdfHtmlConvertOptions {{ PageSize = PdfPageSize.{pageSize} }});");

    /// <summary>
    /// Returns the Images to PDF code.
    /// </summary>
    /// <param name="pictures">The pictures' file names, in page order; null for the sample's.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="fit">How each picture fills its page.</param>
    /// <param name="output">The result's file name.</param>
    /// <returns>The code.</returns>
    public static string ImagesToPdf(
        IReadOnlyList<string>? pictures = null,
        PdfPageSize pageSize = PdfPageSize.A4,
        PictureFit fit = PictureFit.InsideMargins,
        string output = "pictures.pdf")
    {
        pictures ??= ["receipt-1.jpg", "receipt-2.jpg", "id-card.png"];
        var (width, height) = pageSize.ToDimensions();
        double margin = fit == PictureFit.InsideMargins ? ToolOperations.PictureMargin : 0;
        return Lines(
            $"var document = new PdfDocument(PdfPageSize.{pageSize});",
            $"foreach (var path in new[] {{ {string.Join(", ", pictures.Select(Literal))} }})",
            "{",
            $"    document.AddPage().AddImage(PdfImage.FromFile(path), {Number(margin)}, {Number(margin)}, {Number(width - (2 * margin))}, {Number(height - (2 * margin))},",
            "        new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });",
            "}",
            $"await document.SaveAsync({Literal(output)});");
    }

    /// <summary>
    /// Returns a C# string literal for <paramref name="text"/>, with quotes, backslashes, and control characters escaped.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The literal, quotes included.</returns>
    public static string Literal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length + 2).Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\0': builder.Append("\\0"); break;
                default:
                    if (char.IsControl(c) || char.IsSurrogate(c) || c is (char)0x2028 or (char)0x2029)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>
    /// Returns the Merge PDFs code.
    /// </summary>
    /// <param name="files">The PDFs' file names, in order; null for the sample's.</param>
    /// <param name="output">The result's file name.</param>
    /// <returns>The code.</returns>
    public static string Merge(IReadOnlyList<string>? files = null, string output = "merged.pdf")
    {
        files ??= ["q1-report.pdf", "q2-report.pdf"];
        var lines = new List<string> { "var files = new List<PdfFile>", "{" };
        lines.AddRange(files.Select(file => $"    await PdfFile.FromFileAsync({Literal(file)}),"));
        lines.Add("};");
        lines.Add($"await PdfOperations.MergeAsync({Literal(output)}, files);");
        return Lines([.. lines]);
    }

    /// <summary>
    /// Returns the Remove pages code.
    /// </summary>
    /// <param name="file">The PDF's file name.</param>
    /// <param name="pages">The pages to remove, as typed.</param>
    /// <param name="output">The result's file name.</param>
    /// <returns>The code.</returns>
    public static string RemovePages(string file = "scan.pdf", string pages = "2, 6", string output = "scan-trimmed.pdf") =>
        Lines(
            $"var file = await PdfFile.FromFileAsync({Literal(file)});",
            $"await PdfOperations.RemoveAsync(file, PdfPageRange.Parse({Literal(pages.Trim())}), {Literal(output)});");

    /// <summary>
    /// Returns the Split PDF code for every N pages.
    /// </summary>
    /// <param name="file">The PDF's file name.</param>
    /// <param name="pagesPerPart">The pages in each part.</param>
    /// <returns>The code.</returns>
    public static string SplitEvery(string file = "handbook.pdf", int pagesPerPart = 4) =>
        Lines(
            $"var file = await PdfFile.FromFileAsync({Literal(file)});",
            $"await PdfOperations.SplitAsync(file, {pagesPerPart.ToString(CultureInfo.InvariantCulture)}, \"parts\");");

    /// <summary>
    /// Returns the Split PDF code for page ranges.
    /// </summary>
    /// <param name="file">The PDF's file name.</param>
    /// <param name="ranges">The ranges, as typed.</param>
    /// <returns>The code.</returns>
    public static string SplitRanges(string file = "handbook.pdf", string ranges = "1-2, 3-7") =>
        Lines(
            $"var file = await PdfFile.FromFileAsync({Literal(file)});",
            $"await PdfOperations.SplitAsync(file, PdfPageRange.Parse({Literal(ranges.Trim())}), \"parts\");");

    #endregion

    #region Private Methods

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    #endregion

    #endregion
}
