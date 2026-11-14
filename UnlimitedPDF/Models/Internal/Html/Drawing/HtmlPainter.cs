using UnlimitedPDF.Models.Internal.Html.Layout;

namespace UnlimitedPDF.Models.Internal.Html.Drawing;

/// <summary>
/// Draws one page's part of laid-out HTML (WR-038): its commands become content-stream operators, its links and marks
/// go to the page's draw sink for link annotations and bookmarks.
/// </summary>
internal static class HtmlPainter
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Draws a fragment.
    /// </summary>
    /// <param name="sb">The content stream text.</param>
    /// <param name="sink">The page's draw sink.</param>
    /// <param name="fragment">The fragment.</param>
    /// <param name="left">The left edge of the page body.</param>
    /// <param name="headingBookmarks">Whether headings become bookmarks.</param>
    public static void Draw(StringBuilder sb, PageDrawSink sink, HtmlPageFragment fragment, double left, bool headingBookmarks)
    {
        foreach (var command in fragment.Commands)
        {
            switch (command)
            {
                case FillCommand f:
                    if (f.Width > 0 && f.Height > 0) sb.AppendLine(HtmlOperators.Fill(left + f.X, f.Bottom, f.Width, f.Height, f.Color));
                    break;
                case CircleCommand c:
                    sb.AppendLine(HtmlOperators.Circle(left + c.CenterX, c.CenterY, c.Radius, c.Color, c.Filled));
                    break;
                case LineCommand l:
                    string line = HtmlOperators.Line(left + l.X1, l.Y1, left + l.X2, l.Y2, l.Width, l.Color, l.Style);
                    if (line.Length > 0) sb.AppendLine(line);
                    break;
                case TextCommand t:
                    sb.AppendLine(HtmlOperators.Text(sink.FontResource(t.Font), t.FontSize, t.Color, left + t.X, t.Baseline, t.Codes, t.WordSpacing, t.CharacterSpacing));
                    foreach (string missing in StandardText.MissingElements(t.Text)) sink.RecordMissing(missing, t.Text);
                    break;
                case ShapedTextCommand s:
                    sb.AppendLine(HtmlOperators.ShapedText(s.Segment, s.FontSize, s.Color, left + s.X, s.Baseline, sink, s.WordSpacing, s.CharacterSpacing));
                    break;
                case ImageCommand i:
                    if (i.Width > 0 && i.Height > 0) sb.AppendLine(HtmlOperators.Image(sink.ImageName(i.Image.Data), left + i.X, i.Bottom, i.Width, i.Height));
                    break;
                case ClipBeginCommand c:
                    sb.AppendLine(HtmlOperators.ClipBegin(left + c.X, c.Bottom, c.Width, c.Height));
                    break;
                case ClipEndCommand:
                    sb.AppendLine(HtmlOperators.ClipEnd());
                    break;
                case LinkCommand k:
                    sink.Links.Add(new PageLink(left + k.X, k.Bottom, k.Width, k.Height, k.Uri, k.TargetId));
                    break;
                case MarkCommand m:
                    int level = headingBookmarks ? m.HeadingLevel : 0;
                    if (m.Id is not null || level > 0) sink.Marks.Add(new PageMark(m.Id, level, m.Title, left + m.X, m.Y));
                    break;
            }
        }
    }

    #endregion

    #endregion
}
