using System.Globalization;
using System.Text;
using UnlimitedPDF.Models.Internal.Text;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Page content bytes (26112701 T-08): encoded straight from the content builder, they equal what
/// <c>Encoding.ASCII.GetBytes(sb.ToString().Trim())</c> gives, for white space at either end, characters outside ASCII,
/// and a surrogate pair split across two of the builder's chunks. Glyph IDs are written as the digits
/// <c>ToString("X4")</c> gives.
/// </summary>
public class PageContentBytesTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(0, "0000")]
    [InlineData(10, "000A")]
    [InlineData(0x0FFF, "0FFF")]
    [InlineData(0xFFFF, "FFFF")]
    [InlineData(0x10000, "10000")]
    [InlineData(int.MaxValue, "7FFFFFFF")]
    [InlineData(-1, "FFFFFFFF")]
    public void A_glyph_id_is_written_as_its_X4_digits(int glyph, string expected)
    {
        var sb = new StringBuilder("<");
        TextPipeline.AppendGlyphId(sb, glyph).Append('>');

        Assert.Equal($"<{expected}>", sb.ToString());
        Assert.Equal(glyph.ToString("X4", CultureInfo.InvariantCulture), expected);
    }

    [Theory]
    [MemberData(nameof(Contents))]
    public void Content_bytes_equal_the_trimmed_ascii_string_bytes(string name, StringBuilder sb)
    {
        var expected = Encoding.ASCII.GetBytes(sb.ToString().Trim());

        Assert.True(expected.Length <= sb.Length, name);
        Assert.Equal(expected, PdfPage.TrimmedAscii(sb));
    }

    public static TheoryData<string, StringBuilder> Contents()
    {
        var data = new TheoryData<string, StringBuilder>
        {
            { "empty", new StringBuilder() },
            { "white space only", new StringBuilder(" \r\n\t \r\n") },
            { "one line", new StringBuilder().AppendLine("BT /F1 12 Tf 72 720 Td (Hello) Tj ET") },
            { "white space at both ends", new StringBuilder("\r\n  q 1 0 0 1 0 0 cm Q  \r\n") },
            { "outside ASCII", new StringBuilder("BT (café नमस्ते) Tj ET\r\n") },
            { "surrogate pair", new StringBuilder("A \U0001F600 B\r\n") },
            { "lone surrogates", new StringBuilder("x\uD800y\uDC00z") },
        };

        // Appending pieces past the builder's capacity makes several chunks; the pair straddles a chunk boundary, and
        // the trimmed range starts and ends inside chunks.
        foreach (int capacity in new[] { 16, 17, 31, 64 })
        {
            var split = new StringBuilder(capacity);
            split.Append(' ', 3).Append('a', capacity - 4).Append('\uD83D');
            split.Append('\uDE00').Append("BT <0001> Tj ET").Append('\n', 2);
            for (int i = 0; i < 50; i++) split.Append("0 0 0 rg 1 0 0 1 72 ").Append(i).AppendLine(" Tm");
            split.Append(" \r\n");
            data.Add($"split pair, capacity {capacity}", split);
        }
        return data;
    }

    #endregion

    #endregion
}
