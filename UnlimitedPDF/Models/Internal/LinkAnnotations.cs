namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Writes a built page's link annotations (WR-038): direct link dictionaries with no border, each either a URI action
/// or a destination on a page. Only these two kinds are ever written, so no script or other action can reach the
/// output (A05).
/// </summary>
internal static class LinkAnnotations
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the syntax of a page's <c>/Annots</c> array, or null when the page has no link to write.
    /// </summary>
    /// <param name="links">The page's links.</param>
    /// <param name="resolve">Returns the page object number and position an element id points to, or null when the
    /// id is not in the document; such a link is not written.</param>
    /// <returns>The array syntax, or null.</returns>
    public static string? Syntax(IReadOnlyList<PageLink> links, Func<string, (int PageObject, double X, double Y)?> resolve)
    {
        var entries = new List<string>();
        foreach (var link in links)
        {
            string? target = null;
            if (link.Uri is not null)
            {
                // /S /URI = open the address; the address is ASCII, escaped for the literal string (PR-006).
                target = $"/A << /S /URI /URI ({PdfLiteralString.EscapeBytes(Encoding.ASCII.GetBytes(link.Uri))}) >>";
            }
            else if (link.TargetId is not null && resolve(link.TargetId) is { } destination)
            {
                // /XYZ x y null = show the page with that point at the top-left, keeping the zoom.
                target = $"/Dest [{destination.PageObject} 0 R /XYZ {PdfNumber.Format(destination.X)} {PdfNumber.Format(destination.Y)} null]";
            }
            if (target is null) continue;

            entries.Add($"<< /Type /Annot /Subtype /Link /Rect [{PdfNumber.Format(link.X)} {PdfNumber.Format(link.Bottom)} " +
                        $"{PdfNumber.Format(link.X + link.Width)} {PdfNumber.Format(link.Bottom + link.Height)}] /Border [0 0 0] {target} >>");
        }
        return entries.Count == 0 ? null : "[" + string.Join(" ", entries) + "]";
    }

    #endregion

    #endregion
}
