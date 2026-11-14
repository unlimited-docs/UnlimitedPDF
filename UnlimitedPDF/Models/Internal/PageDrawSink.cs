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
    private readonly List<EmbeddedFace> _faces = new();
    private readonly Dictionary<EmbeddedFace, SortedDictionary<int, string>> _faceGlyphs = new(ReferenceEqualityComparer.Instance);

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the sink.
    /// </summary>
    /// <param name="imageName">Returns the page resource name of an image, registering it.</param>
    public PageDrawSink(Func<PdfImageData, string> imageName, EmbeddedFonts? fonts = null)
    {
        ImageName = imageName;
        EmbeddedFonts = fonts ?? new EmbeddedFonts();
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

    /// <summary>
    /// Records that the page draws <paramref name="glyph"/> of a caller font, standing for <paramref name="text"/>
    /// (the first text recorded for a glyph is kept), and returns the font's resource name.
    /// </summary>
    public string FaceResource(PdfFont font, int glyph, string text)
    {
        var face = EmbeddedFonts.Face(font);
        if (!_faceGlyphs.TryGetValue(face, out var glyphs))
        {
            glyphs = new SortedDictionary<int, string>();
            _faceGlyphs[face] = glyphs;
            _faces.Add(face);
        }
        if (!glyphs.ContainsKey(glyph)) glyphs[glyph] = text;
        return face.ResourceName;
    }

    /// <summary>
    /// Returns the glyphs the page draws with a face and the text each stands for.
    /// </summary>
    public IReadOnlyDictionary<int, string> GlyphsOf(EmbeddedFace face) => _faceGlyphs[face];

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the standard fonts the page uses, in resource-name order.
    /// </summary>
    public EmbeddedFonts EmbeddedFonts { get; }

    /// <summary>
    /// Gets the caller fonts the page draws with, in order of first use.
    /// </summary>
    public IReadOnlyList<EmbeddedFace> Faces => _faces;

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
