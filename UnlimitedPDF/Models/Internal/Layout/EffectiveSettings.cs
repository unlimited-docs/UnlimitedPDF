namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// The layout settings in effect on one page after the document defaults and the page rules are merged (WR-030,
/// WR-032). Content is resolved separately because Last-page rules may change it.
/// </summary>
/// <param name="MarginTop">The top margin.</param>
/// <param name="MarginBottom">The bottom margin.</param>
/// <param name="MarginLeft">The left margin.</param>
/// <param name="MarginRight">The right margin.</param>
/// <param name="HeaderHeight">The header band height.</param>
/// <param name="HeaderHidden">Whether the header is hidden.</param>
/// <param name="FooterHeight">The footer band height.</param>
/// <param name="FooterHidden">Whether the footer is hidden.</param>
/// <param name="BodyMarginTop">The space between the header band and the body.</param>
/// <param name="BodyMarginBottom">The space between the body and the footer band.</param>
internal sealed record EffectiveSettings(
    double MarginTop,
    double MarginBottom,
    double MarginLeft,
    double MarginRight,
    double HeaderHeight,
    bool HeaderHidden,
    double FooterHeight,
    bool FooterHidden,
    double BodyMarginTop,
    double BodyMarginBottom);
