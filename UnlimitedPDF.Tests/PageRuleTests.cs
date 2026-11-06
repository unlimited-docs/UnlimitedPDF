using UnlimitedPDF.Enums;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Page rules (WR-032): First, Last, Odd, Even, and page-number rules override the header, footer, body gaps, and side
/// margins; the most specific rule wins for each value; a Last rule may change content only.
/// </summary>
public class PageRuleTests
{
    #region Fields and Constants

    private const double A4Height = 841.89;
    private const int Precision = 6;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_first_page_rule_hides_the_header_and_gives_its_space_to_the_body()
    {
        var document = Document(pages: 2);
        document.AddPageRule(PdfPageRuleTarget.First).Header.IsHidden = true;

        var pages = PdfPageReader.Save(document);

        Assert.False(pages[0].Shows("H"));
        Assert.True(pages[1].Shows("H"));
        Assert.Equal(A4Height - LineOffset(12), FirstFlowBaseline(pages[0]), Precision);
        Assert.Equal(A4Height - 40 - LineOffset(12), FirstFlowBaseline(pages[1]), Precision);
    }

    [Fact]
    public void Odd_and_even_rules_change_side_margins_and_footer_content()
    {
        var document = Document(pages: 2);
        var odd = document.AddPageRule(PdfPageRuleTarget.Odd);
        odd.LeftMargin = 60;
        odd.Footer.AddText("odd", 0, 0);
        var even = document.AddPageRule(PdfPageRuleTarget.Even);
        even.RightMargin = 60;
        even.Footer.AddText("even", 0, 0);

        var pages = PdfPageReader.Save(document);

        Assert.True(pages[0].Shows("odd"));
        Assert.False(pages[0].Shows("F"));
        Assert.True(pages[1].Shows("even"));
        Assert.Equal(60, pages[0].LayoutTexts().First(t => t.Text.StartsWith("line")).X, Precision);
        Assert.Equal(0, pages[1].LayoutTexts().First(t => t.Text.StartsWith("line")).X, Precision);
    }

    [Fact]
    public void The_most_specific_rule_wins_for_each_value()
    {
        var document = Document(pages: 3);
        document.AddPageRule(PdfPageRuleTarget.Odd).Header.Height = 60;
        document.AddPageRule(PdfPageRuleTarget.First).Header.Height = 80;
        document.AddPageRule(3).Header.Height = 100;
        var laterOdd = document.AddPageRule(PdfPageRuleTarget.Odd);
        laterOdd.Header.Height = 70;
        laterOdd.BodyTopGap = 5;

        var pages = PdfPageReader.Save(document);

        // Page 1: First (80) beats Odd; Odd's gap still applies. Page 2: default 40. Page 3: page number (100) wins;
        // between the two Odd rules the later one sets the gap.
        Assert.Equal(A4Height - 80 - 5 - LineOffset(12), FirstFlowBaseline(pages[0]), Precision);
        Assert.Equal(A4Height - 40 - LineOffset(12), FirstFlowBaseline(pages[1]), Precision);
        Assert.Equal(A4Height - 100 - 5 - LineOffset(12), FirstFlowBaseline(pages[2]), Precision);
    }

    [Fact]
    public void A_last_page_rule_changes_content_only()
    {
        var document = Document(pages: 2);
        var last = document.AddPageRule(PdfPageRuleTarget.Last);
        last.Footer.AddText("End of report", 0, 0);

        var pages = PdfPageReader.Save(document);

        Assert.True(pages[0].Shows("F"));
        Assert.True(pages[1].Shows("End of report"));
        Assert.False(pages[1].Shows("F"));
        Assert.Throws<InvalidOperationException>(() => last.Footer.Height = 10);
        Assert.Throws<InvalidOperationException>(() => last.Header.IsHidden = true);
        Assert.Throws<InvalidOperationException>(() => last.BodyTopGap = 10);
        Assert.Throws<InvalidOperationException>(() => last.LeftMargin = 10);
    }

    [Fact]
    public void Page_number_rules_start_at_one_and_rules_past_the_last_page_never_match()
    {
        var document = Document(pages: 1);
        document.AddPageRule(5).Header.Height = 300;

        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPageRule(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPageRule((PdfPageRuleTarget)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPageRule(PdfPageRuleTarget.Even).BodyBottomGap = -1);
        Assert.Single(PdfPageReader.Save(document));
    }

    #endregion

    #region Private Methods

    // A document whose flow fills the given number of pages, with a 40 pt header "H" and a 20 pt footer "F".
    private static PdfDocument Document(int pages)
    {
        var document = new PdfDocument();
        document.Header.Height = 40;
        document.Header.AddText("H", 0, 0);
        document.Footer.Height = 20;
        document.Footer.AddText("F", 0, 0);
        for (int p = 0; p < pages; p++)
        {
            if (p > 0) document.Body.PageBreak();
            document.Body.AddText("line " + p);
        }
        return document;
    }

    private static double FirstFlowBaseline(PdfPageReader.Page page) => page.LayoutTexts().First(t => t.Text.StartsWith("line")).Baseline;

    private static double LineOffset(double size) => size * 1.2 / 2 + size / 3;

    #endregion

    #endregion
}
