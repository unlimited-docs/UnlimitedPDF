using System.ComponentModel;
using System.Reflection;
using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The test collection for tests that change the process-wide PDF input limits.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessWidePdfFileSettingsCollection
{
    /// <summary>
    /// The collection name.
    /// </summary>
    public const string Name = "Process-wide PDF file settings";
}

/// <summary>
/// An existing PDF loads once, from a path, a stream, or bytes, and is fully checked at load (WR-035, AC-14 to AC-16).
/// </summary>
[Collection(ProcessWidePdfFileSettingsCollection.Name)]
public class PdfFileLoadingTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public async Task Async_load_is_cancelled_and_produces_no_file()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = new MemoryStream(ReaderFixtureBuilder.DocumentBytes(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfFile.FromStreamAsync(stream, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Async_loads_read_the_file()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(3);
        string path = TempFile(data);
        try
        {
            var fromFile = await PdfFile.FromFileAsync(path);
            var fromStream = await PdfFile.FromStreamAsync(new NonSeekableStream(data));

            Assert.Equal(3, fromFile.PageCount);
            Assert.Equal(3, fromStream.PageCount);
            Assert.Equal(Path.GetFileName(path), fromFile.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Damaged_object_reached_by_a_page_fails_at_load()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 612 792] >>")
            .AddObject(3, "<< /Type /Page /Parent 2 0 R /Annots [4 0 R] >>")
            .AddObject(4, "<< /Subtype /Text /Contents (never closed >>")
            .ToBytes();

        var error = Assert.Throws<PdfFileException>(() => PdfFile.FromBytes(data));

        Assert.Equal(PdfFileErrorReason.Damaged, error.Reason);
    }

    [Fact]
    public void Default_limits_are_the_built_in_values()
    {
        Assert.Equal(524_288_000, PdfFile.DefaultMaxFileSize);
        Assert.Equal(104_857_600, PdfFile.DefaultMaxStreamSize);
        Assert.Equal(5_000_000, PdfFile.DefaultMaxObjectCount);
    }

    [Fact]
    public void Document_without_pages_fails_as_empty()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 >>")
            .ToBytes();

        Assert.Equal(PdfFileErrorReason.Empty, Assert.Throws<PdfFileException>(() => PdfFile.FromBytes(data)).Reason);
    }

    [Fact]
    public void Every_public_method_records_its_first_version_and_a_description()
    {
        var types = new[] { typeof(PdfFile), typeof(PdfFileLoadOptions), typeof(PdfFileException) };
        foreach (var method in types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName))
        {
            Assert.Equal("1.0.0", VersionOf(method));
            Assert.NotNull(method.GetCustomAttribute<DescriptionAttribute>());
        }
        foreach (var constructor in typeof(PdfFileException).GetConstructors())
            Assert.Equal("1.0.0", VersionOf(constructor));
    }

    [Fact]
    public void Inherited_page_attributes_are_resolved_through_nested_page_tree_nodes()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 3 /MediaBox [0 0 612 792] /Resources << /Font << >> >> >>")
            .AddObject(3, "<< /Type /Pages /Parent 2 0 R /Kids [4 0 R 5 0 R] /Count 2 /Rotate 90 >>")
            .AddObject(4, "<< /Type /Page /Parent 3 0 R >>")
            .AddObject(5, "<< /Type /Page /Parent 3 0 R /Rotate 180 /MediaBox [0 0 300 300] >>")
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /CropBox [10 10 600 780] >>")
            .ToBytes();

        var file = PdfFile.FromBytes(data);

        Assert.Equal(3, file.PageCount);
        Assert.Equal(new[] { 4, 5, 6 }, file.Pages.Select(p => p.ObjectNumber));
        Assert.Equal("90", Number(file.Pages[0].Inherited["Rotate"]));
        Assert.Equal("612", Number(Assert.IsType<PdfReadArray>(file.Pages[0].Inherited["MediaBox"])[2]));
        Assert.Equal("180", Number(file.Pages[1].Inherited["Rotate"]));
        Assert.Equal("300", Number(Assert.IsType<PdfReadArray>(file.Pages[1].Inherited["MediaBox"])[2]));
        Assert.False(file.Pages[2].Inherited.ContainsKey("Rotate"));
        Assert.True(file.Pages[2].Inherited.ContainsKey("CropBox"));
        Assert.True(file.Pages[2].Inherited.ContainsKey("Resources"));
    }

    [Fact]
    public void Non_seekable_stream_is_read_from_its_position_to_the_end_and_left_open()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(2);
        var prefixed = new byte[] { 1, 2, 3 }.Concat(data).ToArray();
        using var stream = new NonSeekableStream(prefixed);
        stream.ReadByte();
        stream.ReadByte();
        stream.ReadByte();

        var file = PdfFile.FromStream(stream);

        Assert.Equal(2, file.PageCount);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Option_values_must_be_greater_than_zero(long value)
    {
        var options = new PdfFileLoadOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxFileSize = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxStreamSize = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxObjectCount = (int)value);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFile.DefaultMaxFileSize = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFile.DefaultMaxStreamSize = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFile.DefaultMaxObjectCount = (int)value);
        Assert.Equal(524_288_000, PdfFile.DefaultMaxFileSize);
    }

    [Fact]
    public void Page_tree_that_refers_back_to_itself_fails_as_damaged()
    {
        var data = new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .AddObject(3, "<< /Type /Pages /Parent 2 0 R /Kids [2 0 R] /Count 1 >>")
            .ToBytes();

        Assert.Equal(PdfFileErrorReason.Damaged, Assert.Throws<PdfFileException>(() => PdfFile.FromBytes(data)).Reason);
    }

    [Fact]
    public void Per_load_limit_wins_over_the_process_wide_default()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(1);
        long original = PdfFile.DefaultMaxFileSize;
        try
        {
            PdfFile.DefaultMaxFileSize = 10;
            var error = Assert.Throws<PdfFileException>(() => PdfFile.FromBytes(data));
            var loaded = PdfFile.FromBytes(data, new PdfFileLoadOptions { MaxFileSize = data.Length });

            Assert.Equal(PdfFileErrorReason.TooLarge, error.Reason);
            Assert.Contains("PdfFile.DefaultMax", error.Message, StringComparison.Ordinal);
            Assert.Equal(1, loaded.PageCount);
        }
        finally
        {
            PdfFile.DefaultMaxFileSize = original;
        }
    }

    [Fact]
    public void Per_load_limit_can_be_lower_than_the_default()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(1);

        var size = Assert.Throws<PdfFileException>(() => PdfFile.FromBytes(data, new PdfFileLoadOptions { MaxFileSize = 100 }));
        var objects = Assert.Throws<PdfFileException>(() => PdfFile.FromStream(new NonSeekableStream(data), new PdfFileLoadOptions { MaxObjectCount = 2 }));

        Assert.Equal(PdfFileErrorReason.TooLarge, size.Reason);
        Assert.Equal(PdfFileErrorReason.TooLarge, objects.Reason);
    }

    [Fact]
    public void Same_file_loads_from_a_path_a_stream_and_bytes()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(4);
        string path = TempFile(data);
        try
        {
            var files = new[]
            {
                PdfFile.FromFile(path),
                PdfFile.FromStream(new MemoryStream(data)),
                PdfFile.FromBytes(data),
            };

            Assert.All(files, file => Assert.Equal(4, file.PageCount));
            Assert.Equal(Path.GetFileName(path), files[0].FileName);
            Assert.Null(files[1].FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => PdfFile.FromBytes(null!)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => PdfFile.FromFile(null!)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => PdfFile.FromStream(null!)).ParamName);
        Assert.Throws<FileNotFoundException>(() => PdfFile.FromFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf")));
    }

    [Theory]
    [InlineData("(Annual Report)", "Annual Report")]
    [InlineData("<FEFF004200E9006C0067006900750065>", "Bélgiue")]
    [InlineData("(Café \u0080 list  )", "Café • list €")]
    [InlineData("(   )", null)]
    public void Title_is_decoded_from_the_information_dictionary(string title, string? expected)
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1, title));

        Assert.Equal(expected, file.Title);
    }

    [Fact]
    public void Title_is_null_without_an_information_dictionary()
    {
        Assert.Null(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1)).Title);
    }

    #endregion

    #region Private Methods

    private static string Number(PdfReadValue value) => Assert.IsType<PdfReadNumber>(value).Text;

    private static string TempFile(byte[] data)
    {
        string path = Path.Combine(Path.GetTempPath(), $"unlimitedpdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, data);
        return path;
    }

    private static string? VersionOf(MemberInfo member) =>
        member.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "AddedVersionAttribute") is { } attribute
            ? attribute.GetType().GetProperty("Version")?.GetValue(attribute) as string
            : null;

    #endregion

    #endregion
}
