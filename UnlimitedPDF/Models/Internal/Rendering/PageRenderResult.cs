namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// One page drawn as SVG, or the reason it is not drawn.
/// </summary>
/// <param name="PageNumber">The 1-based page number.</param>
/// <param name="Width">The page width in points, after any page rotation.</param>
/// <param name="Height">The page height in points, after any page rotation.</param>
/// <param name="Svg">The page as one SVG element; null when the page is not drawn.</param>
/// <param name="Reason">Why the page is not drawn; null when it is drawn.</param>
/// <param name="TextLines">The page's text, one line per text object, as the characters the PDF maps its glyphs to;
/// empty when the page is not drawn.</param>
/// <param name="Counts">What the drawing holds, for checks.</param>
internal sealed record PageRenderResult(
    int PageNumber,
    double Width,
    double Height,
    string? Svg,
    string? Reason,
    IReadOnlyList<string> TextLines,
    PageRenderCounts Counts)
{
    #region Properties, Indexers

    /// <summary>
    /// Gets whether the page is drawn.
    /// </summary>
    public bool IsDrawn => Svg is not null;

    #endregion
}

/// <summary>
/// The number of each kind of item a drawn page holds.
/// </summary>
/// <param name="Paths">Filled, stroked, or clipping paths.</param>
/// <param name="Glyphs">Glyphs shown.</param>
/// <param name="Images">Images drawn.</param>
/// <param name="Links">Links made clickable.</param>
internal sealed record PageRenderCounts(int Paths, int Glyphs, int Images, int Links)
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the counts of a page that is not drawn.
    /// </summary>
    public static PageRenderCounts None { get; } = new(0, 0, 0, 0);

    #endregion
}
