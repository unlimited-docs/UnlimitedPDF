using System.ComponentModel;
using System.Reflection;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Unit conversion helpers turn millimeters, centimeters, inches, and pixels into points (AC-21, WR-023).
/// </summary>
public class PdfUnitsTests
{
    #region Fields and Constants

    private const int Precision = 9;

    #endregion

    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(2.54, 72)]
    [InlineData(1, 28.346456693)]
    [InlineData(0, 0)]
    public void Converts_centimeters(double centimeters, double points)
    {
        Assert.Equal(points, PdfUnits.FromCentimeters(centimeters), Precision);
    }

    [Theory]
    [InlineData(1, 72)]
    [InlineData(8.5, 612)]
    [InlineData(-0.5, -36)]
    public void Converts_inches(double inches, double points)
    {
        Assert.Equal(points, PdfUnits.FromInches(inches), Precision);
    }

    [Theory]
    [InlineData(25.4, 72)]
    [InlineData(210, 595.275590551)]
    public void Converts_millimeters(double millimeters, double points)
    {
        Assert.Equal(points, PdfUnits.FromMillimeters(millimeters), Precision);
    }

    [Theory]
    [InlineData(96, 72)]
    [InlineData(1, 0.75)]
    [InlineData(300, 225)]
    public void Converts_pixels_at_96_per_inch(double pixels, double points)
    {
        Assert.Equal(points, PdfUnits.FromPixels(pixels), Precision);
    }

    [Fact]
    public void Matches_story_examples()
    {
        // AC-21: 20 mm = 56.69 pt and 96 px = 72 pt.
        Assert.Equal(56.69, PdfUnits.FromMillimeters(20), 2);
        Assert.Equal(72, PdfUnits.FromPixels(96));
    }

    [Fact]
    public void Public_methods_carry_version_metadata()
    {
        var methods = typeof(PdfUnits).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.All(methods, method =>
        {
            Assert.Equal("0.1.0", method.GetCustomAttribute<AddedVersionAttribute>()?.Version);
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
        });
    }

    #endregion

    #endregion
}
