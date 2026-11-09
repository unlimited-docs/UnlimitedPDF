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

    [Fact]
    public async Task Async_part_whose_file_also_fails_to_close_is_removed_and_the_write_failure_reported()
    {
        // Review F-01, asynchronous path.
        var exception = await Assert.ThrowsAsync<PdfSplitException>(() => PdfOperations.SplitAsync(
            PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1, _root,
            path => Path.GetFileName(path) == "part-2.pdf"
                ? new FailingFileStream(path, failAfterBytes: 300, failOnDispose: true)
                : new FailingFileStream(path),
            CancellationToken.None));

        Assert.Equal(2, exception.PartNumber);
        Assert.Equal(1, exception.PartsWritten);
        Assert.Equal("Disk full.", Assert.IsType<IOException>(exception.InnerException).Message);
        Assert.Equal(new[] { "part-1.pdf" }, Directory.GetFiles(_root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Cancellation_raised_by_a_callback_without_the_token_is_a_part_failure()
    {
        // Review F-03: only the caller's own token cancels the split.
        var exception = await Assert.ThrowsAsync<PdfSplitException>(() => PdfOperations.SplitAsync(
            PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1,
            part => part.Number == 2 ? throw new TaskCanceledException("The upload timed out.") : new MemoryStream()));

        Assert.Equal(2, exception.PartNumber);
        Assert.Equal(1, exception.PartsWritten);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task Already_cancelled_token_creates_no_folder()
    {
        // Review F-05.
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        string folder = Path.Combine(_root, "never");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PdfOperations.SplitAsync(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)), 1, folder, cancellation.Token));

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task Async_writes_never_touch_a_stream_synchronously()
    {
        // Review F-02: streams such as web response bodies reject synchronous Write and Flush.
        var file = PdfFile.FromBytes(SplitFixtures.Bookmarked());
        var streams = new List<SplitTargetStream>();

        await PdfOperations.SplitAsync(file, 4, part => Track(streams, new SplitTargetStream(part.FileName) { AsyncOnly = true }));
        await PdfOperations.SplitAsync(file, new[] { new PdfPageRange(2, 3) }, part => Track(streams, new SplitTargetStream(part.FileName) { AsyncOnly = true }));
        // SaveAsync itself needs a stream that reports its position (a separate, known gap), so it is checked on a
        // seekable stream that still rejects synchronous I/O.
        using var saved = new AsyncOnlyMemoryStream();
        var document = new PdfDocument();
        document.AddPage().AddText("Saved asynchronously", 72, 720);
        await document.SaveAsync(saved);

        Assert.Equal(PdfOperationsSplitTests.SplitToMemory(file, 4).Select(p => p.Bytes), streams.Take(3).Select(s => s.ToArray()));
        Assert.All(streams, stream => PdfStructureChecker.AssertValid(stream.ToArray()));
        PdfStructureChecker.AssertValid(saved.ToArray());
    }

    [Fact]
    public async Task Cancelling_on_an_async_only_stream_is_reported_as_cancellation()
    {
        // Review F-02: unwinding a cancelled part must not flush synchronously and turn the cancellation into a failure.
        using var cancellation = new CancellationTokenSource();
        var streams = new List<SplitTargetStream>();

        var split = PdfOperations.SplitAsync(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1, part =>
        {
            var stream = new SplitTargetStream(part.FileName) { AsyncOnly = true, PauseAfterBytes = part.Number == 2 ? 100 : null };
            lock (streams) streams.Add(stream);
            return stream;
        }, cancellation.Token);
        await WaitUntil(() => { lock (streams) return streams.Count == 2; });
        await streams[1].Paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        streams[1].Resume.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => split);
        Assert.Equal(2, streams.Count);
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

    private static SplitTargetStream Track(List<SplitTargetStream> streams, SplitTargetStream stream)
    {
        streams.Add(stream);
        return stream;
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

    #region Nested Types

    // A memory stream that, like a web response body, rejects synchronous writes and flushes.
    private sealed class AsyncOnlyMemoryStream : MemoryStream
    {
        private bool _writingAsync;

        public override void Flush()
        {
            if (!_writingAsync) throw new InvalidOperationException("Synchronous operations are disallowed.");
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_writingAsync) throw new InvalidOperationException("Synchronous operations are disallowed.");
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!_writingAsync) throw new InvalidOperationException("Synchronous operations are disallowed.");
            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _writingAsync = true;
            try
            {
                base.Write(buffer.Span);
            }
            finally
            {
                _writingAsync = false;
            }
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
