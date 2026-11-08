namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Reads a file's cross-reference sections: where <c>startxref</c> points and the classic table there.
/// </summary>
internal static class CrossReferenceReader
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the offset that the file's last <c>startxref</c> points at.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <returns>The offset of the newest cross-reference section.</returns>
    /// <exception cref="PdfReadException">There is no valid <c>startxref</c>.</exception>
    public static long FindStartXref(byte[] data)
    {
        int index = PdfByteSearch.LastIndexOf(data, "startxref");
        if (index < 0) throw PdfReadException.Damaged("The file has no startxref.");

        var token = new PdfLexer(data, index + "startxref".Length).Next();
        if (token.Kind != PdfTokenKind.Integer || !long.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long offset))
            throw PdfReadException.Damaged("The startxref offset is not a number.");
        return offset;
    }

    /// <summary>
    /// Reads every cross-reference section from the newest (at <c>startxref</c>) back through <c>/Prev</c>, and merges
    /// them so the newest entry for each object wins. A hybrid file's <c>/XRefStm</c> entries fill the table's free or
    /// missing entries. The newest trailer is kept; a key only an older trailer has is taken from it.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="limits">The load's limits, for decompressing cross-reference streams.</param>
    /// <returns>The merged entries and trailer.</returns>
    /// <exception cref="PdfReadException">A section is missing or invalid, or the <c>/Prev</c> chain loops.</exception>
    public static CrossReferenceSection ReadChain(byte[] data, PdfReadLimits limits)
    {
        var entries = new Dictionary<int, CrossReferenceEntry>();
        var trailer = new PdfReadDictionary();
        var visited = new HashSet<long>();
        long? offset = FindStartXref(data);

        while (offset is long current)
        {
            if (!visited.Add(current)) throw PdfReadException.Damaged("The cross-reference sections refer back to each other.");

            var section = ReadSection(data, current, limits);
            if (section.Trailer["XRefStm"] is PdfReadNumber { IsInteger: true } hidden && visited.Add(hidden.IntegerValue))
            {
                var stream = ReadSection(data, hidden.IntegerValue, limits);
                foreach (var (number, entry) in stream.Entries)
                {
                    if (!section.Entries.TryGetValue(number, out var listed) || listed.Kind == CrossReferenceEntryKind.Free)
                        section.Entries[number] = entry;
                }
            }

            foreach (var (number, entry) in section.Entries)
                entries.TryAdd(number, entry);
            if (entries.Count > limits.MaxObjectCount)
                throw new PdfReadException(PdfReadError.TooLarge, $"The file lists more than {limits.MaxObjectCount.ToString("N0", CultureInfo.InvariantCulture)} objects.");
            foreach (var key in section.Trailer.Keys)
            {
                if (!trailer.ContainsKey(key)) trailer.Set(key, section.Trailer[key]!);
            }

            offset = section.Trailer["Prev"] is PdfReadNumber { IsInteger: true } previous ? previous.IntegerValue : null;
        }

        return new CrossReferenceSection(entries, trailer);
    }

    /// <summary>
    /// Reads the cross-reference section at <paramref name="offset"/>: a classic table or a cross-reference stream.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="offset">The section's offset.</param>
    /// <param name="limits">The load's limits, for decompressing a cross-reference stream.</param>
    /// <returns>The section's entries and trailer.</returns>
    /// <exception cref="PdfReadException">No valid section is at that offset.</exception>
    public static CrossReferenceSection ReadSection(byte[] data, long offset, PdfReadLimits? limits = null)
    {
        if (offset < 0 || offset >= data.Length)
            throw PdfReadException.Damaged($"The cross-reference offset {offset} is outside the file.");

        var lexer = new PdfLexer(data, (int)offset);
        if (lexer.Next().IsKeyword("xref")) return ReadTable(lexer);

        PdfIndirectObject read;
        try
        {
            read = new PdfParser(new PdfLexer(data, (int)offset)).ParseIndirectObject();
        }
        catch (PdfReadException)
        {
            throw PdfReadException.Damaged($"No cross-reference section at offset {offset}.");
        }

        if (read.Value is PdfReadStream { Dictionary: var dictionary } stream && dictionary["Type"] is PdfReadName { Value: "XRef" })
            return ReadStream(stream, limits ?? PdfReadLimits.Default);
        throw PdfReadException.Damaged($"No cross-reference section at offset {offset}.");
    }

    #endregion

    #region Private Methods

    private static long Field(byte[] row, int start, int width)
    {
        long value = 0;
        for (int i = 0; i < width; i++) value = (value << 8) | row[start + i];
        return value;
    }

    private static int ParseInt(PdfToken token) =>
        int.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw PdfReadException.Damaged($"Invalid number '{token.Text}' in the cross-reference table.");

    private static long ParseLong(PdfToken token) =>
        long.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            ? value
            : throw PdfReadException.Damaged($"Invalid offset '{token.Text}' in the cross-reference table.");

    // A cross-reference stream lists rows of three big-endian fields whose widths /W gives, for the object ranges
    // /Index gives (by default 0 to /Size). A missing type field means type 1; unknown types are ignored, which makes
    // those objects read as null.
    private static CrossReferenceSection ReadStream(PdfReadStream stream, PdfReadLimits limits)
    {
        var dictionary = stream.Dictionary;
        if (dictionary["W"] is not PdfReadArray { Count: 3 } widthsArray || widthsArray.Items.Any(w => w is not PdfReadNumber { IsInteger: true }))
            throw PdfReadException.Damaged("A cross-reference stream has no valid /W.");
        int[] widths = widthsArray.Items.Select(w => (int)((PdfReadNumber)w).IntegerValue).ToArray();
        if (widths.Any(w => w < 0 || w > 8) || widths[1] == 0)
            throw PdfReadException.Damaged("A cross-reference stream has invalid field widths.");

        long size = dictionary["Size"] is PdfReadNumber { IsInteger: true } sizeNumber ? sizeNumber.IntegerValue : 0;
        var ranges = new List<(long First, long Count)>();
        if (dictionary["Index"] is PdfReadArray index)
        {
            for (int i = 0; i + 1 < index.Count; i += 2)
            {
                if (index[i] is PdfReadNumber { IsInteger: true } first && index[i + 1] is PdfReadNumber { IsInteger: true } count)
                    ranges.Add((first.IntegerValue, count.IntegerValue));
            }
        }
        else
        {
            ranges.Add((0, size));
        }

        byte[] rows = PdfStreamDecoder.Decode(stream, value => value ?? PdfReadNull.Instance, limits.MaxStreamSize);
        int rowLength = widths.Sum();
        var entries = new Dictionary<int, CrossReferenceEntry>();
        int position = 0;
        foreach (var (first, count) in ranges)
        {
            if (first < 0 || count < 0) throw PdfReadException.Damaged("A cross-reference stream has an invalid /Index.");
            for (long i = 0; i < count && position + rowLength <= rows.Length; i++, position += rowLength)
            {
                long type = widths[0] == 0 ? 1 : Field(rows, position, widths[0]);
                long second = Field(rows, position + widths[0], widths[1]);
                long third = Field(rows, position + widths[0] + widths[1], widths[2]);
                long number = first + i;
                if (number > int.MaxValue) throw PdfReadException.Damaged("A cross-reference stream lists an object number that is too large.");

                CrossReferenceEntry? entry = type switch
                {
                    0 => new CrossReferenceEntry(CrossReferenceEntryKind.Free),
                    1 => new CrossReferenceEntry(CrossReferenceEntryKind.InUse, second, (int)third),
                    2 => new CrossReferenceEntry(CrossReferenceEntryKind.Compressed, StreamNumber: (int)second, StreamIndex: (int)third),
                    _ => null,
                };
                if (entry is not null) entries.TryAdd((int)number, entry.Value);
            }
        }

        return new CrossReferenceSection(entries, dictionary);
    }

    // A table is subsections of "first count" followed by count entries "offset generation n|f", then the trailer.
    // An object listed twice keeps its first entry, as in a single section only one entry can be meant.
    private static CrossReferenceSection ReadTable(PdfLexer lexer)
    {
        var entries = new Dictionary<int, CrossReferenceEntry>();
        while (true)
        {
            var token = lexer.Next();
            if (token.IsKeyword("trailer")) break;

            var count = lexer.Next();
            if (token.Kind != PdfTokenKind.Integer || count.Kind != PdfTokenKind.Integer)
                throw PdfReadException.Damaged($"Invalid cross-reference subsection at offset {token.Start}.");

            int first = ParseInt(token);
            int entryCount = ParseInt(count);
            for (int i = 0; i < entryCount; i++)
            {
                var offset = lexer.Next();
                var generation = lexer.Next();
                var type = lexer.Next();
                if (offset.Kind != PdfTokenKind.Integer || generation.Kind != PdfTokenKind.Integer
                    || !(type.IsKeyword("n") || type.IsKeyword("f")))
                    throw PdfReadException.Damaged($"Invalid cross-reference entry at offset {offset.Start}.");

                int number = first + i;
                if (entries.ContainsKey(number)) continue;
                entries[number] = type.IsKeyword("n")
                    ? new CrossReferenceEntry(CrossReferenceEntryKind.InUse, ParseLong(offset), ParseInt(generation))
                    : new CrossReferenceEntry(CrossReferenceEntryKind.Free);
            }
        }

        if (new PdfParser(lexer).ParseValue() is not PdfReadDictionary trailer)
            throw PdfReadException.Damaged("The trailer is not a dictionary.");
        return new CrossReferenceSection(entries, trailer);
    }

    #endregion

    #endregion
}
