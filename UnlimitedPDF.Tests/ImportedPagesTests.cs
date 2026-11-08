using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Pages of an existing PDF join a document unchanged, in call order, mixed with built pages (WR-036, AC-01 to
/// AC-03, AC-13, AC-16).
/// </summary>
public class ImportedPagesTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Bad_page_bounds_throw_naming_the_parameter_and_change_nothing()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));
        var document = new PdfDocument();
        document.AddPage().AddText("Cover", 72, 72);
        var before = Save(document);

        Assert.Equal("firstPage", Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPages(file, firstPage: 0)).ParamName);
        Assert.Equal("firstPage", Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPages(file, firstPage: 4)).ParamName);
        Assert.Equal("lastPage", Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPages(file, lastPage: 4)).ParamName);
        Assert.Equal("lastPage", Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPages(file, firstPage: 3, lastPage: 2)).ParamName);
        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => document.AddPages(null!)).ParamName);

        Assert.Equal(before, Save(document));
    }

    [Fact]
    public void Built_and_imported_pages_keep_call_order()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));
        var document = new PdfDocument();
        document.AddPage().AddText("Cover", 72, 72);
        document.AddPages(file, firstPage: 1, lastPage: 1);
        document.AddPage().AddText("Middle", 72, 72);
        document.AddPages(file, firstPage: 3, lastPage: 3);

        var output = Read(Save(document));

        Assert.Equal(new[] { "Cover", "Page 1", "Middle", "Page 3" }, PageTexts(output));
    }

    [Fact]
    public void Dropped_page_keys_are_absent_and_pages_hang_under_the_document_tree()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /StructParents 0 /AA << /O 4 0 R >> /Tabs /S /UserUnit 1 >>")
            .AddObject(4, "<< /S /JavaScript /JS (app.alert(1)) >>")
            .ToBytes();
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(data));

        var pdf = Save(document);
        var page = Read(pdf).Pages[0].Dictionary;

        Assert.Equal(2, Assert.IsType<PdfReadReference>(page["Parent"]).ObjectNumber);
        Assert.False(page.ContainsKey("StructParents"));
        Assert.False(page.ContainsKey("AA"));
        Assert.False(page.ContainsKey("Tabs"));
        Assert.True(page.ContainsKey("UserUnit"));
        Assert.DoesNotContain("JavaScript", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Inherited_attributes_are_written_on_each_imported_page()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 300 400] /Rotate 90 >>")
            .AddObject(3, "<< /Type /Pages /Parent 2 0 R /Kids [4 0 R] /Count 1 /CropBox [10 10 290 390] >>")
            .AddObject(4, "<< /Type /Page /Parent 3 0 R /Contents 5 0 R >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("0 0 m 10 10 l S"))
            .ToBytes();
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(data));

        var page = Read(Save(document)).Pages[0].Dictionary;

        Assert.Equal("400", Assert.IsType<PdfReadNumber>(Assert.IsType<PdfReadArray>(page["MediaBox"])[3]).Text);
        Assert.Equal("290", Assert.IsType<PdfReadNumber>(Assert.IsType<PdfReadArray>(page["CropBox"])[2]).Text);
        Assert.Equal("90", Assert.IsType<PdfReadNumber>(page["Rotate"]).Text);
    }

    [Fact]
    public void Merged_output_passes_the_structural_checker_and_saves_the_same_twice()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Cover", 72, 72);
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)));
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.Document(2).ToBytesWithCrossReferenceStream(compressed: new[] { 3 })));

        var first = Save(document);
        var second = Save(document);

        PdfStructureChecker.AssertValid(first);
        Assert.Equal(first, second);
        Assert.Equal(5, Read(first).PageCount);
    }

    [Fact]
    public void One_file_imported_twice_shares_its_font()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));
        var document = new PdfDocument();
        document.AddPages(file, firstPage: 1, lastPage: 1);
        document.AddPages(file, firstPage: 2, lastPage: 3);

        string text = Encoding.Latin1.GetString(Save(document));

        // The document's own font plus one copy of the imported file's font.
        Assert.Equal(2, CountOf(text, "/BaseFont /Helvetica"));
    }

    [Theory]
    [InlineData("%PDF-1.4", "%PDF-1.7")]
    [InlineData("%PDF-1.7", "%PDF-1.7")]
    [InlineData("%PDF-2.0", "%PDF-2.0")]
    public void Output_declares_the_highest_input_version_and_at_least_1_7(string input, string output)
    {
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.Document(1).ToBytes(header: input)));

        Assert.StartsWith(output, Encoding.ASCII.GetString(Save(document), 0, 8), StringComparison.Ordinal);
    }

    [Fact]
    public void Page_content_is_copied_unchanged()
    {
        var content = new byte[] { 0x42, 0x54, 0x0A, 0xFF, 0x00, 0x80, 0x0A, 0x45, 0x54 };
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>")
            .AddStream(4, "/Filter /FlateDecode /DecodeParms << /Predictor 1 >>", content)
            .ToBytes();
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(data));

        var output = Read(Save(document));
        var stream = Assert.IsType<PdfReadStream>(output.Source.Objects.Resolve(output.Pages[0].Dictionary["Contents"]));

        Assert.Equal(content, stream.Data.ToArray());
        Assert.Equal("FlateDecode", Assert.IsType<PdfReadName>(stream.Dictionary["Filter"]).Value);
    }

    [Fact]
    public void Ranges_and_whole_files_import_from_every_input_form()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(4);
        string path = Path.Combine(Path.GetTempPath(), $"unlimitedpdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, data);
        try
        {
            var document = new PdfDocument();
            document.AddPages(PdfFile.FromFile(path));
            document.AddPages(PdfFile.FromStream(new NonSeekableStream(data)), firstPage: 2, lastPage: 3);
            document.AddPages(PdfFile.FromBytes(data), firstPage: 4);

            var output = Read(Save(document));

            Assert.Equal(new[] { "Page 1", "Page 2", "Page 3", "Page 4", "Page 2", "Page 3", "Page 4" }, PageTexts(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Document_without_imports_still_declares_1_7()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Plain", 72, 72);

        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(Save(document), 0, 8), StringComparison.Ordinal);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the text drawn by each page's <c>Tj</c> operators, one string per page.
    /// </summary>
    internal static string[] PageTexts(PdfFile output) => output.Pages
        .Select(page => output.Source.Objects.Resolve(page.Dictionary["Contents"]))
        .Select(contents => contents is PdfReadStream stream ? Encoding.Latin1.GetString(stream.Data.ToArray()) : "")
        .Select(text => string.Join(" ", System.Text.RegularExpressions.Regex.Matches(text, @"\((.*?)\)\s*Tj").Select(m => m.Groups[1].Value)))
        .ToArray();

    /// <summary>
    /// Reads a saved document back with the library's own reader.
    /// </summary>
    internal static PdfFile Read(byte[] pdf) => PdfFile.FromBytes(pdf);

    /// <summary>
    /// Saves a document to bytes.
    /// </summary>
    internal static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    #endregion

    #region Private Methods

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
            count++;
        return count;
    }

    #endregion

    #endregion
}
