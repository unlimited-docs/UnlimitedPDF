namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// The work one page may take while it is drawn. A page that reaches a bound is not drawn, so a hostile PDF cannot
/// hang or exhaust the browser it is previewed in (A06).
/// </summary>
/// <param name="MaxOperators">The most content operators run on a page, counting form XObjects.</param>
/// <param name="MaxPathSegments">The most path segments built on a page.</param>
/// <param name="MaxStateDepth">The deepest nesting of <c>q</c> and form XObjects.</param>
/// <param name="MaxFormDepth">The deepest nesting of form XObjects.</param>
/// <param name="MaxContentBytes">The most decoded content-stream bytes on a page.</param>
/// <param name="MaxImagePixels">The most image pixels on a page.</param>
/// <param name="MaxOutputCharacters">The longest drawing produced for a page.</param>
/// <param name="MaxOperands">The most operands one operator may have.</param>
/// <param name="MaxClipDepth">The deepest nesting of clipping paths.</param>
/// <param name="MaxGlyphs">The most glyphs shown on a page.</param>
/// <param name="MaxFonts">The most fonts read on a page.</param>
/// <param name="MaxFontTextCharacters">The most characters the fonts' text maps on a page may hold together.</param>
/// <param name="MaxGlyphComponents">The most composite glyph components expanded on a page.</param>
internal sealed record PageRenderLimits(
    int MaxOperators,
    int MaxPathSegments,
    int MaxStateDepth,
    int MaxFormDepth,
    long MaxContentBytes,
    long MaxImagePixels,
    int MaxOutputCharacters,
    int MaxOperands,
    int MaxClipDepth,
    int MaxGlyphs,
    int MaxFonts,
    int MaxFontTextCharacters,
    int MaxGlyphComponents)
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the limits used when none are given: far above any page the library writes, low enough to keep a
    /// browser responsive.
    /// </summary>
    public static PageRenderLimits Default { get; } = new(
        MaxOperators: 1_000_000,
        MaxPathSegments: 1_000_000,
        MaxStateDepth: 128,
        MaxFormDepth: 12,
        MaxContentBytes: 16L * 1024 * 1024,
        MaxImagePixels: 16L * 1024 * 1024,
        MaxOutputCharacters: 64 * 1024 * 1024,
        MaxOperands: 4096,
        MaxClipDepth: 256,
        MaxGlyphs: 1_000_000,
        MaxFonts: 32,
        MaxFontTextCharacters: 4_000_000,
        MaxGlyphComponents: 100_000);

    #endregion
}
