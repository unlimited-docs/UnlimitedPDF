using UnlimitedPDF.Models.Internal;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Numbers in new PDF operators are invariant, never use exponent notation, and are finite (PR-006, AC-15).
/// </summary>
public class PdfNumberTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(0d, "0")]
    [InlineData(-12d, "-12")]
    [InlineData(0.75d, "0.75")]
    [InlineData(56.69291338582677d, "56.692913")]
    [InlineData(0.00001d, "0.00001")]
    [InlineData(0.0000001d, "0")]
    [InlineData(-0.0000001d, "0")]
    [InlineData(1e20d, "100000000000000000000")]
    public void Formats_invariant_without_exponent(double value, string expected)
    {
        Assert.Equal(expected, PdfNumber.Format(value));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("")]
    public void Ignores_current_culture(string culture)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal("1234.5", PdfNumber.Format(1234.5));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Rejects_non_finite_values(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PdfNumber.Format(value));
        Assert.Equal("value", exception.ParamName);
    }

    #endregion

    #endregion
}
