namespace UnlimitedPDF.Tests;

/// <summary>
/// The asynchronous split overloads write the same bytes as the synchronous ones, one part at a time, and cancel
/// cleanly: finished parts kept, the part being written removed (WR-037, AC-07, AC-10, CS-007, A06).
/// </summary>
public sealed class SplitAsyncTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"split-async-{Guid.NewGuid():N}");

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Async_overloads_write_the_same_parts_as_sync()
    {
        var file = PdfFile.FromBytes(SplitFixtures.Bookmarked());
        var ranges = new[] { new PdfPageRange(1, 3), new PdfPageRange(5, 8) };

        var syncEvery = PdfOperationsSplitTests.SplitToMemory(file, 4).Select(p => p.Bytes).ToList();
        var syncRanges = PdfOperationsSplitTests.SplitToMemory(file, ranges).Select(p => p.Bytes).ToList();
        var asyncEvery = new List<MemoryStream>();
        var asyncRanges = new List<MemoryStream>();
        await PdfOperations.SplitAsync(file, 4, _ => Add(asyncEvery));
        await PdfOperations.SplitAsync(file, ranges, _ => Add(asyncRanges));
        await PdfOperations.SplitAsync(file, 4, Path.Combine(_root, "every"));
        await PdfOperations.SplitAsync(file, ranges, Path.Combine(_root, "ranges"));

        Assert.Equal(syncEvery, asyncEvery.Select(s => s.ToArray()));
        Assert.Equal(syncRanges, asyncRanges.Select(s => s.ToArray()));
        Assert.Equal(syncEvery, new[] { "part-1.pdf", "part-2.pdf", "part-3.pdf" }.Select(n => File.ReadAllBytes(Path.Combine(_root, "every", n))));
        Assert.Equal(syncRanges, new[] { "part-1.pdf", "part-2.pdf" }.Select(n => File.ReadAllBytes(Path.Combine(_root, "ranges", n))));
    }

    [Fact]
    public async Task Each_part_is_requested_only_after_the_previous_part_is_written()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(6));
        var syncLog = new List<string>();
        var asyncLog = new List<string>();

        PdfOperations.Split(file, 2, part => Open(part, syncLog));
        await PdfOperations.SplitAsync(file, 2, part => Open(part, asyncLog));

        var expected = new[] { "open 1", "write part-1.pdf", "open 2", "write part-2.pdf", "open 3", "write part-3.pdf" };
        Assert.Equal(expected, syncLog);
        Assert.Equal(expected, asyncLog);
    }

    [Fact]
    public async Task Cancelling_a_folder_split_keeps_finished_parts_and_removes_the_part_being_written()
    {
        using var cancellation = new CancellationTokenSource();
        FailingFileStream? second = null;

        var split = PdfOperations.SplitAsync(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1, _root, path =>
        {
            bool isSecond = Path.GetFileName(path) == "part-2.pdf";
            var stream = new FailingFileStream(path, pauseAfterBytes: isSecond ? 100 : null);
            if (isSecond) second = stream;
            return stream;
        }, cancellation.Token);
        await WaitUntil(() => second is not null);
        await second!.Paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        second.Resume.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => split);
        Assert.Equal(new[] { "part-1.pdf" }, Directory.GetFiles(_root).Select(Path.GetFileName));
        PdfStructureChecker.AssertValid(File.ReadAllBytes(Path.Combine(_root, "part-1.pdf")));
    }

    [Fact]
    public async Task Cancelling_a_stream_split_leaves_streams_open_and_requests_no_later_part()
    {
        using var cancellation = new CancellationTokenSource();
        var streams = new List<SplitTargetStream>();

        var split = PdfOperations.SplitAsync(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1, part =>
        {
            var stream = new SplitTargetStream(part.FileName) { PauseAfterBytes = part.Number == 2 ? 100 : null };
            lock (streams) streams.Add(stream);
            return stream;
        }, cancellation.Token);
        await WaitUntil(() => { lock (streams) return streams.Count == 2; });
        await streams[1].Paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        streams[1].Resume.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => split);
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => Assert.False(stream.IsDisposed));
        PdfStructureChecker.AssertValid(streams[0].ToArray());
    }

    [Fact]
    public async Task Already_cancelled_token_writes_nothing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PdfOperations.SplitAsync(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)), 1, _ => { calls++; return new MemoryStream(); }, cancellation.Token));

        Assert.Equal(0, calls);
    }

    #endregion

    #region Private Methods

    private static MemoryStream Add(List<MemoryStream> streams)
    {
        var stream = new MemoryStream();
        streams.Add(stream);
        return stream;
    }

    private static Stream Open(PdfSplitPart part, List<string> log)
    {
        log.Add("open " + part.Number.ToString(CultureInfo.InvariantCulture));
        return new SplitTargetStream(part.FileName, log);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The split did not reach the expected part.");
            await Task.Delay(5);
        }
    }

    #endregion

    #endregion
}
