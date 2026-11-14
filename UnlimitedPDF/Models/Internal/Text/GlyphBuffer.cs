namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// One glyph while text is shaped: the glyph ID, the text cluster it stands for (the index of its first character),
/// its glyph class (1 base, 2 ligature, 3 mark, 4 component), the features that may act on it (a bit mask), ligature
/// bookkeeping for mark attachment, its position values in font units, and data the script shapers use.
/// </summary>
internal sealed class ShapingGlyph
{
    #region Constructors and Finalizers

    public ShapingGlyph(int glyph, int cluster)
    {
        Glyph = glyph;
        Cluster = cluster;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a copy that keeps the cluster, mask, and script data but takes another glyph ID.
    /// </summary>
    public ShapingGlyph WithGlyph(int glyph) => new(glyph, Cluster)
    {
        Mask = Mask,
        GlyphClass = GlyphClass,
        LigatureId = LigatureId,
        LigatureComponent = LigatureComponent,
        Category = Category,
        Position = Position,
        Syllable = Syllable,
        CodePoint = CodePoint
    };

    #endregion

    #endregion

    #region Properties, Indexers

    public int AttachDx { get; set; }

    public int AttachDy { get; set; }

    /// <summary>
    /// Gets or sets the index of the glyph this mark is attached to, or -1.
    /// </summary>
    public int AttachTo { get; set; } = -1;

    /// <summary>
    /// Gets or sets a script shaper's character category (for example an Indic syllabic category).
    /// </summary>
    public int Category { get; set; }

    public int Cluster { get; set; }

    /// <summary>
    /// Gets or sets the code point the glyph was mapped from (0 for glyphs that substitution created).
    /// </summary>
    public int CodePoint { get; set; }

    public int ComponentCount { get; set; } = 1;

    public int Glyph { get; set; }

    public int GlyphClass { get; set; } = 1;

    public bool IsMark => GlyphClass == 3;

    public int LigatureComponent { get; set; }

    public int LigatureId { get; set; }

    public ulong Mask { get; set; } = ulong.MaxValue;

    /// <summary>
    /// Gets or sets a script shaper's position class (for example where an Indic vowel sign is drawn).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the syllable the glyph belongs to, for script shapers.
    /// </summary>
    public int Syllable { get; set; }

    public int XAdvance { get; set; }

    public int XOffset { get; set; }

    public int YAdvance { get; set; }

    public int YOffset { get; set; }

    #endregion
}
