namespace UnlimitedPDF;

/// <summary>
/// Something in the HTML that was skipped or could not be shown as written (WR-038): an unsupported element or CSS
/// feature, a resource that could not be loaded, a dropped link, or clipped content. The HTML is still converted unless
/// <see cref="PdfHtmlLoadOptions.FailOnWarning"/> is set.
/// </summary>
public sealed class PdfHtmlWarning
{
    #region Constructors and Finalizers

    internal PdfHtmlWarning(PdfHtmlWarningKind kind, string source, string message, int? line = null, int? pageNumber = null)
    {
        Kind = kind;
        Source = source;
        Message = message;
        Line = line;
        PageNumber = pageNumber;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the warning as "Kind: message".
    /// </summary>
    /// <returns>The text.</returns>
    [AddedVersion("1.0.0")]
    [Description("Returns the warning as kind and message.")]
    public override string ToString() => $"{Kind}: {Message}";

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets what the warning reports.
    /// </summary>
    public PdfHtmlWarningKind Kind { get; }

    /// <summary>
    /// Gets the 1-based line of the HTML input the warning comes from, or null for a warning found during layout.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Gets a message describing the warning.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the 1-based page number of a warning found during layout, such as clipped content, or null.
    /// </summary>
    public int? PageNumber { get; }

    /// <summary>
    /// Gets what the warning is about: an element name, a CSS property, or a resource reference as written.
    /// </summary>
    public string Source { get; }

    #endregion
}
