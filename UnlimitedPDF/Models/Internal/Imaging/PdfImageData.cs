using System.Security.Cryptography;

namespace UnlimitedPDF.Models.Internal.Imaging;

/// <summary>
/// PDF-ready image data produced once at load: the image dictionary entries, the encoded data, an optional
/// transparency mask, and a content key that identifies identical images within a document.
/// </summary>
internal sealed class PdfImageData
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates image data. Arrays passed in are owned by this instance and must not be changed afterwards.
    /// </summary>
    /// <param name="pixelWidth">The width in pixels, as displayed.</param>
    /// <param name="pixelHeight">The height in pixels, as displayed.</param>
    /// <param name="dictionaryEntries">Image dictionary entries other than /Type, /Subtype, /Length, and /SMask.</param>
    /// <param name="encodedData">The stream data, already encoded for the dictionary's filter.</param>
    /// <param name="dpiX">The file's horizontal resolution, if any.</param>
    /// <param name="dpiY">The file's vertical resolution, if any.</param>
    /// <param name="softMask">The transparency mask image, if any.</param>
    /// <param name="orientation">The EXIF orientation (1 to 8) applied when the image is placed.</param>
    public PdfImageData(
        int pixelWidth,
        int pixelHeight,
        IReadOnlyList<string> dictionaryEntries,
        byte[] encodedData,
        double? dpiX,
        double? dpiY,
        PdfImageData? softMask = null,
        int orientation = ExifReader.Upright)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        DictionaryEntries = dictionaryEntries;
        EncodedData = encodedData;
        DpiX = dpiX;
        DpiY = dpiY;
        SoftMask = softMask;
        Orientation = orientation;

        // Orientation is applied at placement, so it is not part of the stored image and not part of the key.
        ContentKey = ComputeContentKey();
    }

    #endregion

    #region Methods

    #region Private Methods

    private string ComputeContentKey()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(string.Join("\n", DictionaryEntries)));
        hash.AppendData(new byte[] { 0 });
        hash.AppendData(EncodedData);
        if (SoftMask is not null)
            hash.AppendData(Encoding.ASCII.GetBytes("\nmask:" + SoftMask.ContentKey));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets a key that is equal for images with identical PDF data, used to store each image once per document.
    /// </summary>
    public string ContentKey { get; }

    /// <summary>
    /// Gets the image dictionary entries, for example "/Width 640" or "/Filter /DCTDecode".
    /// </summary>
    public IReadOnlyList<string> DictionaryEntries { get; }

    /// <summary>
    /// Gets the file's horizontal resolution in dots per inch, if any.
    /// </summary>
    public double? DpiX { get; }

    /// <summary>
    /// Gets the file's vertical resolution in dots per inch, if any.
    /// </summary>
    public double? DpiY { get; }

    /// <summary>
    /// Gets the encoded stream data.
    /// </summary>
    public byte[] EncodedData { get; }

    /// <summary>
    /// Gets the EXIF orientation (1 to 8) applied when the image is placed; 1 means upright as stored.
    /// </summary>
    public int Orientation { get; }

    /// <summary>
    /// Gets the height in pixels, as displayed.
    /// </summary>
    public int PixelHeight { get; }

    /// <summary>
    /// Gets the width in pixels, as displayed.
    /// </summary>
    public int PixelWidth { get; }

    /// <summary>
    /// Gets the transparency mask image, if any.
    /// </summary>
    public PdfImageData? SoftMask { get; }

    #endregion
}
