namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Returns a saved PDF as text for tests that search the whole file, with the streams the library compresses (page
/// content, <c>ToUnicode</c> maps, and other <c>/FlateDecode</c> streams without a subtype or decode parameters)
/// unpacked in place (WR-042). Everything else stays as written, so a search finds the same text whether or not the
/// file was saved with <see cref="PdfDocument.CompressStreams"/>. Font programs and images keep their encoded bytes,
/// so their data never reads as page text.
/// </summary>
internal static class PdfText
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns <paramref name="pdf"/> as Latin-1 text with its compressed library streams unpacked.
    /// </summary>
    /// <param name="pdf">The complete PDF file bytes.</param>
    /// <returns>The file text.</returns>
    public static string Of(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var (objects, _) = PdfStructureChecker.ReadObjects(pdf);
        var inspector = PdfInspector.Read(pdf);

        // Each stream's data, in file order: where it starts, how long it is, and its unpacked bytes.
        var spans = new List<(int Start, int Length, byte[] Unpacked)>();
        foreach (var (number, (dictionary, stream)) in objects)
        {
            if (stream is null || !IsUnpacked(dictionary)) continue;
            // The line break keeps "4 0 obj" from matching inside "14 0 obj".
            var header = $"\n{number} 0 obj{dictionary}";
            int at = text.IndexOf(header, StringComparison.Ordinal);
            Assert.True(at >= 0, $"Stream object {number} was not found.");
            int start = at + header.Length + "stream".Length;
            start += text[start] == '\r' ? 2 : 1;
            spans.Add((start, stream.Length, inspector.StreamData(number)));
        }

        var result = new StringBuilder(text.Length);
        int position = 0;
        foreach (var (start, length, unpacked) in spans.OrderBy(s => s.Start))
        {
            result.Append(text, position, start - position).Append(Encoding.Latin1.GetString(unpacked));
            position = start + length;
        }
        return result.Append(text, position, text.Length - position).ToString();
    }

    #endregion

    #region Private Methods

    private static bool IsUnpacked(string dictionary) =>
        dictionary.Contains("/Filter /FlateDecode", StringComparison.Ordinal)
        && !dictionary.Contains("/Subtype", StringComparison.Ordinal)
        && !dictionary.Contains("/Length1", StringComparison.Ordinal)
        && !dictionary.Contains("/DecodeParms", StringComparison.Ordinal)
        && !dictionary.Contains("/Type", StringComparison.Ordinal);

    #endregion

    #endregion
}
