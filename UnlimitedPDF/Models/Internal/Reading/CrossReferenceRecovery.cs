namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Rebuilds the cross-reference of a file whose table is broken, by scanning for <c>n g obj</c> headers (WR-035).
/// Later copies of an object win, as in an incremental update. Objects in object streams found by the scan are added
/// unless a plain copy exists.
/// </summary>
internal static class CrossReferenceRecovery
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Scans <paramref name="data"/> and returns the entries found and a trailer: the last <c>trailer</c> dictionary,
    /// else the dictionary of the last cross-reference stream, else one naming the catalog found by the scan.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="limits">The load's limits.</param>
    /// <returns>The rebuilt entries and trailer.</returns>
    /// <exception cref="PdfReadException">No object or no catalog is found, or a limit is passed.</exception>
    public static CrossReferenceSection Recover(byte[] data, PdfReadLimits limits)
    {
        var entries = new Dictionary<int, CrossReferenceEntry>();
        for (int i = 0; i < data.Length; i++)
        {
            if (!IsLineStart(data, i) || data[i] < '0' || data[i] > '9') continue;
            if (TryReadHeader(data, i, out int number, out int generation, out int end))
            {
                entries[number] = new CrossReferenceEntry(CrossReferenceEntryKind.InUse, i, generation);
                if (entries.Count > limits.MaxObjectCount)
                    throw new PdfReadException(PdfReadError.TooLarge, $"The file lists more than {limits.MaxObjectCount.ToString("N0", CultureInfo.InvariantCulture)} objects.");
                i = end - 1;
            }
        }
        if (entries.Count == 0) throw PdfReadException.Damaged("No objects were found in the file.");

        var table = new PdfObjectTable(data, entries, new PdfReadDictionary(), limits);
        AddObjectStreamEntries(table, entries, limits);
        var trailer = FindTrailer(data, limits) ?? CatalogTrailer(new PdfObjectTable(data, entries, new PdfReadDictionary(), limits), entries.Keys);
        return new CrossReferenceSection(entries, trailer);
    }

    #endregion

    #region Private Methods

    private static void AddObjectStreamEntries(PdfObjectTable table, Dictionary<int, CrossReferenceEntry> entries, PdfReadLimits limits)
    {
        foreach (int number in entries.Keys.ToList())
        {
            PdfReadValue value;
            try
            {
                value = table.Get(number);
            }
            catch (PdfReadException exception) when (exception.Error == PdfReadError.Damaged)
            {
                continue;
            }
            if (value is not PdfReadStream { Dictionary: var dictionary } stream || dictionary["Type"] is not PdfReadName { Value: "ObjStm" }) continue;

            var count = table.Resolve(dictionary["N"]) as PdfReadNumber;
            ObjectStreamReader reader;
            try
            {
                reader = ObjectStreamReader.Open(stream, table.Resolve, limits);
            }
            catch (PdfReadException exception) when (exception.Error == PdfReadError.Damaged)
            {
                continue;
            }
            for (int index = 0; index < (count?.IntegerValue ?? 0); index++)
            {
                int compressed = reader.NumberAt(index);
                if (!entries.ContainsKey(compressed))
                    entries[compressed] = new CrossReferenceEntry(CrossReferenceEntryKind.Compressed, StreamNumber: number, StreamIndex: index);
            }
        }
    }

    private static PdfReadDictionary CatalogTrailer(PdfObjectTable table, IEnumerable<int> numbers)
    {
        foreach (int number in numbers.OrderByDescending(n => n))
        {
            PdfReadValue value;
            try
            {
                value = table.Get(number);
            }
            catch (PdfReadException exception) when (exception.Error == PdfReadError.Damaged)
            {
                continue;
            }
            if (value is PdfReadDictionary dictionary && dictionary["Type"] is PdfReadName { Value: "Catalog" })
            {
                var trailer = new PdfReadDictionary();
                trailer.Set("Root", new PdfReadReference(number, 0));
                return trailer;
            }
        }
        throw PdfReadException.Damaged("The file has no document catalog.");
    }

    private static PdfReadDictionary? FindTrailer(byte[] data, PdfReadLimits limits)
    {
        int index = PdfByteSearch.LastIndexOf(data, "trailer");
        while (index >= 0)
        {
            try
            {
                var lexer = new PdfLexer(data, index + "trailer".Length);
                if (new PdfParser(lexer).ParseValue() is PdfReadDictionary trailer && trailer.ContainsKey("Root")) return trailer;
            }
            catch (PdfReadException)
            {
            }
            index = index == 0 ? -1 : data.AsSpan(0, index).LastIndexOf(Encoding.ASCII.GetBytes("trailer"));
        }

        int xref = PdfByteSearch.LastIndexOf(data, "/XRef");
        if (xref < 0) return null;
        int start = data.AsSpan(0, xref).LastIndexOf(Encoding.ASCII.GetBytes("<<"));
        if (start < 0) return null;
        try
        {
            return new PdfParser(new PdfLexer(data, start)).ParseValue() is PdfReadDictionary dictionary && dictionary.ContainsKey("Root")
                ? dictionary
                : null;
        }
        catch (PdfReadException)
        {
            return null;
        }
    }

    private static bool IsLineStart(byte[] data, int index) => index == 0 || data[index - 1] == '\n' || data[index - 1] == '\r';

    // Reads "number generation obj" starting at offset, with nothing but white space between the parts.
    private static bool TryReadHeader(byte[] data, int offset, out int number, out int generation, out int end)
    {
        number = generation = 0;
        end = offset;
        var lexer = new PdfLexer(data, offset);
        try
        {
            var first = lexer.Next();
            var second = lexer.Next();
            var keyword = lexer.Next();
            end = lexer.Position;
            return first.Kind == PdfTokenKind.Integer && second.Kind == PdfTokenKind.Integer && keyword.IsKeyword("obj")
                && int.TryParse(first.Text, NumberStyles.None, CultureInfo.InvariantCulture, out number)
                && int.TryParse(second.Text, NumberStyles.None, CultureInfo.InvariantCulture, out generation);
        }
        catch (PdfReadException)
        {
            return false;
        }
    }

    #endregion

    #endregion
}
