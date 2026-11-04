using UnlimitedPDF.Enums;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The options of the Text and pages demo, with working defaults.
/// </summary>
public sealed class TextBasicsInput
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the body line under the heading.
    /// </summary>
    public string Body { get; set; } = "This PDF was generated in your browser by UnlimitedPDF, a pure .NET library.";

    /// <summary>
    /// Gets or sets the heading font size, in points.
    /// </summary>
    public int FontSize { get; set; } = 26;

    /// <summary>
    /// Gets or sets the heading text.
    /// </summary>
    public string Heading { get; set; } = "Hello from UnlimitedPDF";

    /// <summary>
    /// Gets or sets how many pages the document has.
    /// </summary>
    public int PageCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public PdfPageSize PageSize { get; set; } = PdfPageSize.A4;

    /// <summary>
    /// Gets or sets the heading's left edge, in points from the page's left edge.
    /// </summary>
    public int X { get; set; } = 50;

    /// <summary>
    /// Gets or sets the heading's baseline, in points from the page's bottom edge.
    /// </summary>
    public int Y { get; set; } = 760;

    #endregion
}
