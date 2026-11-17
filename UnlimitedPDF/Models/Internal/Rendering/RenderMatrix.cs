namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// A 2D affine matrix in PDF order <c>[a b c d e f]</c>, mapping (x, y) to (a·x + c·y + e, b·x + d·y + f).
/// </summary>
/// <param name="A">The horizontal scale part.</param>
/// <param name="B">The vertical shear part.</param>
/// <param name="C">The horizontal shear part.</param>
/// <param name="D">The vertical scale part.</param>
/// <param name="E">The horizontal translation.</param>
/// <param name="F">The vertical translation.</param>
internal readonly record struct RenderMatrix(double A, double B, double C, double D, double E, double F)
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns this matrix followed by <paramref name="other"/>, as PDF concatenates <c>cm</c>: a point is first
    /// mapped by this matrix, then by <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The matrix applied second.</param>
    /// <returns>The product.</returns>
    public RenderMatrix Then(RenderMatrix other) => new(
        A * other.A + B * other.C,
        A * other.B + B * other.D,
        C * other.A + D * other.C,
        C * other.B + D * other.D,
        E * other.A + F * other.C + other.E,
        E * other.B + F * other.D + other.F);

    /// <summary>
    /// Returns a translation by (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public static RenderMatrix Translation(double x, double y) => new(1, 0, 0, 1, x, y);

    /// <summary>
    /// Returns whether every part is a finite number.
    /// </summary>
    public bool IsFinite() => double.IsFinite(A) && double.IsFinite(B) && double.IsFinite(C) && double.IsFinite(D)
        && double.IsFinite(E) && double.IsFinite(F);

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the identity matrix.
    /// </summary>
    public static RenderMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    #endregion
}
