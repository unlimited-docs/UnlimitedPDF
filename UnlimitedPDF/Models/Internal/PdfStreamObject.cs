namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Represents a PDF object that contains a stream of data, such as page content or an image.
/// </summary>
internal class PdfStreamObject : PdfObject
{
    #region Fields and Constants

    private byte[] _streamData = Array.Empty<byte>();

    #endregion

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
        // Latin-1 maps each byte to one character, so compressed data is shown, never altered by a text encoding (A08).
        sb.AppendLine(Encoding.Latin1.GetString(StreamData));
        sb.AppendLine("endstream");
        sb.AppendLine("endobj");
        return sb.ToString();
    }

    /// <summary>
    /// Stores the stream's data, compressed with <see cref="FlateEncoder"/> when <paramref name="compress"/> is true
    /// (WR-042); the stream dictionary then names the filter.
    /// </summary>
    /// <param name="data">The stream's unpacked bytes.</param>
    /// <param name="compress">Whether to store the data compressed.</param>
    public void SetData(byte[] data, bool compress)
    {
        StreamData = compress ? FlateEncoder.Compress(data) : data;
        IsFlateEncoded = compress;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the stream dictionary written before the stream data. Both document writers use it, so every
    /// stream type describes its data in one place.
    /// </summary>
    /// <returns>The stream dictionary, for example "&lt;&lt; /Length 42 &gt;&gt;", or
    /// "&lt;&lt; /Length 17 /Filter /FlateDecode &gt;&gt;" for compressed data.</returns>
    internal virtual string GetStreamDictionary()
    {
        var length = StreamData.Length.ToString(CultureInfo.InvariantCulture);
        return IsFlateEncoded ? $"<< /Length {length} /Filter {FlateEncoder.FilterName} >>" : $"<< /Length {length} >>";
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets whether <see cref="StreamData"/> holds compressed data, written with <c>/Filter /FlateDecode</c>.
    /// </summary>
    public bool IsFlateEncoded { get; private set; }

    /// <summary>
    /// Gets or sets the stream's data as written. Setting it stores the data as given, not compressed; use
    /// <see cref="SetData"/> to compress.
    /// </summary>
    public byte[] StreamData
    {
        get => _streamData;
        set
        {
            _streamData = value;
            IsFlateEncoded = false;
        }
    }

    #endregion
}
