namespace UnlimitedPDF;

/// <summary>
/// The exception thrown when image data cannot be loaded: an unsupported format, damaged data, or an image over
/// the pixel limit. Null arguments and file errors throw the standard .NET exceptions instead.
/// </summary>
public class PdfImageException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageException"/> class.
    /// </summary>
    /// <param name="reason">Why the image could not be loaded.</param>
    /// <param name="message">A message naming the problem.</param>
    [AddedVersion("0.1.0")]
    [Description("Initializes a new instance of the PdfImageException class.")]
    public PdfImageException(PdfImageErrorReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageException"/> class with an inner exception.
    /// </summary>
    /// <param name="reason">Why the image could not be loaded.</param>
    /// <param name="message">A message naming the problem.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    [AddedVersion("0.1.0")]
    [Description("Initializes a new instance of the PdfImageException class with an inner exception.")]
    public PdfImageException(PdfImageErrorReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets why the image could not be loaded.
    /// </summary>
    public PdfImageErrorReason Reason { get; }

    #endregion
}
