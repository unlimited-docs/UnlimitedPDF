using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Existing PDFs that cannot be used fail at load with a specific reason, and a broken cross-reference table is
/// recovered (WR-035, AC-10, AC-11, A04).
/// </summary>
public class PdfFileErrorTests
{
    #region Fields and Constants

    private const string StandardEncryption = "<< /Filter /Standard /V 2 /R 3 /Length 128 /O <00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF> /U <FFEEDDCCBBAA99887766554433221100FFEEDDCCBBAA99887766554433221100> /P {0} >>";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Broken_offsets_are_recovered_by_scanning()
    {
        // Extra bytes after the header move every object away from its listed offset.
        var data = Document().ToBytes();
        var shifted = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(data).Replace("%PDF-1.7\n", "%PDF-1.7\n%padding padding\n"));

        var file = PdfFileReader.Load(shifted, PdfReadLimits.Default);

        Assert.Equal("Catalog", Assert.IsType<PdfReadName>(file.Catalog["Type"]).Value);
        Assert.Equal("three", Encoding.Latin1.GetString(Assert.IsType<PdfReadString>(file.Objects.Get(3)).Bytes));
    }

    [Fact]
    public void Wrong_startxref_is_recovered_by_scanning()
    {
        var data = Document().ToBytes();
        string text = Encoding.Latin1.GetString(data);
        int index = text.LastIndexOf("startxref\n", StringComparison.Ordinal);
        var patched = Encoding.Latin1.GetBytes(text.Substring(0, index) + "startxref\n99999999\n%%EOF\n");

        var file = PdfFileReader.Load(patched, PdfReadLimits.Default);

        Assert.Equal("Pages", Assert.IsType<PdfReadName>(Assert.IsType<PdfReadDictionary>(file.Objects.Get(2))["Type"]).Value);
    }

    [Theory]
    [InlineData("-4")]
    [InlineData("-3904")]
    public void Encrypted_file_fails_as_encrypted(string permissions)
    {
        // -4 allows everything (a permission-only file usually opens without a password); -3904 restricts printing
        // and copying. Both are rejected the same way, before any page object is read.
        var data = Document()
            .AddObject(4, string.Format(CultureInfo.InvariantCulture, StandardEncryption, permissions))
            .ToBytes(trailerEntries: "/Root 1 0 R /Encrypt 4 0 R /ID [<0011> <0011>]");

        AssertReason(data, PdfFileErrorReason.Encrypted);
    }

    [Fact]
    public void Encrypted_file_with_a_broken_table_still_fails_as_encrypted()
    {
        var data = Document()
            .AddObject(4, string.Format(CultureInfo.InvariantCulture, StandardEncryption, "-4"))
            .ToBytes(trailerEntries: "/Root 1 0 R /Encrypt 4 0 R");
        var shifted = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(data).Replace("%PDF-1.7\n", "%PDF-1.7\n%shift\n"));

        AssertReason(shifted, PdfFileErrorReason.Encrypted);
    }

    [Fact]
    public void Encryption_named_only_by_an_older_trailer_fails_as_encrypted()
    {
        var original = Document()
            .AddObject(4, string.Format(CultureInfo.InvariantCulture, StandardEncryption, "-4"))
            .ToBytes(trailerEntries: "/Root 1 0 R /Encrypt 4 0 R");
        var updated = new ReaderFixtureBuilder().AddObject(3, "(changed)").AppendUpdateTo(original);

        AssertReason(updated, PdfFileErrorReason.Encrypted);
    }

    [Fact]
    public void Empty_data_fails_as_empty()
    {
        AssertReason(Array.Empty<byte>(), PdfFileErrorReason.Empty);
    }

    [Theory]
    [InlineData("This is a text file, not a PDF.")]
    [InlineData("%PDF-1.7\n1 0 ob")]
    [InlineData("%PDF-1.7\n%%EOF\n")]
    [InlineData("%PDF-1.7\n1 0 obj\n<< /Type /Pages >>\nendobj\n%%EOF\n")]
    public void Data_that_is_not_a_readable_pdf_fails_as_damaged(string text)
    {
        AssertReason(Encoding.ASCII.GetBytes(text), PdfFileErrorReason.Damaged);
    }

    [Fact]
    public void Random_bytes_fail_as_damaged()
    {
        var random = new Random(12);
        var data = new byte[4096];
        random.NextBytes(data);

        AssertReason(data, PdfFileErrorReason.Damaged);
    }

    [Fact]
    public void Failure_carries_the_reader_message_and_inner_exception()
    {
        var error = Assert.Throws<PdfFileException>(() => PdfFileReader.Load(Encoding.ASCII.GetBytes("plain text"), PdfReadLimits.Default));

        Assert.Contains("%PDF-", error.Message, StringComparison.Ordinal);
        Assert.IsType<PdfReadException>(error.InnerException);
    }

    [Fact]
    public void Pdf_input_code_uses_no_cryptography()
    {
        // A04 stays out of scope because PDF input detects encryption and never decrypts (D-020). This keeps any
        // cipher, hash, or key code out of the reading code.
        string root = Path.GetFullPath(Path.Combine(Fixtures.DemoAssets(), "..", "..", "UnlimitedPDF"));
        var files = Directory.GetFiles(Path.Combine(root, "Models", "Internal", "Reading"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "Models"), "PdfFile*.cs"))
            .ToList();
        string[] forbidden = { "System.Security.Cryptography", "MD5", "RC4", "Rc4", "Aes", "SHA256", "Decrypt" };

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            string source = File.ReadAllText(file);
            foreach (var word in forbidden)
                Assert.False(source.Contains(word, StringComparison.Ordinal), $"{Path.GetFileName(file)} contains '{word}'.");
        }
    }

    #endregion

    #region Private Methods

    private static void AssertReason(byte[] data, PdfFileErrorReason reason)
    {
        var error = Assert.Throws<PdfFileException>(() => PdfFileReader.Load(data, PdfReadLimits.Default));
        Assert.Equal(reason, error.Reason);
    }

    private static ReaderFixtureBuilder Document() => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 >>")
        .AddObject(3, "(three)");

    #endregion

    #endregion
}
