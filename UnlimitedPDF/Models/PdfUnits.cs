namespace UnlimitedPDF;

/// <summary>
/// Converts common length units to points, the unit of every length in the library (1 pt = 1/72 in).
/// </summary>
/// <remarks>
/// Conversions are explicit so a plain number always means points (WR-023). A pixel is 1/96 in, so 1 px = 0.75 pt.
/// </remarks>
public static class PdfUnits
{
    #region Fields and Constants

    private const double MillimetersPerInch = 25.4;
    private const double PixelsPerInch = 96;
    private const double PointsPerInch = 72;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Converts centimeters to points.
    /// </summary>
    /// <param name="centimeters">A length in centimeters.</param>
    /// <returns>The length in points, for example 2.54 cm = 72 pt.</returns>
    [AddedVersion("0.1.0")]
    [Description("Converts centimeters to points.")]
    public static double FromCentimeters(double centimeters) => centimeters * 10 * PointsPerInch / MillimetersPerInch;

    /// <summary>
    /// Converts inches to points.
    /// </summary>
    /// <param name="inches">A length in inches.</param>
    /// <returns>The length in points, for example 1 in = 72 pt.</returns>
    [AddedVersion("0.1.0")]
    [Description("Converts inches to points.")]
    public static double FromInches(double inches) => inches * PointsPerInch;

    /// <summary>
    /// Converts millimeters to points.
    /// </summary>
    /// <param name="millimeters">A length in millimeters.</param>
    /// <returns>The length in points, for example 20 mm = 56.69 pt.</returns>
    [AddedVersion("0.1.0")]
    [Description("Converts millimeters to points.")]
    public static double FromMillimeters(double millimeters) => millimeters * PointsPerInch / MillimetersPerInch;

    /// <summary>
    /// Converts pixels to points, with one pixel being 1/96 in.
    /// </summary>
    /// <param name="pixels">A length in pixels.</param>
    /// <returns>The length in points, for example 96 px = 72 pt.</returns>
    [AddedVersion("0.1.0")]
    [Description("Converts pixels to points, with one pixel being 1/96 in.")]
    public static double FromPixels(double pixels) => pixels * PointsPerInch / PixelsPerInch;

    #endregion

    #endregion
}
