namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// Reads and checks a whole existing PDF in the order WR-035 sets: size, emptiness, header, encryption, then the
/// cross-reference (recovered by a scan when broken) and object count.
/// </summary>
internal static class PdfFileReader
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads <paramref name="data"/>, turning every reader failure into a <see cref="PdfFileException"/>.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="limits">The load's limits.</param>
    /// <returns>The file's objects and trailer.</returns>
    /// <exception cref="PdfFileException">The file is encrypted, damaged, empty, or over a limit.</exception>
    public static PdfSourceFile Load(byte[] data, PdfReadLimits limits)
    {
        try
        {
            return Read(data, limits);
        }
        catch (PdfReadException exception)
        {
            throw ToPublic(exception);
        }
    }

    /// <summary>
    /// Reads <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The whole file.</param>
    /// <param name="limits">The load's limits.</param>
    /// <param name="recover">True to rebuild the cross-reference by scanning even when the file's own one reads.</param>
    /// <returns>The file's objects and trailer.</returns>
    /// <exception cref="PdfReadException">The file is encrypted, damaged, empty, or over a limit.</exception>
    public static PdfSourceFile Read(byte[] data, PdfReadLimits limits, bool recover = false)
    {
        if (data.Length > limits.MaxFileSize)
            throw new PdfReadException(PdfReadError.TooLarge, $"The file is over the size limit of {limits.MaxFileSize.ToString("N0", CultureInfo.InvariantCulture)} bytes.");
        if (data.Length == 0) throw new PdfReadException(PdfReadError.Empty, "The data is empty.");

        string version = PdfHeaderReader.ReadVersion(data) ?? throw PdfReadException.Damaged("The data is not a PDF: it has no %PDF- header.");

        CrossReferenceSection? section = null;
        if (!recover)
        {
            try
            {
                section = CrossReferenceReader.ReadChain(data, limits);
                CheckEncryption(section.Trailer);
                CheckRoot(new PdfObjectTable(data, section.Entries, section.Trailer, limits));
            }
            catch (PdfReadException exception) when (exception.Error == PdfReadError.Damaged)
            {
                section = null;
            }
        }
        bool recovered = section is null;
        section ??= CrossReferenceRecovery.Recover(data, limits);

        CheckEncryption(section.Trailer);
        var table = new PdfObjectTable(data, section.Entries, section.Trailer, limits);
        var catalog = CheckRoot(table);
        return new PdfSourceFile(table, catalog, MaxVersion(version, table.Resolve(catalog["Version"]) as PdfReadName), recovered);
    }

    /// <summary>
    /// Turns a reader failure into the public exception. A limit failure names the settings that change it.
    /// </summary>
    /// <param name="exception">The reader failure.</param>
    /// <returns>The public exception, to throw.</returns>
    public static PdfFileException ToPublic(PdfReadException exception)
    {
        var reason = ToReason(exception.Error);
        string message = reason == PdfFileErrorReason.TooLarge
            ? exception.Message + " Change the limit with PdfFileLoadOptions or the PdfFile.DefaultMax properties."
            : exception.Message;
        return new PdfFileException(reason, message, exception);
    }

    #endregion

    #region Private Methods

    // Any trailer with /Encrypt marks the file encrypted (the chain keeps an older trailer's key too).
    private static void CheckEncryption(PdfReadDictionary trailer)
    {
        if (trailer.ContainsKey("Encrypt"))
            throw new PdfReadException(PdfReadError.Encrypted, "The PDF is encrypted. Encrypted files cannot be opened.");
    }

    private static PdfReadDictionary CheckRoot(PdfObjectTable table) =>
        table.Resolve(table.Trailer["Root"]) as PdfReadDictionary
            ?? throw PdfReadException.Damaged("The file has no document catalog.");

    private static string MaxVersion(string header, PdfReadName? catalog)
    {
        if (catalog is null || !Version.TryParse(catalog.Value, out var fromCatalog)) return header;
        return Version.TryParse(header, out var fromHeader) && fromHeader >= fromCatalog ? header : catalog.Value;
    }

    private static PdfFileErrorReason ToReason(PdfReadError error) => error switch
    {
        PdfReadError.Empty => PdfFileErrorReason.Empty,
        PdfReadError.Encrypted => PdfFileErrorReason.Encrypted,
        PdfReadError.TooLarge => PdfFileErrorReason.TooLarge,
        _ => PdfFileErrorReason.Damaged,
    };

    #endregion

    #endregion
}

/// <summary>
/// An existing PDF that has been read and checked.
/// </summary>
/// <param name="Objects">The file's objects.</param>
/// <param name="Catalog">The document catalog.</param>
/// <param name="Version">The PDF version the file declares: the higher of its header and its catalog's
/// <c>/Version</c>.</param>
/// <param name="Recovered">Whether the cross-reference was rebuilt by scanning.</param>
internal sealed record PdfSourceFile(PdfObjectTable Objects, PdfReadDictionary Catalog, string Version, bool Recovered);
