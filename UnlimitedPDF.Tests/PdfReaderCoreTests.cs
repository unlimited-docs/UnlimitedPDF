using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The reader turns PDF syntax into values without changing their bytes, and reads classic cross-reference tables
/// (WR-035, AC-12).
/// </summary>
public class PdfReaderCoreTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Array_reads_references_and_numbers()
    {
        var array = Assert.IsType<PdfReadArray>(Parse("[1 0 R 0 0 612 792 3 0 R]"));

        Assert.Equal(6, array.Count);
        Assert.Equal(1, Assert.IsType<PdfReadReference>(array[0]).ObjectNumber);
        Assert.Equal("0", Assert.IsType<PdfReadNumber>(array[1]).Text);
        Assert.Equal(792, Assert.IsType<PdfReadNumber>(array[4]).IntegerValue);
        Assert.Equal(3, Assert.IsType<PdfReadReference>(array[5]).ObjectNumber);
    }

    [Fact]
    public void Classic_table_with_several_subsections_reads_every_object()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 >>")
            .AddObject(3, "(three)")
            .ToBytes(oneSubsectionPerObject: true);

        var table = Read(data);

        Assert.Equal("Catalog", Name(Assert.IsType<PdfReadDictionary>(table.Get(1))["Type"]));
        Assert.Equal("three", Latin1(Assert.IsType<PdfReadString>(table.Get(3)).Bytes));
        Assert.Equal(new[] { 1, 2, 3 }, table.ObjectNumbers.OrderBy(n => n));
    }

    [Fact]
    public void Cross_reference_stream_lists_every_object()
    {
        var table = Read(ThreeObjects().ToBytesWithCrossReferenceStream());

        Assert.Equal("Catalog", Name(Assert.IsType<PdfReadDictionary>(table.Get(1))["Type"]));
        Assert.Equal("three é", Latin1(Assert.IsType<PdfReadString>(table.Get(3)).Bytes));
        Assert.Equal(1, Assert.IsType<PdfReadReference>(table.Trailer["Root"]).ObjectNumber);
    }

    [Fact]
    public void Cross_reference_stream_with_png_predictor_lists_every_object()
    {
        var table = Read(ThreeObjects().ToBytesWithCrossReferenceStream(pngPredictor: true));

        Assert.Equal("Pages", Name(Assert.IsType<PdfReadDictionary>(table.Get(2))["Type"]));
        Assert.Equal("three é", Latin1(Assert.IsType<PdfReadString>(table.Get(3)).Bytes));
    }

    [Fact]
    public void Cross_reference_loop_fails_as_damaged()
    {
        // The trailer's /Prev points back at its own section.
        var data = ThreeObjects().ToBytes(trailerEntries: "/Root 1 0 R /Prev 00000000");
        string offset = CrossReferenceReader.FindStartXref(data).ToString("D8", CultureInfo.InvariantCulture);
        var patched = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(data).Replace("/Prev 00000000", "/Prev " + offset));

        var error = Assert.Throws<PdfReadException>(() => CrossReferenceReader.ReadChain(patched, PdfReadLimits.Default));

        Assert.Equal(PdfReadError.Damaged, error.Error);
    }

    [Fact]
    public void Hybrid_file_reads_objects_from_its_hidden_stream()
    {
        var table = Read(ThreeObjects().ToHybridBytes(compressed: new[] { 2, 3 }));

        Assert.Equal("Catalog", Name(Assert.IsType<PdfReadDictionary>(table.Get(1))["Type"]));
        Assert.Equal("Pages", Name(Assert.IsType<PdfReadDictionary>(table.Get(2))["Type"]));
        Assert.Equal("three é", Latin1(Assert.IsType<PdfReadString>(table.Get(3)).Bytes));
    }

    [Fact]
    public void Incremental_updates_keep_the_newest_object_and_drop_deleted_ones()
    {
        var original = ThreeObjects().AddObject(4, "(first)").AddObject(5, "(kept)").ToBytes();
        var second = new ReaderFixtureBuilder().AddObject(4, "(second)").AppendUpdateTo(original);
        var third = new ReaderFixtureBuilder().AddObject(4, "(third)").AppendUpdateTo(second, freed: 3);

        var table = Read(third);

        Assert.Equal("third", Latin1(Assert.IsType<PdfReadString>(table.Get(4)).Bytes));
        Assert.Equal("kept", Latin1(Assert.IsType<PdfReadString>(table.Get(5)).Bytes));
        Assert.Same(PdfReadNull.Instance, table.Get(3));
        Assert.DoesNotContain(3, table.ObjectNumbers);
    }

    [Fact]
    public void Inflating_stops_at_the_limit()
    {
        var bomb = ReaderFixtureBuilder.Deflate(new byte[2_000_000]);

        var error = Assert.Throws<PdfReadException>(() => FlateReader.Decode(bomb, maxOutput: 100_000));

        Assert.Equal(PdfReadError.TooLarge, error.Error);
        Assert.True(bomb.Length < 10_000, "The fixture is small but inflates past the limit.");
    }

    [Fact]
    public void Object_stream_objects_read_including_an_extends_entry()
    {
        var table = Read(ThreeObjects().ToBytesWithCrossReferenceStream(compressed: new[] { 2, 3 }, objectStreamEntries: "/Extends 1 0 R"));

        var pages = Assert.IsType<PdfReadDictionary>(table.Get(2));
        Assert.Equal("Pages", Name(pages["Type"]));
        Assert.Equal("three é", Latin1(Assert.IsType<PdfReadString>(table.Resolve(pages["Note"])).Bytes));
    }

    [Fact]
    public void Unsupported_filter_on_a_cross_reference_stream_fails_as_damaged()
    {
        var data = ThreeObjects().ToBytesWithCrossReferenceStream();
        var patched = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(data).Replace("/Filter /FlateDecode", "/Filter /LZWDecode  "));

        var error = Assert.Throws<PdfReadException>(() => CrossReferenceReader.ReadChain(patched, PdfReadLimits.Default));

        Assert.Equal(PdfReadError.Damaged, error.Error);
    }

    [Fact]
    public void Dictionary_keeps_every_value_type_and_key_order()
    {
        var dictionary = Assert.IsType<PdfReadDictionary>(
            Parse("<< /Z true /A false /N null /I -12 /R 3.25 /S (text) /H <414243> /Na /Name /Ar [1 2] /D << /K 1 >> >>"));

        Assert.Equal(new[] { "Z", "A", "N", "I", "R", "S", "H", "Na", "Ar", "D" }, dictionary.Keys);
        Assert.True(Assert.IsType<PdfReadBoolean>(dictionary["Z"]).Value);
        Assert.False(Assert.IsType<PdfReadBoolean>(dictionary["A"]).Value);
        Assert.Same(PdfReadNull.Instance, dictionary["N"]);
        Assert.Equal(-12, Assert.IsType<PdfReadNumber>(dictionary["I"]).IntegerValue);
        Assert.Equal("3.25", Assert.IsType<PdfReadNumber>(dictionary["R"]).Text);
        Assert.Equal("text", Latin1(Assert.IsType<PdfReadString>(dictionary["S"]).Bytes));
        Assert.Equal("ABC", Latin1(Assert.IsType<PdfReadString>(dictionary["H"]).Bytes));
        Assert.Equal("Name", Name(dictionary["Na"]));
        Assert.Equal(2, Assert.IsType<PdfReadArray>(dictionary["Ar"]).Count);
        Assert.IsType<PdfReadDictionary>(dictionary["D"]);
    }

    [Theory]
    [InlineData("%PDF-1.0", "1.0")]
    [InlineData("%PDF-1.4", "1.4")]
    [InlineData("%PDF-1.7", "1.7")]
    [InlineData("%PDF-2.0", "2.0")]
    public void Header_version_is_read(string header, string version)
    {
        var data = new ReaderFixtureBuilder().AddObject(1, "<< /Type /Catalog >>").ToBytes(header);

        Assert.Equal(version, PdfHeaderReader.ReadVersion(data));
    }

    [Fact]
    public void Header_after_leading_bytes_is_found_and_missing_header_reads_null()
    {
        Assert.Equal("1.6", PdfHeaderReader.ReadVersion(Encoding.ASCII.GetBytes("junk\n%PDF-1.6\n")));
        Assert.Null(PdfHeaderReader.ReadVersion(Encoding.ASCII.GetBytes("not a pdf")));
    }

    [Fact]
    public void Hex_string_ignores_spaces_and_pads_an_odd_digit()
    {
        var value = Assert.IsType<PdfReadString>(Parse("<48 65 6C 6C 6F 7>"));

        Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x70 }, value.Bytes);
    }

    [Fact]
    public void High_bytes_in_strings_are_kept()
    {
        var value = Assert.IsType<PdfReadString>(Parse("(éÿ\u0080)"));

        Assert.Equal(new byte[] { 0xE9, 0xFF, 0x80 }, value.Bytes);
    }

    [Fact]
    public void Literal_string_escapes_are_decoded()
    {
        var value = Assert.IsType<PdfReadString>(Parse("(a\\(b\\)c\\\\ \\101\\60 (nested) \\n\\t end\\\nline)"));

        Assert.Equal("a(b)c\\ A0 (nested) \n\t endline", Latin1(value.Bytes));
    }

    [Fact]
    public void Name_escapes_are_decoded()
    {
        Assert.Equal("A B#", Name(Parse("/A#20B#")));
    }

    [Fact]
    public void Nesting_past_the_cap_fails_as_damaged()
    {
        int depth = PdfParser.MaxDepth + 10;
        string text = new string('[', depth) + new string(']', depth);

        var error = Assert.Throws<PdfReadException>(() => Parse(text));

        Assert.Equal(PdfReadError.Damaged, error.Error);
    }

    [Fact]
    public void Nesting_at_the_cap_reads()
    {
        string text = new string('[', PdfParser.MaxDepth) + new string(']', PdfParser.MaxDepth);

        Assert.IsType<PdfReadArray>(Parse(text));
    }

    [Fact]
    public void Object_not_at_its_offset_fails_as_damaged()
    {
        var data = new ReaderFixtureBuilder().AddObject(1, "<< /Type /Catalog >>").AddObject(2, "(x)").ToBytes();
        var entries = new Dictionary<int, CrossReferenceEntry>
        {
            [2] = new(CrossReferenceEntryKind.InUse, PdfByteSearch.IndexOf(data, "1 0 obj", 0, data.Length)),
        };

        var table = new PdfObjectTable(data, entries, new PdfReadDictionary());

        Assert.Equal(PdfReadError.Damaged, Assert.Throws<PdfReadException>(() => table.Get(2)).Error);
    }

    [Fact]
    public void Reference_to_a_free_or_unknown_object_reads_null()
    {
        var table = Read(new ReaderFixtureBuilder().AddObject(1, "<< /Type /Catalog >>").AddObject(3, "(x)").ToBytes());

        Assert.Same(PdfReadNull.Instance, table.Get(2));
        Assert.Same(PdfReadNull.Instance, table.Get(99));
        Assert.Same(PdfReadNull.Instance, table.Resolve(new PdfReadReference(2, 0)));
    }

    [Fact]
    public void Stream_length_given_as_a_reference_is_resolved()
    {
        var content = Encoding.ASCII.GetBytes("BT ET endstream-like text");
        var table = Read(new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog >>")
            .AddStream(2, "", content, length: "3 0 R")
            .AddObject(3, content.Length.ToString(CultureInfo.InvariantCulture))
            .ToBytes());

        var stream = Assert.IsType<PdfReadStream>(table.Get(2));

        Assert.Equal(content, stream.Data.ToArray());
    }

    [Fact]
    public void Stream_with_a_wrong_length_runs_to_endstream()
    {
        var content = new byte[] { 0x00, 0xFF, 0x10, 0x0D, 0x0A, 0x20 };
        var table = Read(new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog >>")
            .AddStream(2, "/Filter /None", content, length: "2")
            .ToBytes());

        var stream = Assert.IsType<PdfReadStream>(table.Get(2));

        Assert.Equal(content, stream.Data.ToArray());
        Assert.Equal("None", Name(stream.Dictionary["Filter"]));
    }

    [Fact]
    public void Trailer_and_startxref_are_read()
    {
        var data = new ReaderFixtureBuilder().AddObject(1, "<< /Type /Catalog >>").ToBytes(trailerEntries: "/Root 1 0 R /Extra (x)");

        var section = CrossReferenceReader.ReadSection(data, CrossReferenceReader.FindStartXref(data));

        Assert.Equal(1, Assert.IsType<PdfReadReference>(section.Trailer["Root"]).ObjectNumber);
        Assert.Equal(2, Assert.IsType<PdfReadNumber>(section.Trailer["Size"]).IntegerValue);
        Assert.Equal(CrossReferenceEntryKind.Free, section.Entries[0].Kind);
        Assert.Equal(CrossReferenceEntryKind.InUse, section.Entries[1].Kind);
    }

    [Theory]
    [InlineData("(never closed")]
    [InlineData("<414")]
    [InlineData("<< /Key >>")]
    [InlineData("<< 1 2 >>")]
    [InlineData("[1 2")]
    [InlineData("endobj")]
    [InlineData("{ }")]
    public void Invalid_syntax_fails_as_damaged(string text)
    {
        Assert.Equal(PdfReadError.Damaged, Assert.Throws<PdfReadException>(() => Parse(text)).Error);
    }

    [Fact]
    public void Missing_startxref_fails_as_damaged()
    {
        var data = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< >>\nendobj\n");

        Assert.Equal(PdfReadError.Damaged, Assert.Throws<PdfReadException>(() => CrossReferenceReader.FindStartXref(data)).Error);
    }

    #endregion

    #region Private Methods

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static string Name(PdfReadValue? value) => Assert.IsType<PdfReadName>(value).Value;

    private static PdfReadValue Parse(string text) =>
        new PdfParser(new PdfLexer(Encoding.Latin1.GetBytes(text))).ParseValue();

    private static PdfObjectTable Read(byte[] data)
    {
        var chain = CrossReferenceReader.ReadChain(data, PdfReadLimits.Default);
        return new PdfObjectTable(data, chain.Entries, chain.Trailer);
    }

    private static ReaderFixtureBuilder ThreeObjects() => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 /Note 3 0 R >>")
        .AddObject(3, "(three é)");

    #endregion

    #endregion
}
