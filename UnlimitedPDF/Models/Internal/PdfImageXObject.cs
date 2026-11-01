namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// An image XObject: the stream object that holds one image's data, written once per document however many times
/// the image is placed.
/// </summary>
internal sealed class PdfImageXObject : PdfStreamObject
{
    #region Fields and Constants

    private readonly PdfImageData _image;
    private readonly int? _softMaskObjectNumber;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the XObject for <paramref name="image"/>.
    /// </summary>
    /// <param name="objectNumber">The object number.</param>
    /// <param name="image">The PDF-ready image data.</param>
    /// <param name="softMaskObjectNumber">The object number of the image's transparency mask, if it has one.</param>
    public PdfImageXObject(int objectNumber, PdfImageData image, int? softMaskObjectNumber)
        : base(objectNumber, string.Empty)
    {
        _image = image;
        _softMaskObjectNumber = softMaskObjectNumber;
        StreamData = image.EncodedData;
    }

    #endregion

    #region Methods

    #region Internal Methods

    /// <inheritdoc />
    internal override string GetStreamDictionary()
    {
        var softMask = _softMaskObjectNumber is int mask ? $" /SMask {new PdfIndirectReference(mask)}" : string.Empty;
        return $"<< /Type /XObject /Subtype /Image {string.Join(" ", _image.DictionaryEntries)}{softMask} /Length {StreamData.Length} >>";
    }

    #endregion

    #endregion
}
