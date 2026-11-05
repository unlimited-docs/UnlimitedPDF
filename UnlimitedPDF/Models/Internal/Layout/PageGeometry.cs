namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// Where the areas of one saved page are, in page coordinates (points from the bottom-left corner, WR-030): from the
/// top edge, the top margin, the header band, the body top gap, the body, the body bottom gap, the footer band, and the
/// bottom margin; the side margins bound all three areas.
/// </summary>
/// <param name="Width">The page width.</param>
/// <param name="Height">The page height.</param>
/// <param name="Left">The x-coordinate of the left margin.</param>
/// <param name="Right">The x-coordinate of the right margin.</param>
/// <param name="HeaderTop">The y-coordinate of the header band's top edge (the top margin).</param>
/// <param name="HeaderHeight">The header band's height; 0 when the header is hidden.</param>
/// <param name="BodyTop">The y-coordinate of the body's top edge.</param>
/// <param name="BodyBottom">The y-coordinate of the body's bottom edge.</param>
/// <param name="FooterTop">The y-coordinate of the footer band's top edge.</param>
/// <param name="FooterHeight">The footer band's height; 0 when the footer is hidden.</param>
internal sealed record PageGeometry(
    double Width,
    double Height,
    double Left,
    double Right,
    double HeaderTop,
    double HeaderHeight,
    double BodyTop,
    double BodyBottom,
    double FooterTop,
    double FooterHeight)
{
    /// <summary>
    /// Gets the body's height (negative when the settings leave no room).
    /// </summary>
    public double BodyHeight => BodyTop - BodyBottom;

    /// <summary>
    /// Gets the width between the side margins (negative when the margins overlap).
    /// </summary>
    public double ContentWidth => Right - Left;

    /// <summary>
    /// Computes the geometry of a page.
    /// </summary>
    /// <param name="size">The page size.</param>
    /// <param name="settings">The settings in effect on the page.</param>
    /// <returns>The geometry.</returns>
    public static PageGeometry Create(PdfPageSize size, EffectiveSettings settings)
    {
        var (width, height) = size.ToDimensions();
        double headerTop = height - settings.TopMargin;
        double headerHeight = settings.HeaderHidden ? 0 : settings.HeaderHeight;
        double footerHeight = settings.FooterHidden ? 0 : settings.FooterHeight;
        double footerTop = settings.BottomMargin + footerHeight;
        return new PageGeometry(
            width,
            height,
            settings.LeftMargin,
            width - settings.RightMargin,
            headerTop,
            headerHeight,
            headerTop - headerHeight - settings.BodyTopGap,
            footerTop + settings.BodyBottomGap,
            footerTop,
            footerHeight);
    }
}
