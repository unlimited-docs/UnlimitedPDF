namespace UnlimitedPDF;

/// <summary>
/// One part of a split (WR-037): its number, the source pages it holds, and its file name. Returned by
/// <see cref="PdfOperations"/> split methods and passed to the callback that supplies each part's stream.
/// </summary>
public sealed class PdfSplitPart
{
    #region Constructors and Finalizers

    internal PdfSplitPart(int number, int firstPage, int lastPage, string fileName)
    {
        Number = number;
        FirstPage = firstPage;
        LastPage = lastPage;
        FileName = fileName;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the file name of the part, for example <c>report-2.pdf</c>: the source file name, or <c>part</c> when the
    /// file was not loaded from a path, then the part number padded with zeros to the width of the part count.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the 1-based number, in the source file, of the part's first page.
    /// </summary>
    public int FirstPage { get; }

    /// <summary>
    /// Gets the 1-based number, in the source file, of the part's last page.
    /// </summary>
    public int LastPage { get; }

    /// <summary>
    /// Gets the 1-based number of the part, in the order the parts are written.
    /// </summary>
    public int Number { get; }

    #endregion
}
