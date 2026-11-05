namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// The layout settings in effect on one page after the document defaults and the page rules are merged (WR-030,
/// WR-032). Content is resolved separately because Last-page rules may change it.
/// </summary>
/// <param name="TopMargin">The top margin.</param>
/// <param name="BottomMargin">The bottom margin.</param>
/// <param name="LeftMargin">The left margin.</param>
/// <param name="RightMargin">The right margin.</param>
/// <param name="HeaderHeight">The header band height.</param>
/// <param name="HeaderHidden">Whether the header is hidden.</param>
/// <param name="FooterHeight">The footer band height.</param>
/// <param name="FooterHidden">Whether the footer is hidden.</param>
/// <param name="BodyTopGap">The gap between the header band and the body.</param>
/// <param name="BodyBottomGap">The gap between the body and the footer band.</param>
internal sealed record EffectiveSettings(
    double TopMargin,
    double BottomMargin,
    double LeftMargin,
    double RightMargin,
    double HeaderHeight,
    bool HeaderHidden,
    double FooterHeight,
    bool FooterHidden,
    double BodyTopGap,
    double BodyBottomGap);
