using UnlimitedPDF.Models.Internal.Imaging;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

internal sealed partial class PageInterpreter
{
    #region Fields and Constants

    // The /Decode array the library writes on Adobe CMYK JPEGs.
    private const string InvertedCmykDecode = "/Decode [1 0 1 0 1 0 1 0]";

    // Image XObjects already written to the drawing's definitions, by object number, so a repeated image is stored once.
    private readonly Dictionary<int, string> _imageIds = new();
    private long _imagePixels;
    private int _images;

    #endregion

    #region Methods

    #region Private Methods

    // Draws the XObject named by Do.
    private void DrawXObject(IReadOnlyList<PdfReadValue> operands, PdfReadDictionary? resources)
    {
        if (operands.Count < 1 || operands[^1] is not PdfReadName name) throw new PageRenderException("An XObject is named wrongly.");
        var objects = _objects.Resolve(resources?["XObject"]) as PdfReadDictionary;
        var reference = objects?[name.Value];
        if (_objects.Resolve(reference) is not PdfReadStream stream) throw new PageRenderException("The page draws an XObject it doesn't have.");

        string subtype = (_objects.Resolve(stream.Dictionary["Subtype"]) as PdfReadName)?.Value ?? string.Empty;
        if (subtype == "Image")
        {
            DrawImage(stream, reference as PdfReadReference);
        }
        else if (subtype == "Form")
        {
            DrawForm(stream);
        }
        else
        {
            throw new PageRenderException($"The page draws an XObject of type {subtype}, which this preview doesn't draw.");
        }
    }

    private void DrawImage(PdfReadStream image, PdfReadReference? reference)
    {
        if (reference is null || !_imageIds.TryGetValue(reference.ObjectNumber, out string? id))
        {
            string dataUri = ImageDataUri(image);
            id = NewId("i");
            _defs.Append("<image id=\"").Append(id).Append("\" width=\"1\" height=\"1\" preserveAspectRatio=\"none\" href=\"")
                .Append(dataUri).Append("\"/>");
            if (reference is not null) _imageIds[reference.ObjectNumber] = id;
        }

        // An image fills the unit square of user space with its first row at the top.
        var placement = new RenderMatrix(1, 0, 0, -1, 0, 1).Then(_state.Ctm);
        _body.Append("<use href=\"#").Append(id).Append("\" transform=\"");
        SvgText.AppendMatrix(_body, placement);
        _body.Append("\"/>");
        _images++;
    }

    // Turns an image XObject into a data URI the browser can show: JPEG data as it is, other images as PNG.
    private string ImageDataUri(PdfReadStream image)
    {
        var dictionary = image.Dictionary;
        if (_objects.Resolve(dictionary["ImageMask"]) is PdfReadBoolean { Value: true })
            throw new PageRenderException("The page has a stencil mask image, which this preview doesn't draw.");

        int width = IntegerEntry(dictionary, "Width");
        int height = IntegerEntry(dictionary, "Height");
        if (width <= 0 || height <= 0) throw new PageRenderException("The page has an image without a size.");
        _imagePixels += (long)width * height;
        if (_imagePixels > _limits.MaxImagePixels) throw new PageRenderException("The page has too many image pixels.");

        var filters = _objects.Resolve(dictionary["Filter"]) switch
        {
            PdfReadName single => new[] { single.Value },
            PdfReadArray array => array.Items.Select(item => (_objects.Resolve(item) as PdfReadName)?.Value ?? string.Empty).ToArray(),
            _ => Array.Empty<string>(),
        };

        if (filters.Length == 1 && filters[0] is "DCTDecode" or "DCT") return JpegDataUri(image, width, height);
        if (filters.Any(filter => filter is "DCTDecode" or "DCT")) throw new PageRenderException("The page has a JPEG image behind another filter, which this preview doesn't draw.");

        if (dictionary.ContainsKey("Decode")) throw new PageRenderException("The page has an image with a decode array, which this preview doesn't draw.");

        // Every buffer below is sized by the image, so the image must fit the drawing before anything is allocated.
        if ((long)width * height * 4 > _limits.MaxOutputCharacters) throw new PageRenderException("The page drawing is too large.");

        int bits = IntegerEntry(dictionary, "BitsPerComponent");
        byte[] samples = DecodeImageData(image, EncodedSize(width, height, ColorComponents(dictionary), bits));
        var (channels, rgb) = ImageSamples(dictionary, samples, width, height);
        byte[]? alpha = Alpha(dictionary, samples, channels, width, height);

        byte[] pixels;
        int outChannels;
        if (alpha is null)
        {
            pixels = rgb;
            outChannels = channels;
        }
        else
        {
            outChannels = 4;
            pixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                for (int c = 0; c < 3; c++) pixels[i * 4 + c] = rgb[i * channels + (channels == 1 ? 0 : c)];
                pixels[i * 4 + 3] = alpha[i];
            }
        }
        byte[] png = PngEncoder.Encode(width, height, outChannels, pixels);
        CheckOutputRoom(png.Length);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    // The base64 form of n bytes takes 4 characters for every 3 bytes; it must fit what is left of the drawing.
    private void CheckOutputRoom(long bytes)
    {
        if (_body.Length + _defs.Length + (bytes + 2) / 3 * 4 > _limits.MaxOutputCharacters)
            throw new PageRenderException("The page drawing is too large.");
    }

    // The number of color components each sample row holds: 1 for gray and indexed images, 3 for RGB.
    private int ColorComponents(PdfReadDictionary dictionary) => _objects.Resolve(dictionary["ColorSpace"]) switch
    {
        PdfReadName { Value: "DeviceRGB" or "RGB" } => 3,
        _ => 1,
    };

    // The most bytes an image's Flate data may inflate to: its rows, plus one predictor byte per row.
    private static long EncodedSize(int width, int height, int components, int bits) =>
        ((long)width * components * bits + 7) / 8 * height + height;

    // JPEG data goes to the browser as it is, so it must be exactly what the dictionary says: the frame's size and
    // components match /Width, /Height, and /ColorSpace, and a /Decode array appears only where the library writes
    // one, on Adobe CMYK JPEGs whose values are stored inverted (browsers invert those themselves).
    private string JpegDataUri(PdfReadStream image, int width, int height)
    {
        var dictionary = image.Dictionary;
        if (dictionary.ContainsKey("SMask") || dictionary.ContainsKey("Mask"))
            throw new PageRenderException("The page has a masked JPEG image, which this preview doesn't draw.");

        byte[] jpeg = image.Data.ToArray();
        CheckOutputRoom(jpeg.Length);
        PdfImageData frame;
        try
        {
            frame = JpegReader.Read(jpeg, new ImageLoadSettings(_limits.MaxImagePixels, PdfCmykInversion.Auto, ApplyExifOrientation: false), CancellationToken.None);
        }
        catch (PdfImageException exception)
        {
            throw new PageRenderException($"A JPEG image on the page can't be read: {exception.Message}");
        }

        var expected = frame.DictionaryEntries;
        string space = (_objects.Resolve(dictionary["ColorSpace"]) as PdfReadName)?.Value ?? string.Empty;
        bool decodeExpected = expected.Contains(InvertedCmykDecode);
        bool decodeGiven = _objects.Resolve(dictionary["Decode"]) is PdfReadArray decode
            && decode.Count == 8
            && decode.Items.Select((item, i) => _objects.Resolve(item) is PdfReadNumber n && n.Value == (i % 2 == 0 ? 1 : 0)).All(ok => ok);
        if (frame.PixelWidth != width || frame.PixelHeight != height
            || !expected.Contains($"/ColorSpace /{space}")
            || (dictionary.ContainsKey("BitsPerComponent") && IntegerEntry(dictionary, "BitsPerComponent") != 8)
            || decodeExpected != decodeGiven
            || (dictionary.ContainsKey("Decode") && !decodeGiven))
            throw new PageRenderException("The page has a JPEG image whose dictionary doesn't match its data.");

        return "data:image/jpeg;base64," + Convert.ToBase64String(WithoutExif(jpeg));
    }

    // Removes EXIF segments: the PDF turns an image through its placement matrix, and a browser would turn it again.
    private static byte[] WithoutExif(byte[] jpeg)
    {
        using var output = new MemoryStream(jpeg.Length);
        output.Write(jpeg, 0, 2);
        int p = 2;
        while (p + 4 <= jpeg.Length && jpeg[p] == 0xFF)
        {
            byte marker = jpeg[p + 1];
            if (marker == 0xDA) break; // start of scan: the rest is image data
            int length = (jpeg[p + 2] << 8) | jpeg[p + 3];
            bool exif = marker == 0xE1 && length >= 8 && p + 10 <= jpeg.Length
                && jpeg[p + 4] == (byte)'E' && jpeg[p + 5] == (byte)'x' && jpeg[p + 6] == (byte)'i' && jpeg[p + 7] == (byte)'f';
            if (!exif) output.Write(jpeg, p, Math.Min(length + 2, jpeg.Length - p));
            p += length + 2;
        }
        if (p < jpeg.Length) output.Write(jpeg, p, jpeg.Length - p);
        return output.ToArray();
    }

    // The image's colors as 8-bit gray (1 channel) or RGB (3 channels), row by row.
    private (int Channels, byte[] Pixels) ImageSamples(PdfReadDictionary dictionary, byte[] samples, int width, int height)
    {
        int bits = IntegerEntry(dictionary, "BitsPerComponent");
        var space = _objects.Resolve(dictionary["ColorSpace"]);
        if (space is PdfReadName { Value: "DeviceGray" or "G" }) return (1, Unpack(samples, width, height, 1, bits, scale: true));
        if (space is PdfReadName { Value: "DeviceRGB" or "RGB" }) return (3, Unpack(samples, width, height, 3, bits, scale: true));
        if (space is PdfReadArray { Count: 4 } indexed
            && _objects.Resolve(indexed[0]) is PdfReadName { Value: "Indexed" or "I" }
            && _objects.Resolve(indexed[1]) is PdfReadName { Value: "DeviceRGB" or "RGB" }
            && _objects.Resolve(indexed[2]) is PdfReadNumber hival)
        {
            byte[] table = _objects.Resolve(indexed[3]) switch
            {
                PdfReadString text => text.Bytes,
                PdfReadStream stream => DecodeImageData(stream, 768),
                _ => throw new PageRenderException("An indexed image has no color table."),
            };
            int entries = (int)Math.Clamp(hival.IntegerValue, 0, 255) + 1;
            byte[] indexes = Unpack(samples, width, height, 1, bits, scale: false);
            var rgb = new byte[width * height * 3];
            for (int i = 0; i < width * height; i++)
            {
                int entry = Math.Min(indexes[i], entries - 1) * 3;
                for (int c = 0; c < 3; c++) rgb[i * 3 + c] = entry + c < table.Length ? table[entry + c] : (byte)0;
            }
            return (3, rgb);
        }
        throw new PageRenderException("The page has an image in a color space this preview doesn't draw.");
    }

    // The alpha channel from a soft mask (/SMask) or a color-key mask (/Mask), or null when the image is opaque.
    private byte[]? Alpha(PdfReadDictionary dictionary, byte[] samples, int channels, int width, int height)
    {
        if (_objects.Resolve(dictionary["SMask"]) is PdfReadStream mask)
        {
            var maskDictionary = mask.Dictionary;
            if (IntegerEntry(maskDictionary, "Width") != width || IntegerEntry(maskDictionary, "Height") != height)
                throw new PageRenderException("The page has an image whose soft mask has another size.");
            if (_objects.Resolve(maskDictionary["ColorSpace"]) is not PdfReadName { Value: "DeviceGray" or "G" })
                throw new PageRenderException("The page has a soft mask in a color space this preview doesn't draw.");
            if (maskDictionary.ContainsKey("Matte") || maskDictionary.ContainsKey("Decode") || maskDictionary.ContainsKey("SMask"))
                throw new PageRenderException("The page has a soft mask with entries this preview doesn't draw.");
            _imagePixels += (long)width * height;
            if (_imagePixels > _limits.MaxImagePixels) throw new PageRenderException("The page has too many image pixels.");
            int maskBits = IntegerEntry(maskDictionary, "BitsPerComponent");
            return Unpack(DecodeImageData(mask, EncodedSize(width, height, 1, maskBits)), width, height, 1, maskBits, scale: true);
        }

        if (_objects.Resolve(dictionary["Mask"]) is PdfReadArray key)
        {
            // Color-key masking: a pixel whose every component falls in its [min max] range of sample values is
            // transparent. The ranges compare with the samples as written, so they are read again unscaled.
            int components = IsIndexed(dictionary) ? 1 : channels;
            if (key.Count != components * 2) throw new PageRenderException("The page has a color-key mask this preview doesn't draw.");
            var ranges = key.Items.Select(item => _objects.Resolve(item) is PdfReadNumber n ? n.IntegerValue : -1).ToArray();
            var raw = RawSamples(samples, width, height, components, IntegerEntry(dictionary, "BitsPerComponent"));
            var alpha = new byte[width * height];
            for (int i = 0; i < width * height; i++)
            {
                bool keyed = true;
                for (int c = 0; c < components && keyed; c++)
                {
                    int value = raw[i * components + c];
                    keyed = value >= ranges[c * 2] && value <= ranges[c * 2 + 1];
                }
                alpha[i] = keyed ? (byte)0 : (byte)255;
            }
            return alpha;
        }

        if (dictionary.ContainsKey("Mask")) throw new PageRenderException("The page has a stencil-masked image, which this preview doesn't draw.");
        return null;
    }

    // Decodes image samples, which count toward the page's image pixels rather than its content bytes. A stream may
    // inflate only to the size its image needs, so a small stream can't expand into a huge buffer (A06).
    private byte[] DecodeImageData(PdfReadStream stream, long maxBytes)
    {
        try
        {
            return PdfStreamDecoder.Decode(stream, _objects.Resolve, maxBytes);
        }
        catch (PdfReadException exception)
        {
            throw new PageRenderException($"An image on the page can't be read: {exception.Message}");
        }
    }

    private bool IsIndexed(PdfReadDictionary dictionary) =>
        _objects.Resolve(dictionary["ColorSpace"]) is PdfReadArray { Count: > 0 } space && _objects.Resolve(space[0]) is PdfReadName { Value: "Indexed" or "I" };

    private int IntegerEntry(PdfReadDictionary dictionary, string key) =>
        _objects.Resolve(dictionary[key]) is PdfReadNumber number && number.IntegerValue is >= 0 and <= int.MaxValue
            ? (int)number.IntegerValue
            : throw new PageRenderException($"An image has no valid /{key}.");

    // Reads rows of 1-, 2-, 4-, 8-, or 16-bit samples as their values, one per sample.
    private static ushort[] RawSamples(byte[] data, int width, int height, int components, int bits)
    {
        if (bits is not (1 or 2 or 4 or 8 or 16)) throw new PageRenderException("The page has an image with an unusual sample size.");
        long samplesPerRow = (long)width * components;
        long rowBytes = (samplesPerRow * bits + 7) / 8;
        if (rowBytes * height > data.Length) throw new PageRenderException("The page has an image with too little data.");

        var output = new ushort[samplesPerRow * height];
        int mask = (1 << Math.Min(bits, 8)) - 1;
        for (int y = 0; y < height; y++)
        {
            long row = y * rowBytes;
            for (long s = 0; s < samplesPerRow; s++)
            {
                output[y * samplesPerRow + s] = bits switch
                {
                    16 => (ushort)((data[row + s * 2] << 8) | data[row + s * 2 + 1]),
                    8 => data[row + s],
                    _ => (ushort)((data[row + s * bits / 8] >> (8 - bits - (int)(s * bits % 8))) & mask),
                };
            }
        }
        return output;
    }

    // Unpacks samples into one byte per sample. With scale, values are stretched to 0..255 (16-bit samples keep their
    // high byte); without, they are kept (palette indexes).
    private static byte[] Unpack(byte[] data, int width, int height, int components, int bits, bool scale)
    {
        var raw = RawSamples(data, width, height, components, bits);
        int max = (1 << Math.Min(bits, 8)) - 1;
        var output = new byte[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            output[i] = bits == 16 ? (byte)(raw[i] >> 8) : (byte)(scale && bits < 8 ? raw[i] * 255 / max : raw[i]);
        }
        return output;
    }

    #endregion

    #endregion
}
