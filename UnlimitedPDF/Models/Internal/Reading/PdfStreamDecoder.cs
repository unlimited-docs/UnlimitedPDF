namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Decodes the streams the reader itself must understand: cross-reference streams and object streams. Only
/// <c>FlateDecode</c> (with PNG or TIFF predictors) is supported; page content is never decoded (WR-035).
/// </summary>
internal static class PdfStreamDecoder
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the decoded data of <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The stream to decode.</param>
    /// <param name="resolve">Resolves references in the stream dictionary.</param>
    /// <param name="maxOutput">The most bytes the result may have.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="PdfReadException">A filter is not supported, the data is damaged, or it is too large.</exception>
    public static byte[] Decode(PdfReadStream stream, Func<PdfReadValue?, PdfReadValue> resolve, long maxOutput)
    {
        var filters = AsList(resolve(stream.Dictionary["Filter"]), resolve);
        var parameters = AsList(resolve(stream.Dictionary["DecodeParms"]), resolve);
        byte[] data = stream.Data.ToArray();
        if (filters.Count == 0)
        {
            if (data.Length > maxOutput) throw new PdfReadException(PdfReadError.TooLarge, "A stream is over the stream size limit.");
            return data;
        }

        for (int i = 0; i < filters.Count; i++)
        {
            if (filters[i] is not PdfReadName { Value: "FlateDecode" or "Fl" })
                throw PdfReadException.Damaged($"Filter /{(filters[i] as PdfReadName)?.Value} is not supported for this stream.");
            data = FlateReader.Decode(data, maxOutput);
            if (i < parameters.Count && parameters[i] is PdfReadDictionary decodeParameters)
                data = Unpredict(data, decodeParameters, resolve);
        }
        return data;
    }

    #endregion

    #region Private Methods

    private static List<PdfReadValue> AsList(PdfReadValue value, Func<PdfReadValue?, PdfReadValue> resolve) => value switch
    {
        PdfReadArray array => array.Items.Select(item => resolve(item)).ToList(),
        PdfReadNull => new List<PdfReadValue>(),
        _ => new List<PdfReadValue> { value },
    };

    private static int IntegerOr(PdfReadDictionary dictionary, string key, int fallback, Func<PdfReadValue?, PdfReadValue> resolve) =>
        resolve(dictionary[key]) is PdfReadNumber number ? (int)number.IntegerValue : fallback;

    private static int Paeth(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int distanceLeft = Math.Abs(estimate - left);
        int distanceUp = Math.Abs(estimate - up);
        int distanceUpLeft = Math.Abs(estimate - upLeft);
        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft) return left;
        return distanceUp <= distanceUpLeft ? up : upLeft;
    }

    // Reverses a PNG (10-15) or TIFF (2) predictor. Each PNG row starts with its own filter type byte.
    private static byte[] Unpredict(byte[] data, PdfReadDictionary parameters, Func<PdfReadValue?, PdfReadValue> resolve)
    {
        int predictor = IntegerOr(parameters, "Predictor", 1, resolve);
        if (predictor <= 1) return data;

        int colors = IntegerOr(parameters, "Colors", 1, resolve);
        int bits = IntegerOr(parameters, "BitsPerComponent", 8, resolve);
        int columns = IntegerOr(parameters, "Columns", 1, resolve);
        if (colors is < 1 or > 32 || bits is not (1 or 2 or 4 or 8 or 16) || columns < 1)
            throw PdfReadException.Damaged("A predictor has parameters out of range.");
        int bytesPerPixel = Math.Max(1, (colors * bits + 7) / 8);

        // A row longer than the whole stream can't be valid; checking it first keeps a hostile /Columns from
        // overflowing or allocating far beyond the data (A06).
        long longRowLength = ((long)colors * bits * columns + 7) / 8;
        if (longRowLength > Math.Max(1, data.Length)) throw PdfReadException.Damaged("A predictor row is longer than its stream.");
        int rowLength = (int)longRowLength;

        if (predictor == 2)
        {
            if (bits != 8) throw PdfReadException.Damaged("A TIFF predictor with other than 8 bits per component is not supported.");
            var output = (byte[])data.Clone();
            for (int row = 0; row + rowLength <= output.Length; row += rowLength)
                for (int i = bytesPerPixel; i < rowLength; i++)
                    output[row + i] = (byte)(output[row + i] + output[row + i - bytesPerPixel]);
            return output;
        }

        if (predictor < 10) throw PdfReadException.Damaged($"Predictor {predictor} is not supported.");

        var result = new List<byte>(data.Length);
        var previous = new byte[rowLength];
        var current = new byte[rowLength];
        for (int offset = 0; offset < data.Length; offset += rowLength + 1)
        {
            int type = data[offset];
            int available = Math.Min(rowLength, data.Length - offset - 1);
            Array.Clear(current, 0, rowLength);
            Array.Copy(data, offset + 1, current, 0, Math.Max(0, available));
            for (int i = 0; i < rowLength; i++)
            {
                int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                int up = previous[i];
                int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                current[i] = type switch
                {
                    0 => current[i],
                    1 => (byte)(current[i] + left),
                    2 => (byte)(current[i] + up),
                    3 => (byte)(current[i] + ((left + up) >> 1)),
                    4 => (byte)(current[i] + Paeth(left, up, upLeft)),
                    _ => throw PdfReadException.Damaged($"PNG predictor row type {type} is not valid."),
                };
            }
            result.AddRange(current.Take(Math.Max(0, available)));
            (previous, current) = (current, previous);
        }
        return result.ToArray();
    }

    #endregion

    #endregion
}
