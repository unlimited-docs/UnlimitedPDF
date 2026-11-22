using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// A tool's page grid and its typed page list stay in step: clicks rewrite the list in page order, a typed list picks
/// its pages, and a list that can't be used says why (26112201 AC-04, AC-05; WCAG 2.1.1).
/// </summary>
public class PageSelectionToolTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Clicking_pages_writes_the_list_in_page_order()
    {
        var selection = new PageSelection(8);

        foreach (int page in new[] { 5, 1, 3, 4 }) selection.Toggle(page);

        Assert.Equal("1, 3-5", selection.Text);
        Assert.Equal([1, 3, 4, 5], selection.Pages);
        Assert.True(selection.IsUsable);

        selection.Toggle(4);
        Assert.Equal("1, 3, 5", selection.Text);
    }

    [Fact]
    public void A_typed_list_picks_its_pages_in_its_order()
    {
        var selection = new PageSelection(8);

        selection.SetText("7-5, 2");

        Assert.Equal([7, 6, 5, 2], selection.Pages);
        Assert.True(selection.Contains(6));
        Assert.False(selection.Contains(1));
        Assert.Equal("7-5, 2", selection.Text);
    }

    [Theory]
    [InlineData("1-", "1-")]
    [InlineData("odd", "odd")]
    [InlineData("9", "Page 9 is past the last page (8).")]
    public void A_list_that_cant_be_used_says_why_and_keeps_the_earlier_pages(string text, string expected)
    {
        var selection = new PageSelection(8);
        selection.SetText("1, 2");

        selection.SetText(text);

        Assert.False(selection.IsUsable);
        Assert.Contains(expected, selection.Error, StringComparison.Ordinal);
        Assert.Equal([1, 2], selection.Pages);
    }

    [Fact]
    public void All_clear_odd_and_even_pick_as_named()
    {
        var selection = new PageSelection(7);

        selection.SelectOdd();
        Assert.Equal("1, 3, 5, 7", selection.Text);
        selection.SelectEven();
        Assert.Equal("2, 4, 6", selection.Text);
        selection.SelectAll();
        Assert.Equal("1-7", selection.Text);
        selection.Clear();
        Assert.Equal(string.Empty, selection.Text);
        Assert.False(selection.IsUsable);
    }

    [Theory]
    [InlineData(new int[0], "")]
    [InlineData(new[] { 1, 2 }, "1, 2")]
    [InlineData(new[] { 1, 2, 3, 7, 9, 10, 11, 12 }, "1-3, 7, 9-12")]
    public void Compact_writes_runs_of_three_or_more_as_ranges(int[] pages, string expected)
    {
        Assert.Equal(expected, PageSelection.Compact(pages));
    }

    #endregion

    #endregion
}
