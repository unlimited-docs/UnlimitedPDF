namespace UnlimitedPDF.Tests;

/// <summary>
/// <see cref="PdfColor"/> factories: 0–255 channels and <c>#RRGGBB</c> strings map onto the 0–1 record, and anything
/// else fails at the call with a specific exception.
/// </summary>
public class PdfColorTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("#002A73")]
    [InlineData("002A73")]
    [InlineData("002a73")]
    [InlineData("#002a73")]
    public void FromHex_accepts_six_hex_digits_with_or_without_hash_in_any_case(string hex)
    {
        Assert.Equal(PdfColor.FromRgb(0, 42, 115), PdfColor.FromHex(hex));
    }

    [Fact]
    public void FromHex_rejects_null()
    {
        Assert.Equal("hex", Assert.Throws<ArgumentNullException>(() => PdfColor.FromHex(null!)).ParamName);
    }

    [Theory]
    [InlineData("#02A")]
    [InlineData("#002A7")]
    [InlineData("#002A733")]
    [InlineData("#GG2A73")]
    [InlineData("##002A73")]
    [InlineData(" 002A73")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("#")]
    public void FromHex_rejects_anything_else(string hex)
    {
        var error = Assert.Throws<ArgumentException>(() => PdfColor.FromHex(hex));
        Assert.Equal("hex", error.ParamName);
    }

    [Fact]
    public void FromRgb_divides_each_channel_by_255()
    {
        Assert.Equal(new PdfColor(0, 172 / 255.0, 140 / 255.0), PdfColor.FromRgb(0, 172, 140));
        Assert.Equal(PdfColor.White, PdfColor.FromRgb(255, 255, 255));
        Assert.Equal(PdfColor.Black, PdfColor.FromRgb(0, 0, 0));
    }

    #endregion

    #endregion
}
