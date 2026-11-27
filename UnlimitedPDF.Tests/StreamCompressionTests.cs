using System.Text.RegularExpressions;
using UnlimitedPDF;
using UnlimitedPDF.Models.Internal.Reading;
using UnlimitedPDF.Tests.Html;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Stream compression (WR-042): by default every stream the library writes itself carries <c>/Filter /FlateDecode</c>
/// and unpacks to exactly the bytes written with <see cref="PdfDocument.CompressStreams"/> off, while copied streams,
/// font programs, and images keep their bytes. Output stays deterministic, every save path follows the switch, a
/// failed save writes nothing, page operations treat a compressed UnlimitedPDF file like a plain one, and a font shared
/// across threads gives the single-thread bytes (26112701 V-02 to V-06, V-08; AC-06 to AC-12).
/// </summary>
public class StreamCompressionTests
{
    #region Fields and Constants

    private const string Flate = "/Filter /FlateDecode";
    private const int SharedFonts = 12;
    private const int SharedImages = 6;

    private static readonly Regex LengthEntry = new(@"/Length \d+", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    // V-02: the default output of each golden document, against its golden file.
    [Theory]
    [MemberData(nameof(BuiltGoldenDocuments))]
    public void Golden_documents_saved_compressed_unpack_to_their_golden_bytes(string name)
    {
        var compressed = GoldenDocuments.Build(name, compressStreams: true);

        PdfStructureChecker.AssertValid(compressed);
        int count = AssertSameUnpacked(File.ReadAllBytes(GoldenFiles.PathFor(name)), compressed, everyLibraryStream: name != GoldenDocuments.Merge);
        Assert.True(count > 0, "No stream was compressed.");
    }

    // V-02 for the HTML goldens, which PdfOperations.ConvertHtml writes compressed.
    [Theory]
    [MemberData(nameof(HtmlGoldenTests.Documents), MemberType = typeof(HtmlGoldenTests))]
    public void Html_goldens_saved_compressed_unpack_to_their_golden_bytes(string name)
    {
        byte[] compressed;
        using (new CultureScope("")) compressed = HtmlGoldenTests.Convert(name, out _, out _, compressStreams: true);

        PdfStructureChecker.AssertValid(compressed);
        Assert.True(AssertSameUnpacked(File.ReadAllBytes(GoldenFiles.PathFor(name)), compressed, everyLibraryStream: true) > 0);
    }

    [Fact]
    public void ConvertHtml_writes_the_compressed_form()
    {
        var html = PdfHtml.FromString("<h1>Title</h1><p>Body text</p>");
        using var converted = new MemoryStream();
        PdfOperations.ConvertHtml(html, converted);

        using var built = new MemoryStream();
        PdfOperations.HtmlDocument(PdfHtml.FromString("<h1>Title</h1><p>Body text</p>"), null).Save(built);

        Assert.Equal(built.ToArray(), converted.ToArray());
        Assert.Contains(Flate, Encoding.Latin1.GetString(converted.ToArray()), StringComparison.Ordinal);
    }

    // V-03: copied streams keep their bytes and their filters, plain or compressed, through AddPages and Merge.
    [Fact]
    public void Merged_pages_keep_their_source_streams_and_filters()
    {
        var plainContent = Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Plain source) Tj ET");
        var packedContent = ReaderFixtureBuilder.Deflate(Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Packed source) Tj ET"));
        var plain = PdfFile.FromBytes(OnePageSource(plainContent, ""));
        var packed = PdfFile.FromBytes(OnePageSource(packedContent, Flate));

        var document = new PdfDocument();
        document.AddPage().AddText("Cover", 72, 720);
        document.AddPages(plain);
        document.AddPages(packed);
        var built = PdfPageReader.SaveBytes(document);
        using var merged = new MemoryStream();
        PdfOperations.Merge(merged, plain, packed);

        var (cover, coverData) = ContentOf(built, 0);
        Assert.Contains(Flate, cover, StringComparison.Ordinal);
        Assert.Contains("(Cover) Tj", Encoding.Latin1.GetString(PdfInspector.Read(built).StreamData(ContentNumber(built, 0))), StringComparison.Ordinal);
        Assert.NotEmpty(coverData);
        foreach (var (pdf, first) in new[] { (built, 1), (merged.ToArray(), 0) })
        {
            PdfStructureChecker.AssertValid(pdf);
            var (plainDictionary, plainData) = ContentOf(pdf, first);
            var (packedDictionary, packedData) = ContentOf(pdf, first + 1);
            Assert.DoesNotContain("/Filter", plainDictionary, StringComparison.Ordinal);
            Assert.Equal(plainContent, plainData);
            Assert.Contains(Flate, packedDictionary, StringComparison.Ordinal);
            Assert.Equal(packedContent, packedData);
        }
    }

    // V-04: the same input gives the same bytes, saved twice or built twice.
    [Theory]
    [MemberData(nameof(BuiltGoldenDocuments))]
    public void Compressed_output_is_deterministic(string name)
    {
        Assert.Equal(GoldenDocuments.Build(name, compressStreams: true), GoldenDocuments.Build(name, compressStreams: true));
    }

    [Fact]
    public void A_document_saved_twice_gives_the_same_compressed_bytes()
    {
        var document = RichDocument(BoxFont());

        var first = PdfPageReader.SaveBytes(document);
        var second = PdfPageReader.SaveBytes(document);

        Assert.Equal(first, second);
        Assert.Equal(first, PdfPageReader.SaveBytes(RichDocument(BoxFont())));
    }

    // V-05: every save path follows the switch.
    [Theory]
    [InlineData(true, "path")]
    [InlineData(true, "stream")]
    [InlineData(true, "path async")]
    [InlineData(true, "stream async")]
    [InlineData(false, "path")]
    [InlineData(false, "stream")]
    [InlineData(false, "path async")]
    [InlineData(false, "stream async")]
    public async Task Each_save_path_follows_the_switch(bool compress, string path)
    {
        var document = StandardTextDocument();
        document.CompressStreams = compress;

        var pdf = await SaveWith(document, path);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(compress, Encoding.Latin1.GetString(pdf).Contains(Flate, StringComparison.Ordinal));
        Assert.True(PdfPageReader.Read(pdf).Single().Shows("Hello"));
        var plain = StandardTextDocument();
        plain.CompressStreams = false;
        if (compress) AssertSameUnpacked(PdfPageReader.SaveBytes(plain), pdf, everyLibraryStream: true);
        else Assert.Equal(PdfPageReader.SaveBytes(plain), pdf);
    }

    [Fact]
    public void Changing_the_switch_between_saves_follows_the_later_value()
    {
        var document = RichDocument(BoxFont());
        var compressed = PdfPageReader.SaveBytes(document);

        document.CompressStreams = false;
        var plain = PdfPageReader.SaveBytes(document);
        document.CompressStreams = true;
        var again = PdfPageReader.SaveBytes(document);

        var fresh = RichDocument(BoxFont());
        fresh.CompressStreams = false;
        Assert.Equal(PdfPageReader.SaveBytes(fresh), plain);
        Assert.Equal(compressed, again);
        // Three pages of content and the ToUnicode map, at least, differ only by compression.
        Assert.True(AssertSameUnpacked(plain, compressed, everyLibraryStream: true) >= 4);
    }

    [Fact]
    public async Task A_save_that_fails_while_preparing_writes_nothing()
    {
        var document = new PdfDocument { FailOnMissingCharacters = true };
        document.AddPage().AddText("Hello \U0001F64F", 72, 700);
        Assert.True(document.CompressStreams);
        var existing = Encoding.ASCII.GetBytes("existing file content");
        var path = Path.Combine(Path.GetTempPath(), $"compress-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, existing);
        try
        {
            using var stream = new MemoryStream();
            Assert.Throws<PdfTextException>(() => document.Save(stream));
            await Assert.ThrowsAsync<PdfTextException>(() => document.SaveAsync(stream));
            Assert.Throws<PdfTextException>(() => document.Save(path));
            await Assert.ThrowsAsync<PdfTextException>(() => document.SaveAsync(path));

            Assert.Equal(0, stream.Length);
            Assert.Equal(existing, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // V-06: a compressed UnlimitedPDF file and the same file written plain give the same pages through every operation.
    [Theory]
    [InlineData("merge")]
    [InlineData("split")]
    [InlineData("extract")]
    [InlineData("remove")]
    public void Page_operations_on_a_compressed_file_match_the_plain_file(string operation)
    {
        var font = BoxFont();
        var compressedDocument = RichDocument(font);
        var plainDocument = RichDocument(font);
        plainDocument.CompressStreams = false;
        var compressed = PdfFile.FromBytes(PdfPageReader.SaveBytes(compressedDocument));
        var plain = PdfFile.FromBytes(PdfPageReader.SaveBytes(plainDocument));

        var expected = Run(operation, plain);
        var actual = Run(operation, compressed);

        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            PdfStructureChecker.AssertValid(actual[i]);
            Assert.Equal(PdfPageReader.Read(expected[i]), PdfPageReader.Read(actual[i]));
            AssertSameUnpacked(expected[i], actual[i], everyLibraryStream: false);
        }
    }

    [Fact]
    public void Trimming_reads_compressed_content_as_it_reads_plain_content()
    {
        var plain = PdfFile.FromBytes(SharedResources(compress: false));
        var compressed = PdfFile.FromBytes(SharedResources(compress: true));

        foreach (var file in new[] { plain, compressed })
        {
            var output = PdfFile.FromBytes(PdfOperationsExtractTests.Extract(file, PdfPageRange.Parse("2")));
            Assert.Equal((1, 1), (Count(output, "Font"), Count(output, "Image")));
            Assert.Equal(new[] { "Page 2" }, ImportedPagesTests.PageTexts(output));
        }
    }

    [Fact]
    public void Content_past_the_stream_limit_keeps_every_resource_whether_compressed_or_plain()
    {
        var limits = new PdfFileLoadOptions { MaxStreamSize = 16 };
        var plain = PdfFile.FromBytes(SharedResources(compress: false), limits);
        var compressed = PdfFile.FromBytes(SharedResources(compress: true), limits);

        foreach (var file in new[] { plain, compressed })
        {
            var output = PdfFile.FromBytes(PdfOperationsExtractTests.Extract(file, PdfPageRange.Parse("2")));
            Assert.Equal((SharedFonts, SharedImages), (Count(output, "Font"), Count(output, "Image")));
        }
    }

    // V-08: one font shared by 16 threads, each saving with compression on, gives the single-thread bytes.
    [Fact]
    public void Threads_that_share_a_font_save_the_single_thread_bytes()
    {
        var expected = PdfPageReader.SaveBytes(RichDocument(BoxFont()));
        var shared = BoxFont();
        var results = new byte[64][];

        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            using var stream = new MemoryStream();
            RichDocument(shared).Save(stream);
            results[i] = stream.ToArray();
        });

        Assert.All(results, bytes => Assert.Equal(expected, bytes));
    }

    #endregion

    #region Private Methods

    // Compares a file written plain with the same file written compressed: the same header, trailer, and objects in
    // the same order; every object other than a stream is byte-equal; a stream is either byte-equal with the same
    // dictionary, or compressed, unpacking to the plain bytes with a dictionary that differs only by /Filter and
    // /Length. With everyLibraryStream, every plain stream other than an image must be compressed (AC-06). Returns the
    // number of compressed streams.
    private static int AssertSameUnpacked(byte[] plain, byte[] compressed, bool everyLibraryStream)
    {
        var (expected, expectedRoot) = PdfStructureChecker.ReadObjects(plain);
        var (actual, actualRoot) = PdfStructureChecker.ReadObjects(compressed);
        var inspector = PdfInspector.Read(compressed);

        Assert.Equal(FirstLine(plain), FirstLine(compressed));
        Assert.Equal(Trailer(plain), Trailer(compressed));
        Assert.Equal(expectedRoot, actualRoot);
        Assert.Equal(expected.Keys.OrderBy(n => n), actual.Keys.OrderBy(n => n));
        Assert.Equal(Order(plain, expected.Keys), Order(compressed, actual.Keys));

        int count = 0;
        foreach (var (number, (dictionary, data)) in expected)
        {
            var (actualDictionary, actualData) = actual[number];
            if (data is null)
            {
                Assert.Null(actualData);
                Assert.Equal(dictionary, actualDictionary);
            }
            else if (!dictionary.Contains("/Filter", StringComparison.Ordinal) && actualDictionary.Contains(Flate, StringComparison.Ordinal))
            {
                Assert.True(data.AsSpan().SequenceEqual(inspector.StreamData(number)), $"Object {number} does not unpack to its plain bytes.");
                Assert.Equal(WithoutFilterAndLength(dictionary), WithoutFilterAndLength(actualDictionary));
                count++;
            }
            else
            {
                Assert.Equal(dictionary, actualDictionary);
                Assert.True(data.AsSpan().SequenceEqual(actualData), $"Object {number} changed.");
                if (everyLibraryStream && !dictionary.Contains("/Filter", StringComparison.Ordinal))
                    Assert.Contains("/Subtype /Image", dictionary, StringComparison.Ordinal);
            }
        }
        return count;
    }

    private static string FirstLine(byte[] pdf) => Encoding.Latin1.GetString(pdf).Split('\n')[0];

    // The object numbers in the order they are written.
    private static List<int> Order(byte[] pdf, IEnumerable<int> numbers)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return numbers.OrderBy(n => Regex.Match(text, $@"(?m)^{n} 0 obj\b").Index).ToList();
    }

    // The trailer dictionary, without the cross-reference offset that follows it.
    private static string Trailer(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        int start = text.LastIndexOf("trailer", StringComparison.Ordinal);
        return text[start..text.IndexOf("startxref", start, StringComparison.Ordinal)];
    }

    private static string WithoutFilterAndLength(string dictionary) =>
        LengthEntry.Replace(dictionary.Replace(" " + Flate, "", StringComparison.Ordinal), "/Length");

    private static PdfFont BoxFont()
    {
        var builder = new TestFontBuilder();
        builder.Map('A', builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 100, 100)));
        builder.Map('B', builder.AddGlyph(500, TestFontBuilder.Box(0, 0, 80, 120)));
        return PdfFont.FromBytes(builder.Build());
    }

    // Three pages: embedded-font text with a header and footer, a PNG and a JPEG with a table, and standard-font text.
    private static PdfDocument RichDocument(PdfFont font)
    {
        var document = new PdfDocument();
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 4, 9, PdfTextAlignment.Right);
        document.AddPage().AddText("ABBA BAAB", font, 72, 700, 14);
        var second = document.AddPage();
        second.AddImage(PdfImage.FromBytes(new PngBuilder { ColorType = 6, Width = 20, Height = 10 }.Build()), 72, 600, 40, 20);
        second.AddImage(PdfImage.FromBytes(new JpegBuilder { Width = 30, Height = 20 }.Build()), 72, 500, 60, 40);
        var table = second.AddTable(72, 400, 300);
        table.SetText(0, 0, "AB", font);
        table.GetOrCreateCell(0, 1).Text = "Cell";
        document.AddPage().AddText("Standard text", 72, 700);
        return document;
    }

    private static PdfDocument StandardTextDocument()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Hello", 72, 700);
        return document;
    }

    private static async Task<byte[]> SaveWith(PdfDocument document, string path)
    {
        if (path.StartsWith("stream", StringComparison.Ordinal))
        {
            using var stream = new MemoryStream();
            if (path.EndsWith("async", StringComparison.Ordinal)) await document.SaveAsync(stream);
            else document.Save(stream);
            return stream.ToArray();
        }

        var file = Path.Combine(Path.GetTempPath(), $"compress-{Guid.NewGuid():N}.pdf");
        try
        {
            if (path.EndsWith("async", StringComparison.Ordinal)) await document.SaveAsync(file);
            else document.Save(file);
            return File.ReadAllBytes(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static List<byte[]> Run(string operation, PdfFile file)
    {
        switch (operation)
        {
            case "merge":
                using (var merged = new MemoryStream())
                {
                    PdfOperations.Merge(merged, file, file);
                    return new List<byte[]> { merged.ToArray() };
                }
            case "split":
                return PdfOperationsSplitTests.SplitToMemory(file, 1).Select(p => p.Bytes).ToList();
            case "extract":
                return new List<byte[]> { PdfOperationsExtractTests.Extract(file, PdfPageRange.Parse("3,1")) };
            default:
                return new List<byte[]> { PdfOperationsExtractTests.Remove(file, PdfPageRange.Parse("2")) };
        }
    }

    // A one-page source whose content stream has the given data and filter entry.
    private static byte[] OnePageSource(byte[] content, string filter) => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
        .AddObject(3, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>")
        .AddStream(4, filter, content)
        .AddObject(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
        .ToBytes();

    // The raw dictionary and data of a page's content stream.
    private static (string Dictionary, byte[] Data) ContentOf(byte[] pdf, int page)
    {
        var (objects, _) = PdfStructureChecker.ReadObjects(pdf);
        var (dictionary, data) = objects[ContentNumber(pdf, page)];
        return (dictionary, data!);
    }

    private static int ContentNumber(byte[] pdf, int page)
    {
        var inspector = PdfInspector.Read(pdf);
        var match = Regex.Match(inspector.DictionaryText(inspector.Pages()[page]), @"/Contents (\d+) 0 R");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // Three pages under one page tree whose resource dictionary holds 12 fonts and 6 images; page n draws font
    // F(n-1) and image Im(n-1). Its content streams are compressed or plain.
    private static byte[] SharedResources(bool compress)
    {
        string fonts = string.Concat(Enumerable.Range(0, SharedFonts).Select(i => $"/F{i} {30 + i} 0 R "));
        string images = string.Concat(Enumerable.Range(0, SharedImages).Select(i => $"/Im{i} {50 + i} 0 R "));
        var builder = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R 5 0 R 6 0 R] /Count 3 /MediaBox [0 0 612 792] /Resources 3 0 R >>")
            .AddObject(3, $"<< /ProcSet [/PDF /Text /ImageC] /Font << {fonts}>> /XObject << {images}>> >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents 21 0 R >>")
            .AddObject(5, "<< /Type /Page /Parent 2 0 R /Contents 22 0 R >>")
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /Contents 23 0 R >>");
        for (int page = 1; page <= 3; page++)
        {
            var content = Encoding.ASCII.GetBytes($"q 10 0 0 10 0 0 cm /Im{page - 1} Do Q BT /F{page - 1} 12 Tf 72 720 Td (Page {page}) Tj ET");
            if (compress) builder.AddStream(20 + page, Flate, ReaderFixtureBuilder.Deflate(content));
            else builder.AddStream(20 + page, "", content);
        }
        for (int i = 0; i < SharedFonts; i++)
            builder.AddObject(30 + i, $"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Name /F{i} >>");
        for (int i = 0; i < SharedImages; i++)
            builder.AddStream(50 + i, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", new byte[] { (byte)i });
        return builder.ToBytes();
    }

    // Font and image objects in a file, leaving out the document's own font (object 3, WR-002).
    private static int Count(PdfFile output, string kind)
    {
        var objects = output.Source.Objects;
        return objects.ObjectNumbers
            .Where(number => number != 3)
            .Select(number => objects.Get(number))
            .Count(value => kind == "Font"
                ? value is PdfReadDictionary d && (d["Type"] as PdfReadName)?.Value == "Font"
                : value is PdfReadStream s && (s.Dictionary["Subtype"] as PdfReadName)?.Value == "Image");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<string> BuiltGoldenDocuments => new()
    {
        GoldenDocuments.FlowKeep,
        GoldenDocuments.HeaderFooter,
        GoldenDocuments.Invoice,
        GoldenDocuments.Merge,
        GoldenDocuments.MiddleTable,
        GoldenDocuments.PageLimit,
        GoldenDocuments.PaginatedTable,
        GoldenDocuments.TextPages
    };

    #endregion
}
