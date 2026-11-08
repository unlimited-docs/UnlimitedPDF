namespace UnlimitedPDF.Models.Internal.Reading;

/// <summary>
/// The kind of a cross-reference entry.
/// </summary>
internal enum CrossReferenceEntryKind
{
    /// <summary>
    /// The object number is free (deleted or unused).
    /// </summary>
    Free,

    /// <summary>
    /// The object is stored at a byte offset.
    /// </summary>
    InUse,

    /// <summary>
    /// The object is stored inside an object stream.
    /// </summary>
    Compressed,
}

/// <summary>
/// Where one object is stored.
/// </summary>
/// <param name="Kind">Free, at an offset, or inside an object stream.</param>
/// <param name="Offset">The byte offset of an in-use object.</param>
/// <param name="Generation">The generation number of an in-use object.</param>
/// <param name="StreamNumber">The object stream holding a compressed object.</param>
/// <param name="StreamIndex">The compressed object's index in its object stream.</param>
internal readonly record struct CrossReferenceEntry(
    CrossReferenceEntryKind Kind,
    long Offset = 0,
    int Generation = 0,
    int StreamNumber = 0,
    int StreamIndex = 0);

/// <summary>
/// One cross-reference section and its trailer, as found at one offset.
/// </summary>
/// <param name="Entries">The entries the section lists, by object number.</param>
/// <param name="Trailer">The trailer dictionary (for a cross-reference stream, the stream dictionary).</param>
internal sealed record CrossReferenceSection(Dictionary<int, CrossReferenceEntry> Entries, PdfReadDictionary Trailer);
