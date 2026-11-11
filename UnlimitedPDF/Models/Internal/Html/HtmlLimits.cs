namespace UnlimitedPDF.Models.Internal.Html;

/// <summary>
/// The limits one HTML load uses (WR-038, A06), captured at load: per-call values win over the process-wide defaults.
/// </summary>
/// <param name="MaxInputSize">The largest input in bytes.</param>
/// <param name="MaxNestingDepth">The deepest element nesting.</param>
/// <param name="MaxResourceSize">The largest single resource in bytes.</param>
/// <param name="MaxTotalResourceSize">The most resource bytes in total.</param>
internal sealed record HtmlLimits(long MaxInputSize, int MaxNestingDepth, long MaxResourceSize, long MaxTotalResourceSize)
{
    /// <summary>
    /// Returns the limits for a load.
    /// </summary>
    /// <param name="options">The load options, or null.</param>
    /// <returns>The limits.</returns>
    public static HtmlLimits For(PdfHtmlLoadOptions? options) => new(
        options?.MaxInputSize ?? PdfHtml.DefaultMaxInputSize,
        options?.MaxNestingDepth ?? PdfHtml.DefaultMaxNestingDepth,
        options?.MaxResourceSize ?? PdfHtml.DefaultMaxResourceSize,
        options?.MaxTotalResourceSize ?? PdfHtml.DefaultMaxTotalResourceSize);

    /// <summary>
    /// Throws when an input is over the input size limit.
    /// </summary>
    /// <param name="size">The input size in bytes.</param>
    /// <exception cref="PdfHtmlException">The input is too large.</exception>
    public void CheckInput(long size)
    {
        if (size > MaxInputSize)
            throw new PdfHtmlException(PdfHtmlErrorReason.InputTooLarge,
                string.Format(CultureInfo.InvariantCulture, "The HTML input is larger than the limit of {0} bytes.", MaxInputSize));
    }
}
