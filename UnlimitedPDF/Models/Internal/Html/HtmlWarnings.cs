namespace UnlimitedPDF.Models.Internal.Html;

/// <summary>
/// Collects the warnings of one HTML input (WR-038). In strict mode the first warning throws instead.
/// </summary>
internal sealed class HtmlWarnings
{
    #region Fields and Constants

    private readonly List<PdfHtmlWarning> _items = new();

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the collector.
    /// </summary>
    /// <param name="strict">Whether the first warning throws a <see cref="PdfHtmlException"/>.</param>
    public HtmlWarnings(bool strict)
    {
        Strict = strict;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a warning, or throws it in strict mode.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="source">The element, property, or reference.</param>
    /// <param name="message">The message.</param>
    /// <param name="line">The 1-based input line, or null.</param>
    /// <param name="pageNumber">The 1-based page number for a layout warning, or null.</param>
    /// <exception cref="PdfHtmlException">Strict mode is on.</exception>
    public void Add(PdfHtmlWarningKind kind, string source, string message, int? line = null, int? pageNumber = null)
    {
        var warning = new PdfHtmlWarning(kind, source, message, line, pageNumber);
        if (Strict) throw new PdfHtmlException(warning);
        _items.Add(warning);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the warnings in the order they occurred.
    /// </summary>
    public IReadOnlyList<PdfHtmlWarning> Items => _items;

    /// <summary>
    /// Gets whether the first warning throws.
    /// </summary>
    public bool Strict { get; }

    #endregion
}
