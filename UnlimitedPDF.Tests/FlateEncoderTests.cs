using System.IO.Compression;
using System.Text;
using UnlimitedPDF.Models.Internal;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The stream compressor (WR-042): its pooled output gives the same bytes as compressing into a growable memory
/// stream, for small, repetitive, and incompressible data, including data larger than its first buffer, and the bytes
/// unpack to the input. Empty data gives the 8-byte empty zlib stream on every runtime, so a blank page's compressed
/// content stays a valid stream (26112701 T-11, AC-09).
/// </summary>
public class FlateEncoderTests
{
    #region Fields and Constants

    private static readonly byte[] EmptyZlib = { 0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01 };

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void A_blank_page_saved_compressed_has_a_valid_empty_content_stream()
    {
        var document = new PdfDocument();
        document.AddPage();
        using var output = new MemoryStream();
        document.Save(output);

        string text = Encoding.Latin1.GetString(output.ToArray());
        Assert.Contains($"<< /Length {EmptyZlib.Length} /Filter /FlateDecode >>", text);
        Assert.DoesNotContain("/Length 0 ", text);
    }

    [Fact]
    public void Compress_gives_the_empty_zlib_stream_for_empty_data()
    {
        var compressed = FlateEncoder.Compress(ReadOnlySpan<byte>.Empty);

        Assert.Equal(EmptyZlib, compressed);
        Assert.Empty(Unpack(compressed));
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Compress_gives_the_memory_stream_bytes_and_unpacks_to_the_input(string name, byte[] data)
    {
        var compressed = FlateEncoder.Compress(data);

        Assert.True(compressed.Length > 0, name);
        Assert.Equal(Reference(data), compressed);
        Assert.Equal(data, Unpack(compressed));
    }

    public static TheoryData<string, byte[]> Inputs()
    {
        var random = new byte[300_000];
        new Random(26112701).NextBytes(random);
        var repetitive = new byte[200_000];
        for (int i = 0; i < repetitive.Length; i++) repetitive[i] = (byte)(i % 7 == 0 ? i / 7 : 0);
        return new TheoryData<string, byte[]>
        {
            { "one byte", new byte[] { 42 } },
            { "text", Encoding.ASCII.GetBytes("BT /E1 12 Tf 72 720 Td <0001000200030004> Tj ET\n") },
            { "repetitive", repetitive },
            { "incompressible", random }
        };
    }

    #endregion

    #region Private Methods

    private static byte[] Reference(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    private static byte[] Unpack(byte[] compressed)
    {
        using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    #endregion

    #endregion
}
