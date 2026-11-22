using System.IO.Compression;
using UnlimitedPDF.Demos.BlazorWasm.Services;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// "Download all" packs every split part with its exact bytes, and a typed or given file name becomes a safe download
/// name (26112201 AC-04; OWASP A05, A08).
/// </summary>
public class ToolFilesTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_zip_holds_every_part_with_its_bytes_in_order()
    {
        var files = new[] { ("a-1.pdf", new byte[] { 1, 2, 3 }), ("a-2.pdf", new byte[] { 4 }), ("a-3.pdf", Array.Empty<byte>()) };

        using var zip = new ZipArchive(new MemoryStream(ZipBuilder.Zip(files)));

        Assert.Equal(["a-1.pdf", "a-2.pdf", "a-3.pdf"], zip.Entries.Select(e => e.FullName));
        for (int i = 0; i < files.Length; i++)
        {
            using var entry = zip.Entries[i].Open();
            using var copy = new MemoryStream();
            entry.CopyTo(copy);
            Assert.Equal(files[i].Item2, copy.ToArray());
        }
    }

    [Theory]
    [InlineData("merged", "merged.pdf")]
    [InlineData("merged.PDF", "merged.pdf")]
    [InlineData("  Q1 report  ", "Q1 report.pdf")]
    [InlineData("../../etc/passwd", "passwd.pdf")]
    [InlineData("C:\\Windows\\evil", "evil.pdf")]
    [InlineData("<script>alert(1)</script>", "script-.pdf")]
    [InlineData("<b>bold", "-b-bold.pdf")]
    [InlineData("a\"b|c?d*e:f", "a-b-c-d-e-f.pdf")]
    [InlineData("line\nbreak\u0000", "line-break-.pdf")]
    [InlineData("CON", "_CON.pdf")]
    [InlineData("nul.txt", "_nul.txt.pdf")]
    [InlineData("...", "fallback.pdf")]
    [InlineData("", "fallback.pdf")]
    [InlineData(null, "fallback.pdf")]
    [InlineData("///", "fallback.pdf")]
    public void A_typed_name_becomes_a_safe_pdf_name(string? typed, string expected)
    {
        Assert.Equal(expected, SafeFileName.For(typed, "fallback"));
    }

    [Fact]
    public void A_long_name_is_cut_to_100_characters_before_the_extension()
    {
        string name = SafeFileName.For(new string('x', 300), "fallback");

        Assert.Equal(104, name.Length);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("handbook.pdf", "handbook")]
    [InlineData("Q1 report.PDF", "Q1 report")]
    [InlineData("photo.jpg", "photo")]
    [InlineData("../x/<b>.pdf", "-b-")]
    [InlineData(".pdf", "file")]
    public void The_stem_of_a_given_name_is_safe(string name, string expected)
    {
        Assert.Equal(expected, SafeFileName.StemOf(name, "file"));
    }

    #endregion

    #endregion
}
