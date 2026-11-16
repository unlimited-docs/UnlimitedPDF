namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// The work shared by every glyph of one font load or one instance (A06), on top of each glyph's own caps: a step
/// budget and an output budget for all glyphs together. Without the step budget a font could keep every glyph just
/// under its cap and still take minutes to load; without the output budget its flattened glyphs, which an instance
/// keeps, could reach gigabytes from a small font. Both are tied to the size of the font's charstring table, since an
/// empty glyph costs a font only a few bytes. (The variation store keeps each data's blend scalars itself.)
/// </summary>
internal sealed class CharstringWork
{
    #region Fields and Constants

    // Real glyphs execute a few hundred steps each and take tens to hundreds of table bytes, and flattening a real
    // font's subroutines takes a few times its table; the fixed parts leave room for small fonts with complex glyphs.
    public const long BaseSteps = 4_000_000;
    public const int StepsPerGlyph = 2_000;
    public const int StepsPerTableByte = 64;
    public const long BaseOutput = 32L * 1024 * 1024;
    public const int OutputPerTableByte = 8;

    private readonly long _stepLimit;
    private readonly long _outputLimit;
    private long _remaining;
    private long _outputLeft;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the shared work for <paramref name="glyphCount"/> glyphs of a charstring table of
    /// <paramref name="tableLength"/> bytes.
    /// </summary>
    public CharstringWork(int glyphCount, int tableLength)
    {
        long table = Math.Max(0, tableLength);
        _remaining = _stepLimit = BaseSteps + Math.Min((long)StepsPerGlyph * Math.Max(1, glyphCount), StepsPerTableByte * table);
        _outputLeft = _outputLimit = BaseOutput + OutputPerTableByte * table;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Spends <paramref name="count"/> steps, and stops the font when the shared budget is spent.
    /// </summary>
    public void Step(int count = 1)
    {
        if ((_remaining -= count) < 0) throw FontBytes.Corrupt("has CFF glyphs that together run too long through subroutines");
    }

    /// <summary>
    /// Spends one flattened glyph's bytes, and stops the font when the shared output budget is spent.
    /// </summary>
    public void Output(int bytes)
    {
        if ((_outputLeft -= bytes) < 0) throw FontBytes.Corrupt("has CFF glyphs that together expand too far through subroutines");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the share of the step budget used so far, from 0 to 1.
    /// </summary>
    public double StepsUsed => (double)(_stepLimit - _remaining) / _stepLimit;

    /// <summary>
    /// Gets the share of the output budget used so far, from 0 to 1.
    /// </summary>
    public double OutputUsed => (double)(_outputLimit - _outputLeft) / _outputLimit;

    #endregion
}
