using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

internal sealed partial class PageInterpreter
{
    #region Fields and Constants

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

        if (filters.Length == 1 && filters[0] is "DCTDecode" or "DCT")
        {
            // Browsers decode JPEG, including the CMYK JPEGs whose inversion the library records in /Decode.
            if (dictionary.ContainsKey("SMask") || dictionary.ContainsKey("Mask"))
                throw new PageRenderException("The page has a masked JPEG image, which this preview doesn't draw.");
            return "data:image/jpeg;base64," + Convert.ToBase64String(image.Data.ToArray());
        }

        if (dictionary.ContainsKey("Decode")) throw new PageRenderException("The page has an image with a decode array, which this preview doesn't draw.");

        byte[] samples = DecodeImageData(image);
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
        return "data:image/png;base64," + Convert.ToBase64String(PngEncoder.Encode(width, height, outChannels, pixels));
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
                PdfReadStream stream => DecodeImageData(stream),
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
            _imagePixels += (long)width * height;
            if (_imagePixels > _limits.MaxImagePixels) throw new PageRenderException("The page has too many image pixels.");
            return Unpack(DecodeImageData(mask), width, height, 1, IntegerEntry(maskDictionary, "BitsPerComponent"), scale: true);
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

    // Decodes image samples. They count toward the page's image pixels, not its content bytes; the bound here only
    // keeps a damaged stream from inflating far past any image the pixel bound allows.
    private byte[] DecodeImageData(PdfReadStream stream)
    {
        try
        {
            return PdfStreamDecoder.Decode(stream, _objects.Resolve, _limits.MaxImagePixels * 8);
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
