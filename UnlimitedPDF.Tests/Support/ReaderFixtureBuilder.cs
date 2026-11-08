namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Writes existing-PDF fixtures for the reader tests (PR-011: generated in test code). Text is written as Latin-1, so
/// a fixture can hold any byte.
/// </summary>
internal sealed class ReaderFixtureBuilder
{
    #region Fields and Constants

    private readonly List<(int Number, int Generation, byte[] Body)> _objects = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds an object whose value is <paramref name="value"/>, for example "&lt;&lt; /Type /Catalog &gt;&gt;".
    /// </summary>
    public ReaderFixtureBuilder AddObject(int number, string value, int generation = 0)
    {
        _objects.Add((number, generation, Latin1(value + "\n")));
        return this;
    }

    /// <summary>
    /// Adds a stream. <paramref name="length"/> is written as the <c>/Length</c> value, for example "5 0 R" or a
    /// deliberately wrong number; by default the true length.
    /// </summary>
    public ReaderFixtureBuilder AddStream(int number, string dictionaryEntries, byte[] data, string? length = null)
    {
        using var body = new MemoryStream();
        body.Write(Latin1($"<< {dictionaryEntries} /Length {length ?? data.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n"));
        body.Write(data);
        body.Write(Latin1("\nendstream\n"));
        _objects.Add((number, 0, body.ToArray()));
        return this;
    }

    /// <summary>
    /// Returns a file with a classic cross-reference table. <paramref name="oneSubsectionPerObject"/> writes every
    /// object in its own subsection.
    /// </summary>
    public byte[] ToBytes(string header = "%PDF-1.7", string trailerEntries = "/Root 1 0 R", bool oneSubsectionPerObject = false)
    {
        using var output = new MemoryStream();
        Write(output, header + "\n");
        var offsets = WriteObjects(output);

        long xref = output.Position;
        int size = offsets.Count == 0 ? 1 : offsets.Keys.Max() + 1;
        Write(output, "xref\n");
        if (oneSubsectionPerObject)
        {
            Write(output, "0 1\n0000000000 65535 f \n");
            foreach (var (number, (offset, generation)) in offsets.OrderBy(o => o.Key))
                Write(output, $"{number} 1\n{offset:D10} {generation:D5} n \n");
        }
        else
        {
            Write(output, $"0 {size}\n0000000000 65535 f \n");
            for (int number = 1; number < size; number++)
            {
                Write(output, offsets.TryGetValue(number, out var entry)
                    ? $"{entry.Offset:D10} {entry.Generation:D5} n \n"
                    : "0000000000 00001 f \n");
            }
        }

        Write(output, $"trailer\n<< {trailerEntries} /Size {size} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    /// <summary>
    /// Appends this builder's objects to <paramref name="previous"/> as an incremental update with its own classic
    /// table and a trailer whose <c>/Prev</c> points at the previous section. <paramref name="freed"/> object numbers
    /// are listed as free (deleted).
    /// </summary>
    public byte[] AppendUpdateTo(byte[] previous, string trailerEntries = "/Root 1 0 R", params int[] freed)
    {
        long prev = PreviousStartXref(previous);
        using var output = new MemoryStream();
        output.Write(previous);
        Write(output, "\n");
        var offsets = WriteObjects(output);

        long xref = output.Position;
        Write(output, "xref\n");
        foreach (var (number, (offset, generation)) in offsets.OrderBy(o => o.Key))
            Write(output, $"{number} 1\n{offset:D10} {generation:D5} n \n");
        foreach (int number in freed)
            Write(output, $"{number} 1\n0000000000 00001 f \n");

        int size = offsets.Keys.Concat(freed).DefaultIfEmpty(0).Max() + 1;
        Write(output, $"trailer\n<< {trailerEntries} /Size {size} /Prev {prev} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    /// <summary>
    /// Returns a file whose only cross-reference section is a compressed cross-reference stream (PDF 1.5).
    /// <paramref name="compressed"/> objects are stored in one object stream. <paramref name="pngPredictor"/> encodes
    /// the rows with the PNG Up predictor.
    /// </summary>
    public byte[] ToBytesWithCrossReferenceStream(
        string header = "%PDF-1.5",
        string trailerEntries = "/Root 1 0 R",
        IReadOnlyCollection<int>? compressed = null,
        bool pngPredictor = false,
        string objectStreamEntries = "")
    {
        compressed ??= Array.Empty<int>();
        using var output = new MemoryStream();
        Write(output, header + "\n");
        var offsets = WriteObjects(output, skip: compressed);

        int max = _objects.Select(o => o.Number).DefaultIfEmpty(0).Max();
        int objectStream = max + 1;
        int xrefNumber = max + 2;
        var streamIndex = WriteObjectStream(output, objectStream, compressed, objectStreamEntries, offsets);

        long xref = output.Position;
        var rows = new List<byte[]>();
        for (int number = 0; number <= xrefNumber; number++)
        {
            if (number == xrefNumber) rows.Add(Row(1, xref, 0));
            else if (streamIndex.TryGetValue(number, out int index)) rows.Add(Row(2, objectStream, index));
            else if (offsets.TryGetValue(number, out var entry)) rows.Add(Row(1, entry.Offset, entry.Generation));
            else rows.Add(Row(0, 0, number == 0 ? 65535 : 0));
        }

        byte[] data = Deflate(pngPredictor ? PngUp(rows) : rows.SelectMany(r => r).ToArray());
        string parameters = pngPredictor ? " /DecodeParms << /Predictor 12 /Columns 7 >>" : "";
        Write(output, $"{xrefNumber} 0 obj\n<< /Type /XRef /Size {xrefNumber + 1} /W [1 4 2] {trailerEntries} /Filter /FlateDecode{parameters} /Length {data.Length} >>\nstream\n");
        output.Write(data);
        Write(output, $"\nendstream\nendobj\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    /// <summary>
    /// Returns a hybrid file: a classic table lists the plain objects and marks <paramref name="compressed"/> objects
    /// free, while a cross-reference stream named by the trailer's <c>/XRefStm</c> lists them in an object stream.
    /// </summary>
    public byte[] ToHybridBytes(IReadOnlyCollection<int> compressed, string trailerEntries = "/Root 1 0 R")
    {
        using var output = new MemoryStream();
        Write(output, "%PDF-1.5\n");
        var offsets = WriteObjects(output, skip: compressed);

        int max = _objects.Select(o => o.Number).DefaultIfEmpty(0).Max();
        int objectStream = max + 1;
        int streamXref = max + 2;
        var streamIndex = WriteObjectStream(output, objectStream, compressed, "", offsets);

        long hidden = output.Position;
        byte[] rows = Deflate(compressed.OrderBy(n => n).SelectMany(n => Row(2, objectStream, streamIndex[n])).ToArray());
        string index = string.Join(" ", compressed.OrderBy(n => n).Select(n => $"{n} 1"));
        Write(output, $"{streamXref} 0 obj\n<< /Type /XRef /Size {streamXref + 1} /W [1 4 2] /Index [{index}] /Filter /FlateDecode /Length {rows.Length} >>\nstream\n");
        output.Write(rows);
        Write(output, "\nendstream\nendobj\n");

        long xref = output.Position;
        Write(output, $"xref\n0 {objectStream + 1}\n0000000000 65535 f \n");
        for (int number = 1; number <= objectStream; number++)
        {
            Write(output, offsets.TryGetValue(number, out var entry)
                ? $"{entry.Offset:D10} {entry.Generation:D5} n \n"
                : "0000000000 00000 f \n");
        }
        Write(output, $"trailer\n<< {trailerEntries} /Size {streamXref + 1} /XRefStm {hidden} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns a builder for a document of <paramref name="pageCount"/> US Letter pages, each showing "Page N" in
    /// Helvetica: catalog 1, page tree 2 (holding the inherited <c>/MediaBox</c>), font 3, then page 2n+2 with
    /// content stream 2n+3. <paramref name="infoTitle"/> adds an information dictionary as object 99.
    /// </summary>
    internal static ReaderFixtureBuilder Document(int pageCount, string? infoTitle = null, string catalogEntries = "")
    {
        var builder = new ReaderFixtureBuilder();
        string kids = string.Join(" ", Enumerable.Range(1, pageCount).Select(n => $"{2 * n + 2} 0 R"));
        builder.AddObject(1, $"<< /Type /Catalog /Pages 2 0 R {catalogEntries}>>")
            .AddObject(2, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        for (int n = 1; n <= pageCount; n++)
        {
            builder.AddObject(2 * n + 2, $"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /Contents {2 * n + 3} 0 R >>");
            builder.AddStream(2 * n + 3, "", Encoding.ASCII.GetBytes($"BT /F1 12 Tf 72 720 Td (Page {n}) Tj ET"));
        }
        if (infoTitle is not null) builder.AddObject(99, $"<< /Title {infoTitle} >>");
        return builder;
    }

    /// <summary>
    /// Returns the bytes of <see cref="Document"/>, with an information dictionary when <paramref name="infoTitle"/>
    /// is given (for example "(Report)").
    /// </summary>
    internal static byte[] DocumentBytes(int pageCount, string? infoTitle = null) =>
        Document(pageCount, infoTitle).ToBytes(trailerEntries: infoTitle is null ? "/Root 1 0 R" : "/Root 1 0 R /Info 99 0 R");

    /// <summary>
    /// Compresses <paramref name="data"/> as zlib (FlateDecode) data.
    /// </summary>
    internal static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);
        return output.ToArray();
    }

    #endregion

    #region Private Methods

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static byte[] PngUp(List<byte[]> rows)
    {
        var output = new List<byte>();
        var previous = new byte[rows[0].Length];
        foreach (var row in rows)
        {
            output.Add(2);
            for (int i = 0; i < row.Length; i++) output.Add((byte)(row[i] - previous[i]));
            previous = row;
        }
        return output.ToArray();
    }

    private static long PreviousStartXref(byte[] file)
    {
        string text = Encoding.Latin1.GetString(file);
        int index = text.LastIndexOf("startxref", StringComparison.Ordinal) + "startxref".Length;
        return long.Parse(text.Substring(index).Trim().Split('\n')[0].Trim(), CultureInfo.InvariantCulture);
    }

    private static byte[] Row(int type, long second, int third) => new[]
    {
        (byte)type,
        (byte)(second >> 24), (byte)(second >> 16), (byte)(second >> 8), (byte)second,
        (byte)(third >> 8), (byte)third,
    };

    // Writes the compressed objects into one object stream and returns each object's index in it.
    private Dictionary<int, int> WriteObjectStream(
        Stream output, int number, IReadOnlyCollection<int> compressed, string extraEntries, Dictionary<int, (long Offset, int Generation)> offsets)
    {
        var index = new Dictionary<int, int>();
        if (compressed.Count == 0) return index;

        var header = new StringBuilder();
        using var bodies = new MemoryStream();
        foreach (var (objectNumber, _, body) in _objects.Where(o => compressed.Contains(o.Number)))
        {
            index[objectNumber] = index.Count;
            header.Append(objectNumber).Append(' ').Append(bodies.Length).Append(' ');
            bodies.Write(body);
        }

        byte[] headerBytes = Latin1(header.ToString());
        byte[] data = Deflate(headerBytes.Concat(bodies.ToArray()).ToArray());
        offsets[number] = (output.Position, 0);
        Write(output, $"{number} 0 obj\n<< /Type /ObjStm /N {index.Count} /First {headerBytes.Length} {extraEntries} /Filter /FlateDecode /Length {data.Length} >>\nstream\n");
        output.Write(data);
        Write(output, "\nendstream\nendobj\n");
        return index;
    }

    private static void Write(Stream stream, string text) => stream.Write(Latin1(text));

    private Dictionary<int, (long Offset, int Generation)> WriteObjects(Stream output, IReadOnlyCollection<int>? skip = null)
    {
        var offsets = new Dictionary<int, (long Offset, int Generation)>();
        foreach (var (number, generation, body) in _objects)
        {
            if (skip?.Contains(number) == true) continue;
            offsets[number] = (output.Position, generation);
            Write(output, $"{number} {generation} obj\n");
            output.Write(body);
            Write(output, "endobj\n");
        }
        return offsets;
    }

    #endregion

    #endregion
}
