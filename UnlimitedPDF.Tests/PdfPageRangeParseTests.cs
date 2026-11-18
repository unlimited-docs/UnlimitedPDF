namespace UnlimitedPDF.Tests;

/// <summary>
/// <see cref="PdfPageRange.Parse(string)"/> reads page numbers and ascending or descending ranges separated by commas,
/// fails with a <see cref="FormatException"/> naming the bad item for anything else, and caps the pages one text may
/// describe (WR-040, AC-05 to AC-07).
/// </summary>
public class PdfPageRangeParseTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("2, 5, 7-9", "2 5 7-9")]
    [InlineData("2,5,7-9", "2 5 7-9")]
    [InlineData(" 2 ,5 , 7 - 9 ", "2 5 7-9")]
    [InlineData("\t3\t-\t3\t", "3")]
    [InlineData("5", "5")]
    [InlineData("9-7", "9 8 7")]
    [InlineData("9,2,2-3", "9 2 2-3")]
    [InlineData("10,1,1", "10 1 1")]
    [InlineData("1-3,4-10", "1-3 4-10")]
    public void Numbers_and_ranges_are_read_in_the_order_written(string text, string expected)
    {
        var ranges = PdfPageRange.Parse(text);

        Assert.Equal(expected, string.Join(" ", ranges.Select(range => range.ToString())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2,,5")]
    [InlineData("2,")]
    [InlineData(",2")]
    [InlineData("0")]
    [InlineData("0-3")]
    [InlineData("-3")]
    [InlineData("7-")]
    [InlineData("1-2-3")]
    [InlineData("last")]
    [InlineData("odd")]
    [InlineData("even")]
    [InlineData("2;5")]
    [InlineData("1 2")]
    [InlineData("+3")]
    [InlineData("٣")]
    [InlineData("99999999999")]
    [InlineData("2147483648")]
    public void Anything_else_fails_with_a_format_exception(string text)
    {
        Assert.Throws<FormatException>(() => PdfPageRange.Parse(text));
    }

    [Fact]
    public void The_message_names_the_bad_item()
    {
        var exception = Assert.Throws<FormatException>(() => PdfPageRange.Parse("2, 5, last"));

        Assert.Contains("Item 3", exception.Message);
        Assert.Contains("\"last\"", exception.Message);
    }

    [Fact]
    public void A_long_bad_item_is_cut_short_in_the_message()
    {
        string item = new('x', 1000);

        var exception = Assert.Throws<FormatException>(() => PdfPageRange.Parse(item));

        Assert.True(exception.Message.Length < 200, exception.Message);
    }

    [Fact]
    public void Null_text_is_rejected()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => PdfPageRange.Parse(null!));
        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void The_largest_page_number_is_accepted()
    {
        var ranges = PdfPageRange.Parse("2147483647");

        Assert.Equal(int.MaxValue, Assert.Single(ranges).FirstPage);
    }

    [Theory]
    [InlineData("1000000-1", 1_000_000)]
    [InlineData("1-1000000", 1)]
    [InlineData("1-500000,1-500000", 2)]
    public void Up_to_a_million_pages_may_be_described(string text, int rangeCount)
    {
        Assert.Equal(rangeCount, PdfPageRange.Parse(text).Count);
    }

    [Theory]
    [InlineData("1000001-1")]
    [InlineData("1-1000001")]
    [InlineData("2147483647-1")]
    [InlineData("1-500000,1-500001")]
    public void More_than_a_million_pages_fail_before_any_range_is_made(string text)
    {
        var exception = Assert.Throws<FormatException>(() => PdfPageRange.Parse(text));

        Assert.Contains("1,000,000", exception.Message);
    }

    [Fact]
    public void Parsed_ranges_split_a_file()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        var streams = new List<MemoryStream>();

        var parts = PdfOperations.Split(file, PdfPageRange.Parse("1-3, 8-10"), _ =>
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        });

        Assert.Equal("1-3 8-10", string.Join(" ", parts.Select(part => $"{part.FirstPage}-{part.LastPage}")));
        Assert.Equal(SplitFixtures.Texts(8, 10), SplitFixtures.PageTexts(streams[1].ToArray()));
    }

    #endregion

    #endregion
}
