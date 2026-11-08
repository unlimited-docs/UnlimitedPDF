namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Finds ASCII markers in file bytes.
/// </summary>
internal static class PdfByteSearch
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the first offset of <paramref name="marker"/> in <paramref name="data"/> between
    /// <paramref name="start"/> and <paramref name="end"/>, or -1.
    /// </summary>
    /// <param name="data">The bytes to search.</param>
    /// <param name="marker">The ASCII marker, for example "%PDF-".</param>
    /// <param name="start">The first offset to try.</param>
    /// <param name="end">The offset the marker must end before.</param>
    /// <returns>The offset, or -1 when the marker is not found.</returns>
    public static int IndexOf(byte[] data, string marker, int start, int end)
    {
        var bytes = Encoding.ASCII.GetBytes(marker);
        end = Math.Min(end, data.Length);
        if (start < 0 || end - start < bytes.Length) return -1;
        int index = data.AsSpan(start, end - start).IndexOf(bytes);
        return index < 0 ? -1 : start + index;
    }

    /// <summary>
    /// Returns the last offset of <paramref name="marker"/> in <paramref name="data"/>, or -1.
    /// </summary>
    /// <param name="data">The bytes to search.</param>
    /// <param name="marker">The ASCII marker, for example "startxref".</param>
    /// <returns>The offset, or -1 when the marker is not found.</returns>
    public static int LastIndexOf(byte[] data, string marker) => data.AsSpan().LastIndexOf(Encoding.ASCII.GetBytes(marker));

    #endregion

    #endregion
}
