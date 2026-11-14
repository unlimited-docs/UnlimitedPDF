namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Loading caller fonts (AC-01, AC-02, WR-039, PR-022): every load method reads the same font; limits, null
/// arguments, file errors, and cancellation behave like the other loaders (WR-026); embedding permissions are honored
/// unless the caller overrides them; a font with one weight rejects a different weight.
/// </summary>
[Collection(nameof(PdfFontDefaultLimitCollection))]
public class PdfFontLoadingTests : IDisposable
{
    #region Fields and Constants

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "unlimitedpdf-font-tests-" + Guid.NewGuid().ToString("N"));

    #endregion

    #region Constructors and Finalizers

    public PdfFontLoadingTests()
    {
        Directory.CreateDirectory(_folder);
    }

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Every_load_method_reads_the_same_font()
    {
        var bytes = Font();
        string path = Write(bytes);

        var fonts = new[]
        {
            PdfFont.FromBytes(bytes),
            PdfFont.FromFile(path),
            await PdfFont.FromFileAsync(path),
            PdfFont.FromStream(new MemoryStream(bytes)),
            await PdfFont.FromStreamAsync(new MemoryStream(bytes))
        };

        foreach (var font in fonts)
        {
            Assert.Equal("Test Sans", font.FamilyName);
            Assert.Equal(400, font.Weight);
            Assert.False(font.IsItalic);
            Assert.False(font.IsVariable);
            Assert.Equal(400, font.MinimumWeight);
            Assert.Equal(400, font.MaximumWeight);
        }
    }

    [Fact]
    public void A_stream_is_read_from_its_position_and_left_open()
    {
        var bytes = Font();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 }.Concat(bytes).ToArray()) { Position = 3 };

        PdfFont.FromStream(stream);

        Assert.True(stream.CanRead);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void FromBytes_copies_the_array()
    {
        var bytes = Font();
        var font = PdfFont.FromBytes(bytes);
        Array.Clear(bytes);

        Assert.Equal("Test Sans", font.FamilyName);
    }

    [Fact]
    public void Style_and_weight_come_from_the_font()
    {
        var font = PdfFont.FromBytes(Font(b => { b.Weight = 700; b.Italic = true; }));

        Assert.Equal(700, font.Weight);
        Assert.True(font.IsItalic);
    }

    [Fact]
    public void Null_arguments_throw_argument_null_exceptions()
    {
        Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => PdfFont.FromBytes(null!)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => PdfFont.FromFile(null!)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => PdfFont.FromStream(null!)).ParamName);
        Assert.Equal("path", Assert.ThrowsAsync<ArgumentNullException>(() => PdfFont.FromFileAsync(null!)).Result.ParamName);
        Assert.Equal("stream", Assert.ThrowsAsync<ArgumentNullException>(() => PdfFont.FromStreamAsync(null!)).Result.ParamName);
    }

    [Fact]
    public void A_missing_file_throws_the_standard_exception()
    {
        Assert.Throws<FileNotFoundException>(() => PdfFont.FromFile(Path.Combine(_folder, "missing.ttf")));
    }

    [Fact]
    public async Task A_cancelled_load_throws_operation_canceled()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfFont.FromStreamAsync(new MemoryStream(Font()), cancellationToken: cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfFont.FromFileAsync(Write(Font()), cancellationToken: cancel.Token));
    }

    [Fact]
    public void Data_over_the_per_load_limit_is_too_large_for_every_method()
    {
        var bytes = Font();
        var options = new PdfFontLoadOptions { MaxFileSize = bytes.Length - 1 };
        string path = Write(bytes);

        foreach (var load in new Func<PdfFont>[]
                 {
                     () => PdfFont.FromBytes(bytes, options),
                     () => PdfFont.FromFile(path, options),
                     () => PdfFont.FromStream(new MemoryStream(bytes), options),
                     () => PdfFont.FromFileAsync(path, options).GetAwaiter().GetResult(),
                     () => PdfFont.FromStreamAsync(new MemoryStream(bytes), options).GetAwaiter().GetResult()
                 })
        {
            Assert.Equal(PdfFontErrorReason.TooLarge, Assert.Throws<PdfFontException>(load).Reason);
        }

        PdfFont.FromBytes(bytes, new PdfFontLoadOptions { MaxFileSize = bytes.Length });
    }

    [Fact]
    public void The_process_wide_limit_applies_when_a_load_sets_none()
    {
        var bytes = Font();
        long original = PdfFont.DefaultMaxFileSize;
        try
        {
            Assert.Equal(52_428_800, original);
            PdfFont.DefaultMaxFileSize = bytes.Length - 1;

            Assert.Equal(PdfFontErrorReason.TooLarge, Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes)).Reason);
            PdfFont.FromBytes(bytes, new PdfFontLoadOptions { MaxFileSize = bytes.Length }); // the per-load limit wins
        }
        finally
        {
            PdfFont.DefaultMaxFileSize = original;
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Limits_must_be_positive(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFontLoadOptions { MaxFileSize = value });
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFont.DefaultMaxFileSize = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Weights_must_be_from_1_to_1000(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFontLoadOptions { Weight = value });
    }

    [Fact]
    public void Damaged_and_unsupported_data_fail_with_a_reason()
    {
        Assert.Equal(PdfFontErrorReason.UnsupportedFormat, Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(Encoding.ASCII.GetBytes("wOF2 not a real font"))).Reason);
        Assert.Equal(PdfFontErrorReason.CorruptData, Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(Font().AsSpan(0, 200).ToArray())).Reason);
    }

    [Theory]
    [InlineData((ushort)0x0002, "restricted license")]
    [InlineData((ushort)0x0200, "only bitmap")]
    [InlineData((ushort)0x0202, "restricted license")]
    public void Fonts_that_forbid_embedding_fail_unless_overridden(ushort fsType, string message)
    {
        var bytes = Font(b => b.FsType = fsType);

        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes));
        Assert.Equal(PdfFontErrorReason.EmbeddingRestricted, error.Reason);
        Assert.Contains(message, error.Message);

        PdfFont.FromBytes(bytes, new PdfFontLoadOptions { IgnoreEmbeddingRestrictions = true });
    }

    [Theory]
    [InlineData((ushort)0x0000, false)]
    [InlineData((ushort)0x0004, false)]
    [InlineData((ushort)0x0008, false)]
    [InlineData((ushort)0x0006, false)] // restricted plus preview-and-print: the less restrictive applies
    [InlineData((ushort)0x0100, true)]
    [InlineData((ushort)0x0108, true)]
    public void Fonts_that_allow_embedding_load_and_no_subsetting_is_remembered(ushort fsType, bool whole)
    {
        var font = PdfFont.FromBytes(Font(b => b.FsType = fsType));

        Assert.Equal(whole, font.EmbedWholeFont);
    }

    [Fact]
    public void A_single_weight_font_rejects_another_weight()
    {
        var bytes = Font();

        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 700 }));
        Assert.Equal(PdfFontErrorReason.WeightNotAvailable, error.Reason);
        Assert.Contains("only weight 400", error.Message);

        Assert.Equal(400, PdfFont.FromBytes(bytes, new PdfFontLoadOptions { Weight = 400 }).Weight);
    }

    #endregion

    #region Private Methods

    private static byte[] Font(Action<TestFontBuilder>? configure = null)
    {
        var builder = new TestFontBuilder();
        configure?.Invoke(builder);
        builder.MapCharacters("Hello");
        return builder.Build();
    }

    private string Write(byte[] bytes)
    {
        string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".ttf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    #endregion

    #endregion
}

/// <summary>
/// Tests that change <see cref="PdfFont.DefaultMaxFileSize"/> run one at a time.
/// </summary>
[CollectionDefinition(nameof(PdfFontDefaultLimitCollection), DisableParallelization = true)]
public class PdfFontDefaultLimitCollection
{
}
