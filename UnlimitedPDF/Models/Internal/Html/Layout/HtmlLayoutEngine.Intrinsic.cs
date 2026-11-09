using UnlimitedPDF.Models.Internal.Html.Css;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// Intrinsic widths (WR-038): the narrowest width content can take without overflowing (its widest word or atomic
/// inline) and the width it takes without wrapping, used for inline-blocks and table columns.
/// </summary>
internal sealed partial class HtmlLayoutEngine
{
    #region Methods

    #region Private Methods

    // Horizontal margins, borders, and padding that do not depend on the containing width.
    private static double FixedHorizontal(ComputedStyle s)
    {
        double Fixed(CssLength? length) => length is { Unit: not CssUnit.Percent } l ? Resolve(l, 0) : 0;
        return Fixed(s.Margins[1]) + Fixed(s.Margins[3]) + s.Borders[1].UsedWidth + s.Borders[3].UsedWidth + Fixed(s.Paddings[1]) + Fixed(s.Paddings[3]);
    }

    // A table cell's width is a preference (preferredWidth): its narrowest width stays its content's, and its widest
    // is at least the preferred width.
    private (double Min, double Max) IntrinsicWidths(HtmlBox box, bool preferredWidth = false)
    {
        var s = box.Style;
        double outside = FixedHorizontal(s);
        double? preferred = s.Width is { Unit: not CssUnit.Percent } width ? Resolve(width, 0) + outside : null;
        if (preferred is double fixedWidth && !preferredWidth) return (fixedWidth, fixedWidth);

        double min = 0, max = 0;
        switch (box)
        {
            case BlockBox block when block.Inlines.Count > 0:
                double line = Resolve(s.TextIndent is { Unit: not CssUnit.Percent } indent ? indent : CssLength.Zero, 0);
                foreach (var token in Tokenize(block, 1e9))
                {
                    if (token.Kind == TokenKind.Break)
                    {
                        max = Math.Max(max, line);
                        line = 0;
                        continue;
                    }
                    line += token.Width;
                    bool wraps = token.Item.Style.WhiteSpace is CssWhiteSpace.Normal or CssWhiteSpace.PreWrap or CssWhiteSpace.PreLine;
                    // Images scale down to fit (D-016), so they set no narrowest width.
                    bool scalable = token.Kind == TokenKind.Atomic && ((AtomicItem)token.Item).Box is HtmlImageBox;
                    if (token.Kind != TokenKind.Space) min = Math.Max(min, wraps && !scalable ? token.Width : 0);
                }
                max = Math.Max(max, line);
                if (s.WhiteSpace is CssWhiteSpace.NoWrap or CssWhiteSpace.Pre) min = max;
                break;
            case BlockBox block:
                foreach (var child in block.Children)
                {
                    var (childMin, childMax) = IntrinsicWidths(child);
                    min = Math.Max(min, childMin);
                    max = Math.Max(max, childMax);
                }
                break;
            case TableBox table:
                (min, max) = TableIntrinsicWidths(table);
                break;
        }
        if (s.MinWidth is { Unit: not CssUnit.Percent } minWidth) { min = Math.Max(min, Resolve(minWidth, 0)); max = Math.Max(max, Resolve(minWidth, 0)); }
        if (s.MaxWidth is { Unit: not CssUnit.Percent } maxWidth) max = Math.Min(max, Math.Max(min, Resolve(maxWidth, 0)));
        double resultMin = min + outside, resultMax = Math.Max(min, max) + outside;
        if (preferred is double p) resultMax = Math.Max(resultMin, p);
        return (resultMin, resultMax);
    }

    #endregion

    #endregion
}
