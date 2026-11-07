namespace UnlimitedPDF;

/// <summary>
/// The pages that page settings apply to. Page settings for one page number are added with <c>AddPageSettings(int)</c>.
/// </summary>
public enum PdfPageTarget
{
    /// <summary>
    /// The first saved page.
    /// </summary>
    First,

    /// <summary>
    /// The last saved page. Last-page settings may change header and footer content only (WR-032).
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
