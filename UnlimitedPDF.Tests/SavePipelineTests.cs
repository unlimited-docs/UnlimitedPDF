using System.ComponentModel;
using System.Reflection;
using UnlimitedPDF.Models.Internal;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Save validates the whole document before writing (AC-20, A08, A10) and shares one stream
/// dictionary source between both writers.
/// </summary>
public class SavePipelineTests
{
    #region Fields and Constants

    private static readonly byte[] ExistingFileBytes = Encoding.ASCII.GetBytes("existing file content");

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Content_stream_dictionary_reports_length()
    {
        var stream = new PdfPageContentStream(4, "abc");

        Assert.Equal("<< /Length 3 >>", stream.GetStreamDictionary());
    }

    [Fact]
    public void PdfDocument_public_methods_carry_version_metadata()
    {
        // CS-013 covers methods; property accessors are special-name methods and are excluded.
        var methods = typeof(PdfDocument)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

        Assert.All(methods, method =>
        {
            Assert.NotNull(method.GetCustomAttribute<AddedVersionAttribute>());
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
        });
    }

    [Fact]
    public void Repeated_saves_produce_identical_output()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Repeat", 72, 720);

        var first = SaveToBytes(document);
        var second = SaveToBytes(document);

        Assert.Equal(first, second);
        PdfStructureChecker.AssertValid(second);
    }

    [Fact]
    public async Task SaveAsync_to_path_leaves_existing_file_unchanged_when_layout_fails()
    {
        var document = DocumentWithLayoutFailure();
        var path = CreateExistingFile();
        try
        {
            AssertLayoutFailure(await Assert.ThrowsAsync<InvalidOperationException>(() => document.SaveAsync(path)));
            Assert.Equal(ExistingFileBytes, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_to_stream_writes_nothing_when_layout_fails()
    {
        var document = DocumentWithLayoutFailure();
        using var stream = new MemoryStream();

        AssertLayoutFailure(await Assert.ThrowsAsync<InvalidOperationException>(() => document.SaveAsync(stream)));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Save_to_path_leaves_existing_file_unchanged_when_layout_fails()
    {
        var document = DocumentWithLayoutFailure();
        var path = CreateExistingFile();
        try
        {
            AssertLayoutFailure(Assert.Throws<InvalidOperationException>(() => document.Save(path)));
            Assert.Equal(ExistingFileBytes, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_to_stream_writes_nothing_when_layout_fails()
    {
        var document = DocumentWithLayoutFailure();
        using var stream = new MemoryStream();

        AssertLayoutFailure(Assert.Throws<InvalidOperationException>(() => document.Save(stream)));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task Sync_and_async_saves_write_identical_bytes()
    {
        var document = new PdfDocument();
        document.AddPage().AddText("Same bytes", 72, 720);
        using var asyncStream = new MemoryStream();

        var sync = SaveToBytes(document);
        await document.SaveAsync(asyncStream);

        Assert.Equal(sync, asyncStream.ToArray());
    }

    #endregion

    #region Private Methods

    // The document is built before the assertion, so the failure must come from Save rejecting the AC-20 layout.
    private static void AssertLayoutFailure(InvalidOperationException exception) =>
        Assert.Contains("has no height left after padding and text", exception.Message);

    private static string CreateExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"unlimitedpdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, ExistingFileBytes);
        return path;
    }

    private static PdfDocument DocumentWithLayoutFailure()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddText("Valid content before the failing table", 72, 720);

        // AC-20: an Exactly 20 pt row holding an image and 12 pt text (a 14.4 pt line) leaves the image no height.
        var table = page.AddTable(50, 700, 100);
        table.SetColumnWidths(100);
        table.GetOrCreateCell(0, 0).SetContent(PdfImage.FromBytes(new JpegBuilder { Width = 200, Height = 80 }.Build()), "Logo");
        return document;
    }

    private static byte[] SaveToBytes(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    #endregion

    #endregion
}
