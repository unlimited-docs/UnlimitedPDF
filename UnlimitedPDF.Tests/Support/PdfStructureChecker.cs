using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Re-reads a PDF written by the library and reports structural defects: a wrong header, xref offsets that do
/// not point at their object, stream <c>/Length</c> values that differ from the data, references to missing
/// objects, missing image transparency masks, images stored more than once (PR-010), and damaged embedded fonts:
/// a Type0 font without Identity-H, a CIDFont, descriptor, <c>ToUnicode</c> map, or font program, an unsafe font
/// name, an invalid TrueType or CFF program, and widths that differ from the program (A05, A08).
/// </summary>
internal static class PdfStructureChecker
{
    #region Fields and Constants

    private static readonly Regex LengthPattern = new(@"/Length\s+(\d+)", RegexOptions.CultureInvariant);
    private static readonly Regex ReferencePattern = new(@"(\d+)\s+(\d+)\s+R\b", RegexOptions.CultureInvariant);
    private static readonly Regex SoftMaskPattern = new(@"/SMask\s+(\d+)\s+\d+\s+R\b", RegexOptions.CultureInvariant);
    private static readonly Regex StreamKeywordPattern = new(@"stream(\r\n|\n)", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Fails the current test with every defect found in <paramref name="pdf"/>.
    /// </summary>
    /// <param name="pdf">The complete PDF file bytes.</param>
    public static void AssertValid(byte[] pdf)
    {
        var errors = Check(pdf);
        Assert.True(errors.Count == 0, "PDF structure errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    /// <summary>
    /// Checks the structure of <paramref name="pdf"/>.
    /// </summary>
    /// <param name="pdf">The complete PDF file bytes.</param>
    /// <returns>One message per defect; empty when the file is structurally valid.</returns>
    public static IReadOnlyList<string> Check(byte[] pdf)
    {
        var errors = new List<string>();

        // Latin-1 maps every byte to one char, so string offsets equal byte offsets.
        var text = Encoding.Latin1.GetString(pdf);

        if (!text.StartsWith("%PDF-1.", StringComparison.Ordinal))
            errors.Add("Header does not start with '%PDF-1.'.");

        var xrefOffset = ReadStartXref(text, errors);
        if (xrefOffset < 0)
            return errors;

        var offsets = ReadXref(text, xrefOffset, errors, out var trailer);
        if (offsets is null)
            return errors;

        var objects = new Dictionary<int, PdfObjectInfo>();
        for (int number = 1; number < offsets.Length; number++)
        {
            var info = ReadObject(text, pdf, number, offsets[number], errors);
            if (info is not null)
                objects[number] = info;
        }

        CheckTrailer(trailer, offsets.Length, objects, errors);
        CheckReferences(objects, errors);
        CheckSoftMasks(objects, errors);
        CheckImagesStoredOnce(objects, errors);
        if (errors.Count == 0) PdfFontChecks.Check(pdf, errors);

        return errors;
    }

    /// <summary>
    /// Returns every object's dictionary text and raw stream data, and the catalog's object number, for
    /// <see cref="PdfInspector"/>. Fails the test when the file is structurally damaged.
    /// </summary>
    /// <param name="pdf">The complete PDF file bytes.</param>
    /// <returns>The objects and the root object number.</returns>
    public static (IReadOnlyDictionary<int, (string Dictionary, byte[]? Stream)> Objects, int Root) ReadObjects(byte[] pdf)
    {
        var errors = new List<string>();
        var text = Encoding.Latin1.GetString(pdf);
        var xrefOffset = ReadStartXref(text, errors);
        var offsets = xrefOffset < 0 ? null : ReadXref(text, xrefOffset, errors, out var trailer);
        if (offsets is null) throw new InvalidOperationException("The PDF cannot be read: " + string.Join(" ", errors));

        var objects = new Dictionary<int, (string, byte[]?)>();
        for (int number = 1; number < offsets.Length; number++)
        {
            var info = ReadObject(text, pdf, number, offsets[number], errors);
            if (info is not null) objects[number] = (info.Dictionary, info.StreamData);
        }
        var rootMatch = Regex.Match(text[(int)xrefOffset..], @"/Root\s+(\d+)\s+\d+\s+R", RegexOptions.CultureInvariant);
        return (objects, rootMatch.Success ? int.Parse(rootMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0);
    }

    #endregion

    #region Private Methods

    private static void CheckImagesStoredOnce(Dictionary<int, PdfObjectInfo> objects, List<string> errors)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (number, info) in objects.OrderBy(o => o.Key))
        {
            if (!info.IsImage || info.StreamData is null)
                continue;

            // The dictionary is compared with its references, so two images that share color data but use different
            // masks are distinct images, while a stored copy of the same image (same mask object) is a duplicate.
            // Masks are images too, so identical masks stored twice are also reported.
            var key = info.Dictionary.Trim() + "|" + Convert.ToHexString(SHA256.HashData(info.StreamData));
            if (seen.TryGetValue(key, out var first))
                errors.Add($"Image object {number} duplicates image object {first}; an image must be stored once.");
            else
                seen[key] = number;
        }
    }

    private static void CheckReferences(Dictionary<int, PdfObjectInfo> objects, List<string> errors)
    {
        foreach (var (number, info) in objects.OrderBy(o => o.Key))
        {
            foreach (Match match in ReferencePattern.Matches(info.Dictionary))
            {
                var target = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (objects.ContainsKey(target))
                    continue;

                var owner = info.IsPage ? "Page object" : "Object";
                errors.Add($"{owner} {number} references missing object {target}.");
            }
        }
    }

    private static void CheckSoftMasks(Dictionary<int, PdfObjectInfo> objects, List<string> errors)
    {
        foreach (var (number, info) in objects.OrderBy(o => o.Key))
        {
            if (!info.IsImage)
                continue;

            var match = SoftMaskPattern.Match(info.Dictionary);
            if (!match.Success)
                continue;

            var target = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!objects.TryGetValue(target, out var mask) || !mask.IsImage)
                errors.Add($"Image object {number} names transparency mask {target}, which is missing or not an image.");
        }
    }

    private static void CheckTrailer(string trailer, int size, Dictionary<int, PdfObjectInfo> objects, List<string> errors)
    {
        var sizeMatch = Regex.Match(trailer, @"/Size\s+(\d+)", RegexOptions.CultureInvariant);
        if (!sizeMatch.Success || int.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture) != size)
            errors.Add($"Trailer /Size does not equal the xref entry count {size}.");

        var rootMatch = Regex.Match(trailer, @"/Root\s+(\d+)\s+\d+\s+R", RegexOptions.CultureInvariant);
        if (!rootMatch.Success || !objects.ContainsKey(int.Parse(rootMatch.Groups[1].Value, CultureInfo.InvariantCulture)))
            errors.Add("Trailer /Root is missing or references a missing object.");
    }

    private static bool IsEndStreamAt(string text, int index)
    {
        // The library always writes an end-of-line marker after the data. Requiring it means a /Length that is one
        // byte too long cannot hide inside that marker.
        if (index < text.Length && text[index] == '\r') index++;
        if (index >= text.Length || text[index] != '\n') return false;
        index++;
        return string.CompareOrdinal(text, index, "endstream", 0, "endstream".Length) == 0;
    }

    private static PdfObjectInfo? ReadObject(string text, byte[] pdf, int number, long offset, List<string> errors)
    {
        var expected = $"{number} 0 obj";
        if (offset < 0 || offset >= text.Length
            || string.CompareOrdinal(text, (int)offset, expected, 0, expected.Length) != 0
            || (offset + expected.Length < text.Length && !char.IsWhiteSpace(text[(int)offset + expected.Length])))
        {
            errors.Add($"Xref entry for object {number} points at offset {offset}, which does not start '{expected}'.");
            return null;
        }

        var start = (int)offset + expected.Length;
        var endObj = text.IndexOf("endobj", start, StringComparison.Ordinal);
        var streamMatch = StreamKeywordPattern.Match(text, start);

        if (streamMatch.Success && (endObj < 0 || streamMatch.Index < endObj))
        {
            var dictionary = text[start..streamMatch.Index];
            var dataStart = streamMatch.Index + streamMatch.Length;
            var lengthMatch = LengthPattern.Match(dictionary);
            if (!lengthMatch.Success)
            {
                errors.Add($"Stream object {number} has no /Length.");
                return new PdfObjectInfo(dictionary, null);
            }

            var length = long.Parse(lengthMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var dataEnd = dataStart + length;
            if (dataEnd > text.Length || !IsEndStreamAt(text, (int)dataEnd))
            {
                errors.Add($"Stream object {number} has /Length {length}, which does not match its data.");
                return new PdfObjectInfo(dictionary, null);
            }

            return new PdfObjectInfo(dictionary, pdf[dataStart..(int)dataEnd]);
        }

        if (endObj < 0)
        {
            errors.Add($"Object {number} has no 'endobj'.");
            return null;
        }

        return new PdfObjectInfo(text[start..endObj], null);
    }

    private static long ReadStartXref(string text, List<string> errors)
    {
        var index = text.LastIndexOf("startxref", StringComparison.Ordinal);
        if (index < 0)
        {
            errors.Add("No 'startxref' found.");
            return -1;
        }

        var match = Regex.Match(text[(index + "startxref".Length)..], @"^\s*(\d+)", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            errors.Add("'startxref' is not followed by an offset.");
            return -1;
        }

        var offset = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        if (offset >= text.Length || string.CompareOrdinal(text, (int)offset, "xref", 0, 4) != 0)
        {
            errors.Add($"'startxref' offset {offset} does not point at 'xref'.");
            return -1;
        }

        return offset;
    }

    private static long[]? ReadXref(string text, long xrefOffset, List<string> errors, out string trailer)
    {
        trailer = string.Empty;
        var lines = text[(int)xrefOffset..].Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var header = lines.Length > 1 ? lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        if (header.Length != 2 || header[0] != "0" || !int.TryParse(header[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
        {
            errors.Add("Xref subsection header is not '0 <count>'.");
            return null;
        }

        if (lines.Length < 2 + count + 1)
        {
            errors.Add("Xref table is truncated.");
            return null;
        }

        var offsets = new long[count];
        for (int i = 0; i < count; i++)
        {
            var entry = lines[2 + i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var inUse = i == 0 ? "f" : "n";
            if (entry.Length != 3 || entry[0].Length != 10 || entry[2] != inUse
                || !long.TryParse(entry[0], NumberStyles.None, CultureInfo.InvariantCulture, out offsets[i]))
            {
                errors.Add($"Xref entry {i} is malformed: '{lines[2 + i]}'.");
                return null;
            }
        }

        var trailerIndex = 2 + count;
        if (lines[trailerIndex] != "trailer")
        {
            errors.Add("'trailer' does not follow the xref entries.");
            return null;
        }

        trailer = string.Join(" ", lines.Skip(trailerIndex + 1).TakeWhile(l => l != "startxref"));
        return offsets;
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed record PdfObjectInfo(string Dictionary, byte[]? StreamData)
    {
        public bool IsImage => Regex.IsMatch(Dictionary, @"/Subtype\s*/Image\b", RegexOptions.CultureInvariant);

        public bool IsPage => Regex.IsMatch(Dictionary, @"/Type\s*/Page\b", RegexOptions.CultureInvariant);
    }

    #endregion
}
