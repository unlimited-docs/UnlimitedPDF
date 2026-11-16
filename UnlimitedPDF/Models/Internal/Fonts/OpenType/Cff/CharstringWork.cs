namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// The work shared by every glyph of one font load or one instance (A06): a step budget for all glyphs together, on
/// top of each glyph's own cap. Without it a font could keep every glyph just under its cap and still take minutes to
/// load. (The variation store keeps each <c>vsindex</c>'s blend scalars itself.)
/// </summary>
internal sealed class CharstringWork
{
    #region Fields and Constants

    // Real glyphs execute a few hundred steps each; the fixed part leaves room for fonts with few, complex glyphs.
    public const long BaseSteps = 4_000_000;
    public const int StepsPerGlyph = 2_000;

    private long _remaining;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the shared work for <paramref name="glyphCount"/> glyphs.
    /// </summary>
    public CharstringWork(int glyphCount) => _remaining = BaseSteps + (long)StepsPerGlyph * Math.Max(1, glyphCount);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Spends one step, and stops the font when the shared budget is spent.
    /// </summary>
    public void Step()
    {
        if (--_remaining < 0) throw FontBytes.Corrupt("has CFF glyphs that together run too long through subroutines");
    }

    #endregion

    #endregion
}
