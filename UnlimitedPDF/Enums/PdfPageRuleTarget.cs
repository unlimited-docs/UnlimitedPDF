namespace UnlimitedPDF.Enums;

/// <summary>
/// The pages a page rule applies to. A rule for one page number is added with <c>AddPageRule(int)</c>.
/// </summary>
public enum PdfPageRuleTarget
{
    /// <summary>
    /// The first saved page.
    /// </summary>
    First,

    /// <summary>
    /// The last saved page. A Last rule may change header and footer content only (WR-032).
    /// </summary>
    Last,

    /// <summary>
    /// Pages with an odd number.
    /// </summary>
    Odd,

    /// <summary>
    /// Pages with an even number.
    /// </summary>
    Even
}
