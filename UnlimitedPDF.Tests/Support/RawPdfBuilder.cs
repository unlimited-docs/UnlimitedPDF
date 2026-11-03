namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Writes small hand-made PDF files with a correct xref table, so checker tests can introduce exactly one defect.
/// </summary>
internal sealed class RawPdfBuilder
{
    #region Fields and Constants

    private readonly Dictionary<int, long> _offsetAdjustments = new();
    private readonly List<(int Number, string Dictionary, byte[]? Data, long? Length)> _objects = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a dictionary object.
    /// </summary>
    public RawPdfBuilder AddObject(int number, string dictionary)
    {
        _objects.Add((number, dictionary, null, null));
        return this;
    }

    /// <summary>
    /// Adds a stream object. <paramref name="lengthOverride"/> writes a deliberately wrong <c>/Length</c>.
    /// </summary>
    public RawPdfBuilder AddStream(int number, string dictionaryEntries, byte[] data, long? lengthOverride = null)
    {
        _objects.Add((number, dictionaryEntries, data, lengthOverride));
        return this;
    }

    /// <summary>
    /// Shifts the xref offset written for <paramref name="number"/> by <paramref name="delta"/> bytes.
    /// </summary>
    public RawPdfBuilder ShiftXrefOffset(int number, long delta)
    {
        _offsetAdjustments[number] = delta;
        return this;
    }

    /// <summary>
    /// Returns the PDF bytes.
    /// </summary>
    public byte[] ToBytes(string header = "%PDF-1.7")
    {
        using var output = new MemoryStream();
        var offsets = new long[_objects.Count + 1];

        Write(output, header + "\n");
        foreach (var (number, dictionary, data, length) in _objects.OrderBy(o => o.Number))
        {
            offsets[number] = output.Position;
            Write(output, $"{number} 0 obj\n");
            if (data is null)
            {
                Write(output, dictionary + "\n");
            }
            else
            {
                Write(output, $"<< {dictionary} /Length {length ?? data.Length} >>\nstream\n");
                output.Write(data);
                Write(output, "\nendstream\n");
            }

            Write(output, "endobj\n");
        }

        var xref = output.Position;
        Write(output, $"xref\n0 {offsets.Length}\n0000000000 65535 f \n");
        for (int number = 1; number < offsets.Length; number++)
        {
            var offset = offsets[number] + _offsetAdjustments.GetValueOrDefault(number);
            Write(output, $"{offset:D10} 00000 n \n");
        }

        Write(output, $"trailer\n<< /Root 1 0 R /Size {offsets.Length} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    #endregion

    #region Private Methods

    private static void Write(Stream stream, string text) => stream.Write(Encoding.ASCII.GetBytes(text));

    #endregion

    #endregion
}
