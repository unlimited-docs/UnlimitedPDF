namespace UnlimitedPDF;

/// <summary>
/// The exception thrown when font data cannot be loaded: an unsupported format, damaged data, data over the file size
/// limit, a font whose embedding permissions forbid embedding, or a weight the font does not have. Null arguments and
/// file errors throw the standard .NET exceptions instead.
/// </summary>
public class PdfFontException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFontException"/> class.
    /// </summary>
    /// <param name="reason">Why the font could not be loaded.</param>
    /// <param name="message">A message naming the problem.</param>
    [AddedVersion("1.1.0")]
    [Description("Initializes a new instance of the PdfFontException class.")]
    public PdfFontException(PdfFontErrorReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFontException"/> class with an inner exception.
    /// </summary>
    /// <param name="reason">Why the font could not be loaded.</param>
    /// <param name="message">A message naming the problem.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    [AddedVersion("1.1.0")]
    [Description("Initializes a new instance of the PdfFontException class with an inner exception.")]
    public PdfFontException(PdfFontErrorReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets why the font could not be loaded.
    /// </summary>
    public PdfFontErrorReason Reason { get; }

    #endregion
}
