namespace UnlimitedPDF.Tests;

/// <summary>
/// Page ranges are 1-based and inclusive and reject reversed or zero ranges at construction; split exceptions carry
/// the failed part and the parts written (WR-037, AC-03, AC-04, AC-10).
/// </summary>
public class PdfPageRangeTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    public void First_page_below_one_is_rejected(int firstPage, int lastPage)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPageRange(firstPage, lastPage));
        Assert.Equal("firstPage", exception.ParamName);
    }

    [Fact]
    public void Reversed_range_is_rejected()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPageRange(5, 3));
        Assert.Equal("lastPage", exception.ParamName);
    }

    [Theory]
    [InlineData(1, 3, "1-3")]
    [InlineData(5, 5, "5")]
    [InlineData(8, 10, "8-10")]
    public void Valid_range_keeps_its_pages(int firstPage, int lastPage, string text)
    {
        var range = new PdfPageRange(firstPage, lastPage);

        Assert.Equal(firstPage, range.FirstPage);
        Assert.Equal(lastPage, range.LastPage);
        Assert.Equal(text, range.ToString());
    }

    [Fact]
    public void Split_exception_carries_part_and_count()
    {
        var cause = new IOException("disk full");

        var exception = new PdfSplitException("Part 3 of 5 failed.", 3, 2, cause);

        Assert.Equal(3, exception.PartNumber);
        Assert.Equal(2, exception.PartsWritten);
        Assert.Same(cause, exception.InnerException);
        Assert.Equal("Part 3 of 5 failed.", exception.Message);
    }

    #endregion

    #endregion
}
