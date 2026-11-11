using UnlimitedPDF.Models.Internal.Fonts;

namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Collects what drawing one built page needs besides its operators (WR-038): image resource names, the standard fonts
/// used, link rectangles, and marked positions (element ids and headings) that links and bookmarks point to.
/// </summary>
internal sealed class PageDrawSink
{
    #region Fields and Constants

    private readonly SortedSet<StandardFont> _fonts = new();

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the sink.
    /// </summary>
    /// <param name="imageName">Returns the page resource name of an image, registering it.</param>
    public PageDrawSink(Func<PdfImageData, string> imageName)
    {
        ImageName = imageName;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Registers a standard font as used on the page and returns its resource name without the slash.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The resource name, for example "HF2".</returns>
    public string FontResource(StandardFont font)
    {
        _fonts.Add(font);
        return StandardFontObject.ResourceName(font);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the standard fonts the page uses, in resource-name order.
    /// </summary>
    public IReadOnlyCollection<StandardFont> Fonts => _fonts;

    /// <summary>
    /// Gets the function that returns an image's resource name, registering the image on the page.
    /// </summary>
    public Func<PdfImageData, string> ImageName { get; }

    /// <summary>
    /// Gets the link rectangles drawn on the page, in drawing order.
    /// </summary>
    public List<PageLink> Links { get; } = new();

    /// <summary>
    /// Gets the marked positions on the page, in drawing order.
    /// </summary>
    public List<PageMark> Marks { get; } = new();

    #endregion
}

/// <summary>
/// A clickable rectangle on a page: a web or mail link, or a jump to a marked element id.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Uri">The URI for a web or mail link (ASCII), or null.</param>
/// <param name="TargetId">The element id a jump goes to, or null.</param>
internal sealed record PageLink(double X, double Bottom, double Width, double Height, string? Uri, string? TargetId);

/// <summary>
/// A marked position on a page: where an element with an id starts, or where a heading starts.
/// </summary>
/// <param name="Id">The element id, or null.</param>
/// <param name="HeadingLevel">The heading level 1 to 6 when the position is a bookmarked heading, else 0.</param>
/// <param name="Title">The heading text for a bookmark, or null.</param>
/// <param name="X">The x-coordinate of the position.</param>
/// <param name="Y">The y-coordinate of the position's top.</param>
internal sealed record PageMark(string? Id, int HeadingLevel, string? Title, double X, double Y);
