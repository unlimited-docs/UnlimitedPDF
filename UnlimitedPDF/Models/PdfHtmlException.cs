namespace UnlimitedPDF;

/// <summary>
/// The exception thrown when HTML cannot be converted: input or resources over a limit, nesting deeper than the limit,
/// or a warning while <see cref="PdfHtmlLoadOptions.FailOnWarning"/> is set. It is thrown before any output is written.
/// Null arguments, file errors, and exceptions from a resource resolver are not wrapped.
/// </summary>
public class PdfHtmlException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfHtmlException"/> class.
    /// </summary>
    /// <param name="reason">Why the HTML could not be converted.</param>
    /// <param name="message">A message naming the problem.</param>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfHtmlException class.")]
    public PdfHtmlException(PdfHtmlErrorReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfHtmlException"/> class for a warning raised in strict mode.
    /// </summary>
    /// <param name="warning">The warning.</param>
    /// <exception cref="ArgumentNullException"><paramref name="warning"/> is null.</exception>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfHtmlException class for a warning raised in strict mode.")]
    public PdfHtmlException(PdfHtmlWarning warning)
        : base((warning ?? throw new ArgumentNullException(nameof(warning))).ToString())
    {
        Reason = PdfHtmlErrorReason.Warning;
        Warning = warning;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets why the HTML could not be converted.
    /// </summary>
    public PdfHtmlErrorReason Reason { get; }

    /// <summary>
    /// Gets the warning that failed the conversion in strict mode, or null.
    /// </summary>
    public PdfHtmlWarning? Warning { get; }

    #endregion
}
