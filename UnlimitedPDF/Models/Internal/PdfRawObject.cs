namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// An object whose value is already PDF syntax as bytes, such as an object copied from an existing file. The writer
/// copies the bytes unchanged, so bytes above 0x7F survive (the text writer is ASCII).
/// </summary>
internal sealed class PdfRawObject : PdfObject
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the object.
    /// </summary>
    /// <param name="objectNumber">The object number in the document being written.</param>
    /// <param name="body">The value's bytes, for example a dictionary; for a stream, its dictionary with the right
    /// <c>/Length</c>.</param>
    /// <param name="streamData">The stream data, or null when the object is not a stream.</param>
    public PdfRawObject(int objectNumber, byte[] body, byte[]? streamData = null)
        : base(objectNumber, string.Empty)
    {
        Body = body;
        StreamData = streamData;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the object as Latin-1 text, for diagnostics.
    /// </summary>
    /// <returns>The object's syntax.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ObjectNumber} {GenerationNumber} obj");
        sb.AppendLine(Encoding.Latin1.GetString(Body));
        if (StreamData is not null)
        {
            sb.AppendLine("stream");
            sb.AppendLine(Encoding.Latin1.GetString(StreamData));
            sb.AppendLine("endstream");
        }
        sb.AppendLine("endobj");
        return sb.ToString();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the value's bytes.
    /// </summary>
    public byte[] Body { get; set; }

    /// <summary>
    /// Gets or sets the stream data, or null for an object that is not a stream.
    /// </summary>
    public byte[]? StreamData { get; set; }

    #endregion
}
