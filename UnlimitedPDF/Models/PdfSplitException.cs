namespace UnlimitedPDF;

/// <summary>
/// The exception thrown when writing a split part fails (WR-037). The parts before <see cref="PartNumber"/> were
/// written and kept; a half-written part file was removed; no later part was written. The cause is in
/// <see cref="Exception.InnerException"/>. Invalid arguments throw the standard .NET exceptions instead, and
/// cancellation throws <see cref="OperationCanceledException"/>.
/// </summary>
public class PdfSplitException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSplitException"/> class.
    /// </summary>
    /// <param name="message">A message naming the part that failed.</param>
    /// <param name="partNumber">The 1-based number of the part that failed.</param>
    /// <param name="partsWritten">How many parts were written before it.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    [AddedVersion("1.0.0")]
    [Description("Initializes a new instance of the PdfSplitException class.")]
    public PdfSplitException(string message, int partNumber, int partsWritten, Exception innerException)
        : base(message, innerException)
    {
        PartNumber = partNumber;
        PartsWritten = partsWritten;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the 1-based number of the part that failed.
    /// </summary>
    public int PartNumber { get; }

    /// <summary>
    /// Gets how many parts were written, and kept, before the failure.
    /// </summary>
    public int PartsWritten { get; }

    #endregion
}
