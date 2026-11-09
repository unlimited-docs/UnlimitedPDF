namespace UnlimitedPDF.Tests;

/// <summary>
/// A part that cannot be written stops the split with <see cref="PdfSplitException"/> naming the part and the parts
/// written; finished parts are kept, a half-written part file is removed, caller streams are never closed, and no
/// later part is started (WR-037, AC-10, A08, A10).
/// </summary>
public sealed class SplitFailureTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"split-fail-{Guid.NewGuid():N}");

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Folder_part_that_cannot_be_opened_stops_the_split_and_keeps_earlier_parts()
    {
        Directory.CreateDirectory(Path.Combine(_root, "part-2.pdf"));

        var exception = Assert.Throws<PdfSplitException>(() =>
            PdfOperations.Split(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1, _root));

        Assert.Equal(2, exception.PartNumber);
        Assert.Equal(1, exception.PartsWritten);
        Assert.NotNull(exception.InnerException);
        PdfStructureChecker.AssertValid(File.ReadAllBytes(Path.Combine(_root, "part-1.pdf")));
        Assert.True(Directory.Exists(Path.Combine(_root, "part-2.pdf")), "The blocking folder is never deleted.");
        Assert.False(File.Exists(Path.Combine(_root, "part-3.pdf")));
    }

    [Fact]
    public void Half_written_folder_part_is_removed_and_earlier_parts_kept()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "keep");
        var opened = new List<string>();

        var exception = Assert.Throws<PdfSplitException>(() => PdfOperations.Split(
            PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3)), 1, _root,
            path =>
            {
                opened.Add(Path.GetFileName(path));
                return new FailingFileStream(path, failAfterBytes: Path.GetFileName(path) == "part-2.pdf" ? 300 : null);
            }));

        Assert.Equal(2, exception.PartNumber);
        Assert.Equal(1, exception.PartsWritten);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf" }, opened);
        Assert.Equal(new[] { "notes.txt", "part-1.pdf" }, Directory.GetFiles(_root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        PdfStructureChecker.AssertValid(File.ReadAllBytes(Path.Combine(_root, "part-1.pdf")));
    }

    [Fact]
    public void Throwing_stream_callback_stops_at_its_part()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        var cause = new UnauthorizedAccessException("No access.");
        var streams = new List<MemoryStream>();
        int calls = 0;

        var exception = Assert.Throws<PdfSplitException>(() => PdfOperations.Split(file, 2, part =>
        {
            calls++;
            if (part.Number == 3) throw cause;
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        }));

        Assert.Equal(3, exception.PartNumber);
        Assert.Equal(2, exception.PartsWritten);
        Assert.Same(cause, exception.InnerException);
        Assert.Contains("Part 3 of 5", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, calls);
        Assert.All(streams, stream =>
        {
            Assert.True(stream.CanWrite);
            PdfStructureChecker.AssertValid(stream.ToArray());
        });
    }

    [Fact]
    public void Stream_that_fails_mid_write_stops_at_its_part_and_stays_open()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(6));
        var streams = new List<SplitTargetStream>();

        var exception = Assert.Throws<PdfSplitException>(() => PdfOperations.Split(file, 2, part =>
        {
            var stream = new SplitTargetStream(part.FileName) { FailAfterBytes = part.Number == 2 ? 200 : null };
            streams.Add(stream);
            return stream;
        }));

        Assert.Equal(2, exception.PartNumber);
        Assert.Equal(1, exception.PartsWritten);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => Assert.False(stream.IsDisposed));
        PdfStructureChecker.AssertValid(streams[0].ToArray());
    }

    [Fact]
    public void Null_stream_from_the_callback_is_a_part_failure()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2));

        var exception = Assert.Throws<PdfSplitException>(() => PdfOperations.Split(file, 1, _ => null!));

        Assert.Equal(1, exception.PartNumber);
        Assert.Equal(0, exception.PartsWritten);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    #endregion

    #endregion
}
