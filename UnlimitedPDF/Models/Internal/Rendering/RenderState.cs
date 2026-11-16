namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// The graphics state the content operators change, saved by <c>q</c> and restored by <c>Q</c>.
/// </summary>
internal sealed class RenderState
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a copy for <c>q</c>. Text-object state is not part of the graphics state and is not copied.
    /// </summary>
    public RenderState Copy() => (RenderState)MemberwiseClone();

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the number of SVG groups opened for clipping in this state, closed when it is restored.
    /// </summary>
    public int OpenGroups { get; set; }

    /// <summary>
    /// Gets or sets the current transformation matrix, from user space to the SVG page.
    /// </summary>
    public RenderMatrix Ctm { get; set; } = RenderMatrix.Identity;

    /// <summary>
    /// Gets or sets the dash pattern (<c>d</c>); empty for a solid line.
    /// </summary>
    public double[] Dash { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the dash phase (<c>d</c>).
    /// </summary>
    public double DashPhase { get; set; }

    /// <summary>
    /// Gets or sets the fill color (<c>rg</c>, <c>g</c>, <c>k</c>) as red, green, and blue from 0 to 1.
    /// </summary>
    public (double Red, double Green, double Blue) Fill { get; set; } = (0, 0, 0);

    /// <summary>
    /// Gets or sets the line cap (<c>J</c>): 0 butt, 1 round, 2 square.
    /// </summary>
    public int LineCap { get; set; }

    /// <summary>
    /// Gets or sets the line join (<c>j</c>): 0 miter, 1 round, 2 bevel.
    /// </summary>
    public int LineJoin { get; set; }

    /// <summary>
    /// Gets or sets the line width (<c>w</c>) in user space.
    /// </summary>
    public double LineWidth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the miter limit (<c>M</c>).
    /// </summary>
    public double MiterLimit { get; set; } = 10;

    /// <summary>
    /// Gets or sets the stroke color (<c>RG</c>, <c>G</c>, <c>K</c>) as red, green, and blue from 0 to 1.
    /// </summary>
    public (double Red, double Green, double Blue) Stroke { get; set; } = (0, 0, 0);

    /// <summary>
    /// Gets or sets the character spacing (<c>Tc</c>) in unscaled text space units.
    /// </summary>
    public double CharacterSpacing { get; set; }

    /// <summary>
    /// Gets or sets the current font (<c>Tf</c>); null before any is set.
    /// </summary>
    public RenderFont? Font { get; set; }

    /// <summary>
    /// Gets or sets the font size (<c>Tf</c>).
    /// </summary>
    public double FontSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the horizontal scaling (<c>Tz</c>) as a fraction (1 is 100 percent).
    /// </summary>
    public double HorizontalScale { get; set; } = 1;

    /// <summary>
    /// Gets or sets the leading (<c>TL</c>).
    /// </summary>
    public double Leading { get; set; }

    /// <summary>
    /// Gets or sets the text rendering mode (<c>Tr</c>).
    /// </summary>
    public int RenderMode { get; set; }

    /// <summary>
    /// Gets or sets the text rise (<c>Ts</c>).
    /// </summary>
    public double Rise { get; set; }

    /// <summary>
    /// Gets or sets the word spacing (<c>Tw</c>), applied to single-byte code 32.
    /// </summary>
    public double WordSpacing { get; set; }

    #endregion
}
