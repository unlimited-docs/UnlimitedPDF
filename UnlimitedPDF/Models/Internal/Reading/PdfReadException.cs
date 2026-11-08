namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Thrown inside the reader when an existing PDF cannot be read. The public load methods turn it into the public
/// exception.
/// </summary>
internal sealed class PdfReadException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="error">Why reading failed.</param>
    /// <param name="message">What was wrong, for the caller.</param>
    public PdfReadException(PdfReadError error, string message)
        : base(message)
    {
        Error = error;
    }

    #endregion

    #region Methods

    #region Internal Methods

    /// <summary>
    /// Creates a <see cref="PdfReadError.Damaged"/> exception.
    /// </summary>
    /// <param name="message">What was wrong.</param>
    /// <returns>The exception, to throw.</returns>
    internal static PdfReadException Damaged(string message) => new(PdfReadError.Damaged, message);

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets why reading failed.
    /// </summary>
    public PdfReadError Error { get; }

    #endregion
}
