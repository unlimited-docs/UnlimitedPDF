namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Reads the version from a file's <c>%PDF-x.y</c> header (WR-035).
/// </summary>
internal static class PdfHeaderReader
{
    #region Fields and Constants

    /// <summary>
    /// How far into the file the header may start. Some producers write a few bytes before it.
    /// </summary>
    public const int SearchLength = 1024;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the header version, for example "1.7", or null when the file has no PDF header near its start.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <returns>The version text, or null.</returns>
    public static string? ReadVersion(byte[] data)
    {
        int index = PdfByteSearch.IndexOf(data, "%PDF-", 0, SearchLength + 5);
        if (index < 0) return null;

        int position = index + 5;
        int start = position;
        while (position < data.Length && data[position] >= '0' && data[position] <= '9') position++;
        if (position == start || position >= data.Length || data[position] != '.') return null;
        position++;
        int minor = position;
        while (position < data.Length && data[position] >= '0' && data[position] <= '9') position++;
        if (position == minor) return null;
        return Encoding.ASCII.GetString(data, start, position - start);
    }

    #endregion

    #endregion
}
