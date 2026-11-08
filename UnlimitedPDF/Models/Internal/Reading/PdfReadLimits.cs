namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// The limits one load applies (WR-035, A06), captured when the load starts.
/// </summary>
/// <param name="MaxFileSize">The most bytes the input may have.</param>
/// <param name="MaxStreamSize">The most bytes one stream may decompress to.</param>
/// <param name="MaxObjectCount">The most objects the input may list.</param>
internal sealed record PdfReadLimits(long MaxFileSize, long MaxStreamSize, int MaxObjectCount)
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the built-in defaults: 500 MB, 100 MB, and 5,000,000 objects (1 MB = 1,048,576 bytes).
    /// </summary>
    public static PdfReadLimits Default { get; } = new(524_288_000, 104_857_600, 5_000_000);

    #endregion
}
