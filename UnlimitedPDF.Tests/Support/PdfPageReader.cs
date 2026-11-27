using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Reads the pages of a PDF the library wrote, in page-tree order: each page's media box and its content stream
/// text. Objects are found through the cross-reference table, and a content stream with <c>/Filter /FlateDecode</c>
/// is unpacked first, so the text is the same whether or not the library compressed the stream.
/// </summary>
internal static class PdfPageReader
{
    #region Fields and Constants

    private static readonly Regex ContentsPattern = new(@"/Contents (\d+) 0 R", RegexOptions.CultureInvariant);
    private static readonly Regex MediaBoxPattern = new(@"/MediaBox (\[[^\]]*\])", RegexOptions.CultureInvariant);

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
        var inspector = PdfInspector.Read(pdf);
        return inspector.Pages()
            .Select(number =>
            {
                var page = inspector.DictionaryText(number);
                int contents = int.Parse(ContentsPattern.Match(page).Groups[1].Value, CultureInfo.InvariantCulture);
                var content = inspector.HasStream(contents) ? Encoding.Latin1.GetString(inspector.StreamData(contents)) : "";
                return new Page(MediaBoxPattern.Match(page).Groups[1].Value, content);
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

        /// <summary>
        /// Returns the clip rectangles the page sets with <c>re W n</c>, as (x, y, width, height), in drawing order.
        /// </summary>
        public IReadOnlyList<(double X, double Y, double Width, double Height)> ClipRectangles() =>
            Regex.Matches(Content, @"(\S+) (\S+) (\S+) (\S+) re W n")
                .Select(m => (Number(m.Groups[1].Value), Number(m.Groups[2].Value), Number(m.Groups[3].Value), Number(m.Groups[4].Value)))
                .ToList();

        /// <summary>
        /// Returns the rectangles drawn by the page layout API (pages, bands, and flow), as (x, y, width, height), in
        /// drawing order.
        /// </summary>
        public IReadOnlyList<(double X, double Y, double Width, double Height)> LayoutRectangles() =>
            Regex.Matches(Content, @"q \S+ \S+ \S+ rg (\S+) (\S+) (\S+) (\S+) re f Q")
                .Select(m => (Number(m.Groups[1].Value), Number(m.Groups[2].Value), Number(m.Groups[3].Value), Number(m.Groups[4].Value)))
                .ToList();

        /// <summary>
        /// Returns the text lines drawn by the page layout API with their position: the x-coordinate where the text
        /// starts and its baseline, in drawing order.
        /// </summary>
        public IReadOnlyList<(string Text, double X, double Baseline)> LayoutTexts() =>
            Regex.Matches(Content, @"1 0 0 1 (\S+) (\S+) Tm \((.*?)\) Tj ET")
                .Select(m => (m.Groups[3].Value, Number(m.Groups[1].Value), Number(m.Groups[2].Value)))
                .ToList();

        private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    }

    #endregion
}
