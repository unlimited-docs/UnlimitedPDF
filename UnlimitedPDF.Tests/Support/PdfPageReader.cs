using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Reads the pages of a PDF the library wrote, in <c>/Kids</c> order: each page's media box and its content stream
/// text. Relies on the library's own uncompressed output, not on a general PDF parser.
/// </summary>
internal static class PdfPageReader
{
    #region Fields and Constants

    private static readonly Regex ContentsPattern = new(@"/Contents (\d+) 0 R", RegexOptions.CultureInvariant);
    private static readonly Regex KidsPattern = new(@"/Kids \[([^\]]*)\]", RegexOptions.CultureInvariant);
    private static readonly Regex MediaBoxPattern = new(@"/MediaBox (\[[^\]]*\])", RegexOptions.CultureInvariant);
    private static readonly Regex ObjectPattern = new(@"(?m)^(\d+) 0 obj\r?\n(.*?)endobj", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex ReferencePattern = new(@"(\d+) 0 R", RegexOptions.CultureInvariant);
    private static readonly Regex StreamPattern = new(@"stream\r?\n(.*?)\r?\nendstream", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the document's pages in reading order.
    /// </summary>
    /// <param name="pdf">The complete PDF file bytes.</param>
    /// <returns>Each page's media box and content stream text.</returns>
    public static IReadOnlyList<Page> Read(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var objects = ObjectPattern.Matches(text).ToDictionary(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), m => m.Groups[2].Value);

        var kids = KidsPattern.Match(objects[2]).Groups[1].Value;
        return ReferencePattern.Matches(kids)
            .Select(m => objects[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)])
            .Select(page =>
            {
                int contents = int.Parse(ContentsPattern.Match(page).Groups[1].Value, CultureInfo.InvariantCulture);
                var stream = StreamPattern.Match(objects[contents]);
                return new Page(MediaBoxPattern.Match(page).Groups[1].Value, stream.Success ? stream.Groups[1].Value : "");
            })
            .ToList();
    }

    /// <summary>
    /// Saves <paramref name="document"/> to memory and returns its pages in reading order.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>Each page's media box and content stream text.</returns>
    public static IReadOnlyList<Page> Save(PdfDocument document) => Read(SaveBytes(document));

    /// <summary>
    /// Saves <paramref name="document"/> to memory, checks its structure, and returns the bytes.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] SaveBytes(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        var bytes = stream.ToArray();
        PdfStructureChecker.AssertValid(bytes);
        return bytes;
    }

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// One page: its media box and its content stream text.
    /// </summary>
    /// <param name="MediaBox">The media box, for example "[0 0 595.28 841.89]".</param>
    /// <param name="Content">The content stream text.</param>
    internal sealed record Page(string MediaBox, string Content)
    {
        /// <summary>
        /// Returns whether the page draws the text <paramref name="text"/> with a <c>Tj</c> operator.
        /// </summary>
        public bool Shows(string text) => Content.Contains("(" + text + ") Tj", StringComparison.Ordinal);

        /// <summary>
        /// Returns the filled rectangles the page draws, as (x, y, width, height), in drawing order.
        /// </summary>
        public IReadOnlyList<(double X, double Y, double Width, double Height)> FilledRectangles() =>
            Regex.Matches(Content, @"(?m)^(\S+) (\S+) (\S+) (\S+) re f\r?$")
                .Select(m => (Number(m.Groups[1].Value), Number(m.Groups[2].Value), Number(m.Groups[3].Value), Number(m.Groups[4].Value)))
                .ToList();

        private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    }

    #endregion
}
