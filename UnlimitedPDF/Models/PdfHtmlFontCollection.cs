using System.Collections;

namespace UnlimitedPDF;

/// <summary>
/// The caller fonts HTML can use, by CSS family name (WR-039, D-011): text whose <c>font-family</c> lists a registered
/// name is drawn with the face of that family that matches its weight and style. Add one face per weight and style
/// (regular, bold, italic, bold italic), or one variable font, which draws every weight in its range (D-013). Family
/// names are matched without regard to case. Set it through <see cref="PdfHtmlLoadOptions.Fonts"/>; the faces are
/// read when the HTML is loaded.
/// </summary>
public sealed class PdfHtmlFontCollection : IEnumerable<KeyValuePair<string, PdfFont>>
{
    #region Fields and Constants

    private readonly List<KeyValuePair<string, PdfFont>> _faces = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Registers a face for a CSS family name. The face's weight and style come from the font
    /// (<see cref="PdfFont.Weight"/>, <see cref="PdfFont.IsItalic"/>). Adding the same face to a family again does nothing.
    /// </summary>
    /// <param name="family">The CSS family name, for example <c>Noto Sans Devanagari</c>.</param>
    /// <param name="font">The face.</param>
    /// <exception cref="ArgumentException"><paramref name="family"/> is empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> or <paramref name="font"/> is null.</exception>
    [AddedVersion("1.1.0")]
    [Description("Registers a font face for a CSS family name.")]
    public void Add(string family, PdfFont font)
    {
        if (family is null) throw new ArgumentNullException(nameof(family));
        if (font is null) throw new ArgumentNullException(nameof(font));
        string name = Normalize(family);
        if (name.Length == 0) throw new ArgumentException("The family name is empty.", nameof(family));
        if (_faces.Any(f => f.Key == name && ReferenceEquals(f.Value, font))) return;
        _faces.Add(new KeyValuePair<string, PdfFont>(name, font));
    }

    /// <summary>
    /// Returns an enumerator over the registered faces, in the order they were added, with their family names in
    /// lowercase.
    /// </summary>
    /// <returns>The enumerator.</returns>
    [AddedVersion("1.1.0")]
    [Description("Returns an enumerator over the registered faces.")]
    public IEnumerator<KeyValuePair<string, PdfFont>> GetEnumerator() => _faces.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns a copy of the registered faces, keyed by lowercase family name, for one load.
    /// </summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<PdfFont>> Snapshot() =>
        _faces.GroupBy(f => f.Key, StringComparer.Ordinal)
              .ToDictionary(g => g.Key, g => (IReadOnlyList<PdfFont>)g.Select(f => f.Value).ToList(), StringComparer.Ordinal);

    /// <summary>
    /// Returns a family name as it is matched: trimmed, without quotes, and in lowercase.
    /// </summary>
    internal static string Normalize(string family) => family.Trim().Trim('"', '\'').Trim().ToLowerInvariant();

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the number of registered faces.
    /// </summary>
    public int Count => _faces.Count;

    #endregion
}
