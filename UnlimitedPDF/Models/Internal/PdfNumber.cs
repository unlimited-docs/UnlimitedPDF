namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Formats numbers for PDF syntax: invariant culture, no exponent notation, at most six decimals (PR-006).
/// </summary>
internal static class PdfNumber
{
    #region Fields and Constants

    private const string NumberFormat = "0.######";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Formats <paramref name="value"/> as a PDF real number, for example 0.75, 56.692913, or -12.
    /// </summary>
    /// <param name="value">A finite number.</param>
    /// <returns>The number in PDF syntax.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is NaN or infinite, which PDF cannot represent.</exception>
    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "PDF numbers must be finite.");

        var text = value.ToString(NumberFormat, CultureInfo.InvariantCulture);

        // Values that round to zero from below would otherwise print as "-0".
        return text == "-0" ? "0" : text;
    }

    #endregion

    #endregion
}
