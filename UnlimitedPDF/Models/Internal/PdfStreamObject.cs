namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents a PDF object that contains a stream of data, such as page content or an image.
/// </summary>
internal class PdfStreamObject : PdfObject
{
    #region Constructors and Finalizers

    public PdfStreamObject(int objectNumber, string content, int generationNumber = 0)
        : base(objectNumber, content, generationNumber)
    {
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the string representation of the PDF stream object.
    /// </summary>
    /// <returns>A string representing the complete PDF stream object, including its dictionary and data.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ObjectNumber} {GenerationNumber} obj");
        sb.AppendLine(GetStreamDictionary());
        sb.AppendLine("stream");
        sb.AppendLine(Encoding.ASCII.GetString(StreamData ?? new byte[0]));
        sb.AppendLine("endstream");
        sb.AppendLine("endobj");
        return sb.ToString();
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the stream dictionary written before the stream data. Both document writers use it, so every
    /// stream type describes its data in one place.
    /// </summary>
    /// <returns>The stream dictionary, for example "&lt;&lt; /Length 42 &gt;&gt;".</returns>
    internal virtual string GetStreamDictionary()
    {
        return $"<< /Length {StreamData.Length} >>";
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the raw byte data for the stream.
    /// </summary>
    public byte[] StreamData { get; set; } = Array.Empty<byte>();

    #endregion
}
