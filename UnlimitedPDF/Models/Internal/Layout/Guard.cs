namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// Argument checks for the page layout API (A10): lengths are finite points, and sizes are zero or greater, or greater
/// than zero where an empty size draws nothing.
/// </summary>
internal static class Guard
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Throws when <paramref name="value"/> is NaN or infinite.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    public static void Finite(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite number of points.");
    }

    /// <summary>
    /// Throws when <paramref name="value"/> is negative, NaN, or infinite.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public static void NonNegative(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite number of points, zero or greater.");
    }

    /// <summary>
    /// Throws when <paramref name="value"/> is zero, negative, NaN, or infinite.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is not greater than zero or not finite.</exception>
    public static void Positive(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite number of points greater than zero.");
    }

    #endregion

    #endregion
}
