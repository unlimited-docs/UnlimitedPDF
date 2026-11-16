namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// The work shared by every glyph of one font load or one instance, or of every other weight of one variable font
/// (A06), on top of each glyph's own caps: a step budget and an output budget for all glyphs together, and for
/// TrueType instances a budget for applying <c>gvar</c> deltas. Without the step budget a font could keep every glyph
/// just under its cap and still take minutes to load; without the output budget the glyphs an instance keeps could
/// reach gigabytes from a small font; without the variation budget a few bytes of <c>gvar</c> could move every point
/// of a glyph again for each of thousands of tuples. All are tied to the size of the tables that drive the work
/// (<c>CFF </c>, <c>CFF2</c>, or, for TrueType instances, <c>glyf</c> and <c>gvar</c>), since an empty glyph or an
/// empty tuple costs a font only a few bytes. For CFF a step is an operand or operator (and a hint mask byte); for
/// TrueType it is a point instanced, and the variation budget is charged every point of a glyph for each tuple that
/// applies. (The variation store keeps each data's blend scalars itself.)
/// </summary>
internal sealed class CharstringWork
{
    #region Fields and Constants

    // Real CFF glyphs execute a few hundred steps each in tens to hundreds of table bytes, and flattening a real font's
    // subroutines takes a few times its table; real TrueType glyphs have a few hundred points, each at least a byte of
    // the table, and instancing writes them at most about two and a half times larger; the gvar tuples that apply at
    // one weight move at most about a fifth of a point per byte of glyf and gvar together (0.03 to 0.2 in seven
    // installed fonts), so two points per byte leaves ten times that. The fixed parts leave room for small fonts with
    // complex glyphs.
    public const long BaseSteps = 4_000_000;
    public const int StepsPerGlyph = 2_000;
    public const int StepsPerTableByte = 16;
    public const long BaseOutput = 8L * 1024 * 1024;
    public const int OutputPerTableByte = 8;
    public const int TrueTypeOutputPerTableByte = 4;
    public const long BaseVariation = 4_000_000;
    public const int VariationPerTableByte = 2;

    private readonly string _tooLong;
    private readonly string _tooLarge;
    private long _remaining;
    private long _outputLeft;
    private long _variedLeft;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the work shared by <paramref name="instances"/> instances' worth of <paramref name="glyphCount"/> CFF or
    /// CFF2 glyphs of an outline table of <paramref name="tableLength"/> bytes.
    /// </summary>
    public CharstringWork(int glyphCount, int tableLength, int instances = 1)
        : this(glyphCount, tableLength, OutputPerTableByte, 0, instances,
            "has CFF glyphs that together run too long through subroutines",
            "has CFF glyphs that together expand too far through subroutines")
    {
    }

    private CharstringWork(int glyphCount, int tableLength, int outputPerTableByte, int variationLength, int instances, string tooLong, string tooLarge)
    {
        long table = Math.Max(0, tableLength);
        _remaining = instances * (BaseSteps + Math.Min((long)StepsPerGlyph * Math.Max(1, glyphCount), StepsPerTableByte * table));
        _outputLeft = instances * (BaseOutput + outputPerTableByte * table);
        _variedLeft = instances * (BaseVariation + VariationPerTableByte * (table + Math.Max(0, variationLength)));
        _tooLong = tooLong;
        _tooLarge = tooLarge;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Creates the work shared by <paramref name="instances"/> instances' worth of <paramref name="glyphCount"/>
    /// TrueType glyphs of a <c>glyf</c> table of <paramref name="tableLength"/> bytes, varied by a <c>gvar</c> table
    /// of <paramref name="variationLength"/> bytes.
    /// </summary>
    public static CharstringWork ForTrueType(int glyphCount, int tableLength, int variationLength, int instances = 1) =>
        new(glyphCount, tableLength, TrueTypeOutputPerTableByte, variationLength, instances,
            "has TrueType glyphs with too many points in all to vary",
            "has TrueType glyphs that together grow too large when varied");

    /// <summary>
    /// Spends <paramref name="count"/> steps, and stops the font when the shared budget is spent.
    /// </summary>
    public void Step(int count = 1)
    {
        if ((_remaining -= count) < 0) throw FontBytes.Corrupt(_tooLong);
    }

    /// <summary>
    /// Spends one finished glyph's bytes, and stops the font when the shared output budget is spent.
    /// </summary>
    public void Output(int bytes)
    {
        if ((_outputLeft -= bytes) < 0) throw FontBytes.Corrupt(_tooLarge);
    }

    /// <summary>
    /// Spends the <paramref name="points"/> that one <c>gvar</c> tuple moves, and stops the font when the shared
    /// variation budget is spent.
    /// </summary>
    public void Vary(int points)
    {
        if ((_variedLeft -= points) < 0) throw FontBytes.Corrupt("has TrueType glyph variations that together take too long to apply");
    }

    #endregion

    #endregion
}
