using UnlimitedPDF.Models.Internal;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Objects copied from an existing file are written byte for byte, with correct cross-reference offsets, by both
/// writers (WR-002, WR-017).
/// </summary>
public class RawObjectWriteTests
{
    #region Fields and Constants

    private static readonly byte[] HighBytes = Enumerable.Range(0x80, 0x80).Select(b => (byte)b).ToArray();

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public async Task Raw_objects_round_trip_through_the_async_writer()
    {
        var (document, plain, stream) = DocumentWithRawObjects();
        using var output = new MemoryStream();

        await document.SaveAsync(output);

        AssertRoundTrip(output.ToArray(), plain, stream);
    }

    [Fact]
    public void Raw_objects_round_trip_through_the_writer()
    {
        var (document, plain, stream) = DocumentWithRawObjects();
        using var output = new MemoryStream();

        document.Save(output);

        AssertRoundTrip(output.ToArray(), plain, stream);
    }

    [Fact]
    public void Header_version_is_written_as_set()
    {
        var header = new PdfHeader();
        Assert.Equal("%PDF-1.7", header.ToString());

        header.Version = "2.0";

        Assert.Equal("%PDF-2.0", header.ToString());
    }

    [Fact]
    public void Raw_objects_are_numbered_after_every_existing_object()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("x", 10, 10);

        var first = document.AddRawObject(Encoding.ASCII.GetBytes("(a)"));
        var second = document.AddRawObject(Encoding.ASCII.GetBytes("(b)"));

        Assert.Equal(first.ObjectNumber + 1, second.ObjectNumber);
        Assert.True(first.ObjectNumber > 3);
    }

    #endregion

    #region Private Methods

    private static void AssertRoundTrip(byte[] pdf, int plain, int stream)
    {
        PdfStructureChecker.AssertValid(pdf);

        var chain = CrossReferenceReader.ReadChain(pdf, PdfReadLimits.Default);
        var table = new PdfObjectTable(pdf, chain.Entries, chain.Trailer);
        Assert.Equal(HighBytes, Assert.IsType<PdfReadString>(table.Get(plain)).Bytes);

        var read = Assert.IsType<PdfReadStream>(table.Get(stream));
        Assert.Equal(HighBytes, read.Data.ToArray());
        Assert.Equal(HighBytes, Assert.IsType<PdfReadString>(read.Dictionary["Note"]).Bytes);
    }

    private static (PdfDocument Document, int Plain, int Stream) DocumentWithRawObjects()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Raw objects", 72, 72);

        // A literal string holding every byte from 0x80 to 0xFF, escaped where PDF syntax needs it.
        var literal = new List<byte> { (byte)'(' };
        literal.AddRange(HighBytes);
        literal.Add((byte)')');

        var dictionary = Encoding.ASCII.GetBytes($"<< /Note ").Concat(literal).Concat(Encoding.ASCII.GetBytes($" /Length {HighBytes.Length} >>")).ToArray();
        var plain = document.AddRawObject(literal.ToArray());
        var stream = document.AddRawObject(dictionary, HighBytes);
        return (document, plain.ObjectNumber, stream.ObjectNumber);
    }

    #endregion

    #endregion
}
