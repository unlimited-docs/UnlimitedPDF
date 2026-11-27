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
    /// Creates a stream the library writes itself, such as a <c>ToUnicode</c> map or a stream the page importer adds
    /// (WR-042). It keeps the unpacked bytes, so each save writes it compressed or plain with
    /// <see cref="ApplyCompression"/>. Until then it is written plain.
    /// </summary>
    /// <param name="objectNumber">The object number in the document being written.</param>
    /// <param name="data">The stream's unpacked bytes.</param>
    /// <returns>The object.</returns>
    public static PdfRawObject LibraryStream(int objectNumber, byte[] data)
    {
        var stream = new PdfRawObject(objectNumber, Array.Empty<byte>());
        stream.SetLibraryStream(data);
        return stream;
    }

    /// <summary>
    /// Writes a library stream (see <see cref="LibraryStream"/>) compressed with <see cref="FlateEncoder"/> when
    /// <paramref name="compress"/> is true, else plain: its data and a dictionary with the matching <c>/Length</c>,
    /// and <c>/Filter /FlateDecode</c> when compressed. Does nothing for any other object.
    /// </summary>
    /// <param name="compress">Whether to write the stream compressed (<see cref="PdfDocument.CompressStreams"/>).</param>
    public void ApplyCompression(bool compress)
    {
        if (UnpackedStreamData is not { } data) return;
        var written = compress ? FlateEncoder.Compress(data) : data;
        var length = written.Length.ToString(CultureInfo.InvariantCulture);
        Body = Encoding.ASCII.GetBytes(compress ? $"<< /Length {length} /Filter {FlateEncoder.FilterName} >>" : $"<< /Length {length} >>");
        StreamData = written;
    }

    /// <summary>
    /// Makes this object a library stream holding <paramref name="data"/>, written plain until
    /// <see cref="ApplyCompression"/> runs.
    /// </summary>
    /// <param name="data">The stream's unpacked bytes.</param>
    public void SetLibraryStream(byte[] data)
    {
        UnpackedStreamData = data;
        ApplyCompression(compress: false);
    }

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

    /// <summary>
    /// Gets the unpacked bytes of a stream the library writes itself, or null for any other object, such as one
    /// copied from an existing file, whose bytes are always written as they are.
    /// </summary>
    public byte[]? UnpackedStreamData { get; private set; }

    #endregion
}
