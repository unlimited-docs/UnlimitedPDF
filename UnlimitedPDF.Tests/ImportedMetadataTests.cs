using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// A document with a metadata source copies that file's document information and XMP stream: strings are written
/// again escaped, indirect values resolved, structured values left out; a document without one is unchanged
/// (WR-040, AC-10, AC-12, A05).
/// </summary>
public class ImportedMetadataTests
{
    #region Fields and Constants

    private const string Xmp = "<?xpacket begin=\"\"?><x:xmpmeta xmlns:x=\"urn:unlimitedpdf:test\"><dc:title>Annual Report</dc:title></x:xmpmeta><?xpacket end=\"w\"?>";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Document_information_is_copied_with_strings_escaped()
    {
        var output = Save(Source(), withSource: true);
        var info = Info(output);

        Assert.Equal(@"Annual (2026) Report \ final", Text(output, info, "Title"));
        Assert.Equal("Jane", Text(output, info, "Author"));
        Assert.Equal("Abé", PdfTextString.Decode(Assert.IsType<PdfReadString>(info["Subject"]).Bytes));
        Assert.Equal("a, b", Text(output, info, "Keywords"));
        Assert.Equal("True", Assert.IsType<PdfReadName>(info["Trapped"]).Value);
        Assert.False(info.ContainsKey("Custom"));
        Assert.All(info.Keys, key => Assert.IsNotType<PdfReadReference>(info[key]));
    }

    [Fact]
    public void Xmp_metadata_is_copied_unchanged()
    {
        var output = Save(Source(), withSource: true);

        var metadata = Assert.IsType<PdfReadStream>(output.Source.Objects.Resolve(output.Source.Catalog["Metadata"]));

        Assert.Equal(Xmp, Encoding.ASCII.GetString(metadata.Data.ToArray()));
        Assert.Equal("Metadata", Assert.IsType<PdfReadName>(metadata.Dictionary["Type"]).Value);
    }

    [Fact]
    public void Compressed_xmp_keeps_its_filter_array_and_parameters()
    {
        // Review R-03: a filter array and its decode parameters are direct values the copy keeps with the bytes.
        byte[] packed = ReaderFixtureBuilder.Deflate(Encoding.ASCII.GetBytes(Xmp));
        var source = ReaderFixtureBuilder.Document(1, catalogEntries: "/Metadata 42 0 R ")
            .AddStream(42, "/Type /Metadata /Subtype /XML /Filter [/FlateDecode] /DecodeParms [<< /Predictor 1 >>]", packed)
            .ToBytes();

        var output = Save(source, withSource: true);
        var metadata = Assert.IsType<PdfReadStream>(output.Source.Objects.Resolve(output.Source.Catalog["Metadata"]));

        Assert.Equal("FlateDecode", Assert.IsType<PdfReadName>(Assert.IsType<PdfReadArray>(metadata.Dictionary["Filter"])[0]).Value);
        Assert.IsType<PdfReadArray>(metadata.Dictionary["DecodeParms"]);
        Assert.Equal(packed, metadata.Data.ToArray());
    }

    [Fact]
    public void Xmp_whose_filter_refers_to_another_object_is_not_copied()
    {
        var source = ReaderFixtureBuilder.Document(1, catalogEntries: "/Metadata 42 0 R ")
            .AddStream(42, "/Type /Metadata /Subtype /XML /Filter 43 0 R", ReaderFixtureBuilder.Deflate(Encoding.ASCII.GetBytes(Xmp)))
            .AddObject(43, "[/FlateDecode 44 0 R]")
            .AddObject(44, "/Unknown")
            .ToBytes();

        var output = Save(source, withSource: true);

        Assert.Null(output.Source.Catalog["Metadata"]);
    }

    [Fact]
    public void A_source_without_metadata_adds_none()
    {
        var output = Save(ReaderFixtureBuilder.DocumentBytes(2), withSource: true);

        Assert.Null(output.Source.Objects.Trailer["Info"]);
        Assert.Null(output.Source.Catalog["Metadata"]);
    }

    [Fact]
    public void Without_a_metadata_source_nothing_is_copied()
    {
        var output = Save(Source(), withSource: false);

        Assert.Null(output.Source.Objects.Trailer["Info"]);
        Assert.Null(output.Source.Catalog["Metadata"]);
    }

    [Fact]
    public async Task Sync_and_async_saves_write_the_same_bytes_and_saving_twice_is_stable()
    {
        var file = PdfFile.FromBytes(Source());
        var document = new PdfDocument { MetadataSource = file };
        document.AddPagesCore(file, new[] { 0 }, null, trimResources: true);

        var first = ImportedPagesTests.Save(document);
        using var stream = new MemoryStream();
        await document.SaveAsync(stream);

        Assert.Equal(first, stream.ToArray());
        Assert.Equal(first, ImportedPagesTests.Save(document));
    }

    #endregion

    #region Private Methods

    private static PdfReadDictionary Info(PdfFile output) =>
        Assert.IsType<PdfReadDictionary>(output.Source.Objects.Resolve(output.Source.Objects.Trailer["Info"]));

    private static PdfFile Save(byte[] source, bool withSource)
    {
        var file = PdfFile.FromBytes(source);
        var document = new PdfDocument { MetadataSource = withSource ? file : null };
        document.AddPagesCore(file, new[] { 0 }, null, trimResources: true);
        var pdf = ImportedPagesTests.Save(document);
        PdfStructureChecker.AssertValid(pdf);
        return ImportedPagesTests.Read(pdf);
    }

    // Two pages, an /Info dictionary with escaped, indirect, UTF-16, name, and structured values, and an XMP stream.
    private static byte[] Source() => ReaderFixtureBuilder.Document(2, catalogEntries: "/Metadata 42 0 R ")
        .AddObject(40, @"<< /Title (Annual \(2026\) Report \\ final) /Author 41 0 R /Subject <FEFF0041006200E9> /Keywords (a, b) /Trapped /True /Custom [1 2] >>")
        .AddObject(41, "(Jane)")
        .AddStream(42, "/Type /Metadata /Subtype /XML", Encoding.ASCII.GetBytes(Xmp))
        .ToBytes(trailerEntries: "/Root 1 0 R /Info 40 0 R");

    private static string Text(PdfFile output, PdfReadDictionary info, string key) =>
        Encoding.Latin1.GetString(Assert.IsType<PdfReadString>(output.Source.Objects.Resolve(info[key])).Bytes);

    #endregion

    #endregion
}
