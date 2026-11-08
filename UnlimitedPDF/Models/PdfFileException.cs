namespace UnlimitedPDF;

/// <summary>
/// The exception thrown when an existing PDF cannot be loaded: it is encrypted, damaged, empty, or over a limit.
/// Null arguments and file errors throw the standard .NET exceptions instead.
/// </summary>
public class PdfFileException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileException"/> class.
    /// </summary>
    /// <param name="reason">Why the PDF could not be loaded.</param>
    /// <param name="message">A message naming the problem.</param>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfFileException class.")]
    public PdfFileException(PdfFileErrorReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileException"/> class with an inner exception.
    /// </summary>
    /// <param name="reason">Why the PDF could not be loaded.</param>
    /// <param name="message">A message naming the problem.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfFileException class with an inner exception.")]
    public PdfFileException(PdfFileErrorReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets why the PDF could not be loaded.
    /// </summary>
    public PdfFileErrorReason Reason { get; }

    #endregion
}
