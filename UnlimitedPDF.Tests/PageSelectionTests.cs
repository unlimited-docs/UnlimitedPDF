using UnlimitedPDF.Models.Internal.Importing;

namespace UnlimitedPDF.Tests;

/// <summary>
/// One check for every page selection: split, extract, remove, and adding a selection to a document fail the same
/// way before any output, and a selection keeps its order and repeats (WR-037, WR-040, AC-01, AC-13).
/// </summary>
public class PageSelectionTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Indexes_follow_the_order_given_with_repeats()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));

        var indexes = PageSelection.Expand(file, PdfPageRange.Parse("9,2,2-3"), "ranges");

        Assert.Equal(new[] { 8, 1, 1, 2 }, indexes);
    }

    [Fact]
    public void A_range_past_the_last_page_names_the_range_and_the_page_count()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PageSelection.Expand(file, PdfPageRange.Parse("2,5,12"), "ranges"));

        Assert.Equal("ranges", exception.ParamName);
        Assert.Contains("Page range 3 (12)", exception.Message);
        Assert.Contains("last page, 10", exception.Message);
    }

    [Fact]
    public void An_empty_selection_is_rejected()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));

        var exception = Assert.Throws<ArgumentException>(() => PageSelection.Expand(file, Array.Empty<PdfPageRange>(), "ranges"));

        Assert.Equal("ranges", exception.ParamName);
    }

    [Fact]
    public void A_null_list_or_range_is_rejected()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));

        Assert.Equal("ranges", Assert.Throws<ArgumentNullException>(() => PageSelection.Expand(file, null, "ranges")).ParamName);
        var exception = Assert.Throws<ArgumentNullException>(() => PageSelection.Expand(file, new[] { new PdfPageRange(1, 1), null! }, "ranges"));
        Assert.Contains("Page range 2 is null", exception.Message);
    }

    #endregion

    #endregion
}
