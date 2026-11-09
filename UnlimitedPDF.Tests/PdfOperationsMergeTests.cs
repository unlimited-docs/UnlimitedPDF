using System.ComponentModel;
using System.Reflection;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The one-call merge joins whole files with the same bytes as the document path, and checks everything before it
/// writes (PR-017, WR-036, AC-21, AC-22).
/// </summary>
public class PdfOperationsMergeTests
{
    #region Fields and Constants

    private static readonly byte[] ExistingFileBytes = Encoding.ASCII.GetBytes("existing file content");

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public async Task Async_merge_to_a_stream_is_cancelled_by_its_token()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfOperations.MergeAsync(output, Inputs(), cancellation.Token));
    }

    [Fact]
    public async Task Merge_bytes_equal_the_document_path_for_every_target()
    {
        var inputs = Inputs();
        var expected = DocumentPath(inputs);

        using var sync = new MemoryStream();
        PdfOperations.Merge(sync, inputs);
        using var async = new MemoryStream();
        await PdfOperations.MergeAsync(async, inputs);
        string syncPath = TempPath();
        string asyncPath = TempPath();
        try
        {
            PdfOperations.Merge(syncPath, inputs);
            await PdfOperations.MergeAsync(asyncPath, inputs);

            Assert.Equal(expected, sync.ToArray());
            Assert.Equal(expected, async.ToArray());
            Assert.Equal(expected, File.ReadAllBytes(syncPath));
            Assert.Equal(expected, File.ReadAllBytes(asyncPath));
        }
        finally
        {
            File.Delete(syncPath);
            File.Delete(asyncPath);
        }
    }

    [Fact]
    public void Merged_file_holds_every_page_in_order_with_one_bookmark_per_input()
    {
        using var output = new MemoryStream();
        PdfOperations.Merge(output, Inputs());

        var merged = PdfFile.FromBytes(output.ToArray());

        PdfStructureChecker.AssertValid(output.ToArray());
        Assert.Equal(new[] { "Page 1", "Page 2", "Page 1", "Page 2", "Page 3" }, ImportedPagesTests.PageTexts(merged));
        Assert.True(merged.Source.Catalog.ContainsKey("Outlines"));
    }

    [Fact]
    public void Bad_arguments_throw_and_leave_an_existing_file_unchanged()
    {
        string path = TempPath();
        File.WriteAllBytes(path, ExistingFileBytes);
        try
        {
            Assert.Equal("inputs", Assert.Throws<ArgumentException>(() => PdfOperations.Merge(path)).ParamName);
            Assert.Equal("inputs", Assert.Throws<ArgumentNullException>(() => PdfOperations.Merge(path, (PdfFile[])null!)).ParamName);
            Assert.Equal("inputs", Assert.Throws<ArgumentNullException>(() => PdfOperations.Merge(path, Inputs()[0], null!)).ParamName);
            Assert.Equal("inputs", Assert.Throws<ArgumentException>(() => PdfOperations.MergeAsync(path, Array.Empty<PdfFile>()).GetAwaiter().GetResult()).ParamName);

            Assert.Equal(ExistingFileBytes, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Equal("outputPath", Assert.Throws<ArgumentNullException>(() => PdfOperations.Merge((string)null!, Inputs())).ParamName);
        Assert.Equal("output", Assert.Throws<ArgumentNullException>(() => PdfOperations.Merge((Stream)null!, Inputs())).ParamName);
    }

    [Fact]
    public void Public_names_match_the_approved_merge_api()
    {
        var expected = new[] { "PdfFile", "PdfFileLoadOptions", "PdfFileException", "PdfFileErrorReason", "PdfOperations" };
        var types = typeof(PdfDocument).Assembly.GetExportedTypes().Select(t => t.Name).ToHashSet();
        Assert.All(expected, name => Assert.Contains(name, types));
        Assert.True(typeof(PdfOperations).IsAbstract && typeof(PdfOperations).IsSealed, "PdfOperations is a static class.");

        var addPages = typeof(PdfDocument).GetMethod(nameof(PdfDocument.AddPages))!;
        Assert.Equal(new[] { "file", "firstPage", "lastPage", "bookmarkTitle" }, addPages.GetParameters().Select(p => p.Name));
        Assert.Equal(new[] { "FromBytes", "FromFile", "FromFileAsync", "FromStream", "FromStreamAsync" },
            typeof(PdfFile).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        // Merge, plus the split operations (PR-017, WR-037) and the HTML conversion (WR-038) added beside it.
        Assert.Equal(new[] { "ConvertHtml", "ConvertHtml", "ConvertHtmlAsync", "ConvertHtmlAsync", "Merge", "Merge", "MergeAsync", "MergeAsync", "Split", "Split", "Split", "Split", "SplitAsync", "SplitAsync", "SplitAsync", "SplitAsync" },
            typeof(PdfOperations).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "Damaged", "Empty", "Encrypted", "TooLarge" }, Enum.GetNames(typeof(PdfFileErrorReason)).OrderBy(n => n, StringComparer.Ordinal));

        foreach (var method in typeof(PdfOperations).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Append(addPages))
        {
            Assert.Equal("1.0.0", method.GetCustomAttributes().First(a => a.GetType().Name == "AddedVersionAttribute").GetType().GetProperty("Version")!.GetValue(method.GetCustomAttributes().First(a => a.GetType().Name == "AddedVersionAttribute")));
            Assert.NotNull(method.GetCustomAttribute<DescriptionAttribute>());
        }
    }

    #endregion

    #region Private Methods

    private static byte[] DocumentPath(PdfFile[] inputs)
    {
        var document = new PdfDocument();
        foreach (var input in inputs) document.AddPages(input);
        return ImportedPagesTests.Save(document);
    }

    private static PdfFile[] Inputs() => new[]
    {
        PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2, "(First)")),
        PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)),
    };

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"unlimitedpdf-{Guid.NewGuid():N}.pdf");

    #endregion

    #endregion
}
