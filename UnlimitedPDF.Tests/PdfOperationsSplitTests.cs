namespace UnlimitedPDF.Tests;

/// <summary>
/// <see cref="PdfOperations.Split(PdfFile, int, Func{PdfSplitPart, Stream})"/> and its overloads: every N pages or
/// one part per range, every argument checked before any output, one stream per part (WR-037, AC-01 to AC-04,
/// AC-06).
/// </summary>
public class PdfOperationsSplitTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Every_input_form_can_be_split()
    {
        var data = ReaderFixtureBuilder.DocumentBytes(10);
        string path = Path.Combine(Path.GetTempPath(), $"split-input-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, data);
        try
        {
            using var stream = new MemoryStream(data);
            foreach (var file in new[] { PdfFile.FromBytes(data), PdfFile.FromStream(stream), PdfFile.FromFile(path) })
            {
                var parts = SplitToMemory(file, 4);

                Assert.Equal(new[] { SplitFixtures.Texts(1, 4), SplitFixtures.Texts(5, 8), SplitFixtures.Texts(9, 10) },
                    parts.Select(p => SplitFixtures.PageTexts(p.Bytes)));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(1, "1-1 2-2 3-3 4-4 5-5 6-6 7-7 8-8 9-9 10-10")]
    [InlineData(3, "1-3 4-6 7-9 10-10")]
    [InlineData(4, "1-4 5-8 9-10")]
    [InlineData(10, "1-10")]
    [InlineData(25, "1-10")]
    [InlineData(int.MaxValue, "1-10")]
    public void Every_n_pages_gives_consecutive_parts_with_a_shorter_last_part(int pagesPerPart, string expected)
    {
        var parts = SplitToMemory(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10)), pagesPerPart);

        Assert.Equal(expected, string.Join(" ", parts.Select(p => $"{p.Part.FirstPage}-{p.Part.LastPage}")));
        Assert.Equal(Enumerable.Range(1, parts.Count), parts.Select(p => p.Part.Number));
        foreach (var (part, bytes) in parts)
        {
            PdfStructureChecker.AssertValid(bytes);
            Assert.Equal(SplitFixtures.Texts(part.FirstPage, part.LastPage), SplitFixtures.PageTexts(bytes));
        }
    }

    [Fact]
    public void Ranges_give_one_part_each_in_the_order_given_with_overlaps_and_gaps()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));

        var parts = SplitToMemory(file, new[] { new PdfPageRange(1, 3), new PdfPageRange(2, 5), new PdfPageRange(8, 10) });

        Assert.Equal(new[] { SplitFixtures.Texts(1, 3), SplitFixtures.Texts(2, 5), SplitFixtures.Texts(8, 10) },
            parts.Select(p => SplitFixtures.PageTexts(p.Bytes)));
        Assert.DoesNotContain(parts.SelectMany(p => SplitFixtures.PageTexts(p.Bytes)), text => text is "Page 6" or "Page 7");
        Assert.All(parts, p => PdfStructureChecker.AssertValid(p.Bytes));
    }

    [Fact]
    public void Ranges_keep_their_given_order_and_repeats()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));

        var reversed = SplitToMemory(file, new[] { new PdfPageRange(10, 10), new PdfPageRange(1, 1) });
        var repeated = SplitToMemory(file, new[] { new PdfPageRange(1, 3), new PdfPageRange(1, 3) });

        Assert.Equal(new[] { "Page 10", "Page 1" }, reversed.Select(p => Assert.Single(SplitFixtures.PageTexts(p.Bytes))));
        Assert.Equal(2, repeated.Count);
        Assert.Equal(repeated[0].Bytes, repeated[1].Bytes);
        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf" }, repeated.Select(p => p.Part.FileName));
    }

    [Fact]
    public void Invalid_arguments_fail_before_any_stream_is_requested()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        int calls = 0;
        Stream Open(PdfSplitPart part)
        {
            calls++;
            return new MemoryStream();
        }

        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(null!, 4, Open)).ParamName);
        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(null!, new[] { new PdfPageRange(1, 2) }, Open)).ParamName);
        Assert.Equal("pagesPerPart", Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Split(file, 0, Open)).ParamName);
        Assert.Equal("pagesPerPart", Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Split(file, -1, Open)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(file, (IEnumerable<PdfPageRange>)null!, Open)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentException>(() => PdfOperations.Split(file, Array.Empty<PdfPageRange>(), Open)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(file, new[] { new PdfPageRange(1, 2), null! }, Open)).ParamName);
        var past = Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Split(file, new[] { new PdfPageRange(1, 2), new PdfPageRange(9, 11) }, Open));
        Assert.Equal("ranges", past.ParamName);
        Assert.Contains("9-11", past.Message, StringComparison.Ordinal);
        Assert.Equal("openPart", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(file, 4, (Func<PdfSplitPart, Stream>)null!)).ParamName);
        Assert.Equal("openPart", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(file, new[] { new PdfPageRange(1, 2) }, (Func<PdfSplitPart, Stream>)null!)).ParamName);
        // The async overloads check their arguments before the task starts.
        Assert.Equal("pagesPerPart", Assert.Throws<ArgumentOutOfRangeException>(() => { _ = PdfOperations.SplitAsync(file, 0, Open); }).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentException>(() => { _ = PdfOperations.SplitAsync(file, Array.Empty<PdfPageRange>(), Open); }).ParamName);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Stream_callback_gets_each_part_and_streams_are_written_from_their_position_and_left_open()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        var prefix = Enumerable.Repeat((byte)'x', 100).ToArray();
        var seen = new List<(int Number, int First, int Last, string Name)>();
        var streams = new List<MemoryStream>();

        var parts = PdfOperations.Split(file, 4, part =>
        {
            seen.Add((part.Number, part.FirstPage, part.LastPage, part.FileName));
            var stream = new MemoryStream();
            stream.Write(prefix);
            streams.Add(stream);
            return stream;
        });

        Assert.Equal(new[] { (1, 1, 4, "part-1.pdf"), (2, 5, 8, "part-2.pdf"), (3, 9, 10, "part-3.pdf") }, seen);
        Assert.Equal(seen, parts.Select(p => (p.Number, p.FirstPage, p.LastPage, p.FileName)));
        foreach (var stream in streams)
        {
            Assert.True(stream.CanWrite);
            var bytes = stream.ToArray();
            Assert.Equal(prefix, bytes.Take(100));
            PdfStructureChecker.AssertValid(bytes.Skip(100).ToArray());
        }
    }

    [Fact]
    public void Non_seekable_streams_receive_valid_parts()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));
        var streams = new List<SplitTargetStream>();

        PdfOperations.Split(file, new[] { new PdfPageRange(2, 3) }, part =>
        {
            var stream = new SplitTargetStream(part.FileName);
            streams.Add(stream);
            return stream;
        });

        var target = Assert.Single(streams);
        Assert.False(target.IsDisposed);
        PdfStructureChecker.AssertValid(target.ToArray());
        Assert.Equal(SplitFixtures.Texts(2, 3), SplitFixtures.PageTexts(target.ToArray()));
    }

    [Fact]
    public void Parts_can_be_written_straight_into_zip_entries()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(5));
        using var zipBytes = new MemoryStream();

        using (var zip = new System.IO.Compression.ZipArchive(zipBytes, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entries = new List<Stream>();
            PdfOperations.Split(file, 2, part =>
            {
                foreach (var previous in entries) previous.Dispose();
                var entry = zip.CreateEntry(part.FileName).Open();
                entries.Add(entry);
                return entry;
            });
            foreach (var entry in entries) entry.Dispose();
        }

        zipBytes.Position = 0;
        using var read = new System.IO.Compression.ZipArchive(zipBytes, System.IO.Compression.ZipArchiveMode.Read);
        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf", "part-3.pdf" }, read.Entries.Select(e => e.FullName));
        foreach (var entry in read.Entries)
        {
            using var data = new MemoryStream();
            using (var stream = entry.Open()) stream.CopyTo(data);
            PdfStructureChecker.AssertValid(data.ToArray());
        }
    }

    #endregion

    #region Internal Methods

    // Splits into memory streams and returns each part with its bytes.
    internal static List<(PdfSplitPart Part, byte[] Bytes)> SplitToMemory(PdfFile file, int pagesPerPart)
    {
        var streams = new List<MemoryStream>();
        var parts = PdfOperations.Split(file, pagesPerPart, _ => Add(streams));
        return parts.Select((part, i) => (part, streams[i].ToArray())).ToList();
    }

    // Splits by ranges into memory streams and returns each part with its bytes.
    internal static List<(PdfSplitPart Part, byte[] Bytes)> SplitToMemory(PdfFile file, IEnumerable<PdfPageRange> ranges)
    {
        var streams = new List<MemoryStream>();
        var parts = PdfOperations.Split(file, ranges, _ => Add(streams));
        return parts.Select((part, i) => (part, streams[i].ToArray())).ToList();
    }

    #endregion

    #region Private Methods

    private static MemoryStream Add(List<MemoryStream> streams)
    {
        var stream = new MemoryStream();
        streams.Add(stream);
        return stream;
    }

    #endregion

    #endregion
}
