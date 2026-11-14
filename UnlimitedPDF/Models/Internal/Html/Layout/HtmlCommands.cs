using UnlimitedPDF.Models.Internal.Fonts;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// One drawing command of laid-out HTML. Its x-coordinates are measured from the left edge of the page body, its
/// y-coordinates are page coordinates (WR-038).
/// </summary>
internal abstract record HtmlCommand;

/// <summary>
/// A filled rectangle.
/// </summary>
internal sealed record FillCommand(double X, double Bottom, double Width, double Height, PdfColor Color) : HtmlCommand;

/// <summary>
/// A filled or stroked circle, for list markers.
/// </summary>
internal sealed record CircleCommand(double CenterX, double CenterY, double Radius, PdfColor Color, bool Filled) : HtmlCommand;

/// <summary>
/// A styled straight line, for borders and text decoration.
/// </summary>
internal sealed record LineCommand(double X1, double Y1, double X2, double Y2, double Width, PdfColor Color, HtmlBorderStyle Style) : HtmlCommand;

/// <summary>
/// A run of text in a standard font, with the text it was encoded from (characters drawn as '?' are recorded from it).
/// </summary>
internal sealed record TextCommand(StandardFont Font, double FontSize, PdfColor Color, double X, double Baseline, byte[] Codes, double WordSpacing, double CharacterSpacing, string Text = "") : HtmlCommand;

/// <summary>
/// A run of text shaped in a caller font (WR-039).
/// </summary>
internal sealed record ShapedTextCommand(FontSegment Segment, double FontSize, PdfColor Color, double X, double Baseline, double WordSpacing, double CharacterSpacing) : HtmlCommand;

/// <summary>
/// An image drawn into a box.
/// </summary>
internal sealed record ImageCommand(PdfImage Image, double X, double Bottom, double Width, double Height) : HtmlCommand;

/// <summary>
/// The start of a clip to a rectangle, ended by <see cref="ClipEndCommand"/>.
/// </summary>
internal sealed record ClipBeginCommand(double X, double Bottom, double Width, double Height) : HtmlCommand;

/// <summary>
/// The end of a clip.
/// </summary>
internal sealed record ClipEndCommand : HtmlCommand;

/// <summary>
/// A clickable rectangle: a web or mail link, or a jump to an element id.
/// </summary>
internal sealed record LinkCommand(double X, double Bottom, double Width, double Height, string? Uri, string? TargetId) : HtmlCommand;

/// <summary>
/// A marked position: an element id, a heading for a bookmark, or both.
/// </summary>
internal sealed record MarkCommand(string? Id, int HeadingLevel, string? Title, double X, double Y) : HtmlCommand;

/// <summary>
/// The part of laid-out HTML on one page: which page (counted from the page the HTML starts on), the top and bottom
/// of the part, and its commands in drawing order.
/// </summary>
internal sealed class HtmlPageFragment
{
    #region Constructors and Finalizers

    public HtmlPageFragment(int pageOffset, double top)
    {
        PageOffset = pageOffset;
        Top = top;
        Bottom = top;
    }

    #endregion

    #region Properties, Indexers

    public double Bottom { get; set; }

    public List<HtmlCommand> Commands { get; } = new();

    public int PageOffset { get; }

    public double Top { get; }

    #endregion
}
