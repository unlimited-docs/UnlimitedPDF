namespace UnlimitedPDF.Tests;

/// <summary>
/// <see cref="PdfOperations.ExtractAsync(PdfFile, IEnumerable{PdfPageRange}, Stream, CancellationToken)"/> and
/// <see cref="PdfOperations.RemoveAsync(PdfFile, IEnumerable{PdfPageRange}, Stream, CancellationToken)"/> write the same
/// bytes as their synchronous twins and honor cancellation as <c>MergeAsync</c> does (WR-040, AC-03).
/// </summary>
public class ExtractRemoveAsyncTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public async Task Async_streams_match_the_sync_bytes()
    {
        var file = PdfFile.FromBytes(PdfOperationsExtractTests.Source(10));
        var ranges = PdfPageRange.Parse("9,2,2-3");

        using var extracted = new MemoryStream();
        await PdfOperations.ExtractAsync(file, ranges, extracted);
        using var removed = new MemoryStream();
        await PdfOperations.RemoveAsync(file, ranges, removed);

        Assert.Equal(PdfOperationsExtractTests.Extract(file, ranges), extracted.ToArray());
        Assert.Equal(PdfOperationsExtractTests.Remove(file, ranges), removed.ToArray());
    }

    [Fact]
    public async Task Async_paths_match_the_sync_bytes()
    {
        var file = PdfFile.FromBytes(PdfOperationsExtractTests.Source(6));
        var ranges = PdfPageRange.Parse("6-4");
        string path = Path.Combine(Path.GetTempPath(), $"extract-async-{Guid.NewGuid():N}.pdf");
        try
        {
            await PdfOperations.ExtractAsync(file, ranges, path);
            Assert.Equal(PdfOperationsExtractTests.Extract(file, ranges), await File.ReadAllBytesAsync(path));

            await PdfOperations.RemoveAsync(file, ranges, path);
            Assert.Equal(PdfOperationsExtractTests.Remove(file, ranges), await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_cancelled_token_cancels_the_write()
    {
        var file = PdfFile.FromBytes(PdfOperationsExtractTests.Source(3));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfOperations.ExtractAsync(file, PdfPageRange.Parse("1"), new MemoryStream(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfOperations.RemoveAsync(file, PdfPageRange.Parse("1"), new MemoryStream(), cancellation.Token));
    }

    [Fact]
    public void Invalid_arguments_fail_before_a_task_is_returned()
    {
        var file = PdfFile.FromBytes(PdfOperationsExtractTests.Source(3));

        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = PdfOperations.ExtractAsync(file, PdfPageRange.Parse("4"), new MemoryStream()); });
        Assert.Throws<ArgumentException>(() => { _ = PdfOperations.RemoveAsync(file, PdfPageRange.Parse("1-3"), new MemoryStream()); });
        Assert.Throws<ArgumentNullException>(() => { _ = PdfOperations.ExtractAsync(file, PdfPageRange.Parse("1"), (string)null!); });
    }

    #endregion

    #endregion
}
