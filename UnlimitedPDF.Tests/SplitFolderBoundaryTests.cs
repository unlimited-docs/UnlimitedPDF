namespace UnlimitedPDF.Tests;

/// <summary>
/// Folder output writes only generated <c>&lt;base&gt;-&lt;number&gt;.pdf</c> names straight inside the caller's
/// folder, creates the folder when missing, overwrites its own names, and never touches other files (WR-037, AC-05,
/// A01, A05).
/// </summary>
public sealed class SplitFolderBoundaryTests : IDisposable
{
    #region Fields and Constants

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"split-{Guid.NewGuid():N}");

    #endregion

    #region Constructors and Finalizers

    public SplitFolderBoundaryTests() => Directory.CreateDirectory(_root);

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(3, "report-1.pdf report-2.pdf report-3.pdf")]
    [InlineData(9, "report-1.pdf report-2.pdf report-3.pdf report-4.pdf report-5.pdf report-6.pdf report-7.pdf report-8.pdf report-9.pdf")]
    [InlineData(10, "report-01.pdf report-02.pdf report-03.pdf report-04.pdf report-05.pdf report-06.pdf report-07.pdf report-08.pdf report-09.pdf report-10.pdf")]
    public void Names_carry_the_source_name_and_a_padded_part_number(int pages, string expected)
    {
        var file = PdfFile.FromFile(Source("report.pdf", pages));
        string folder = Path.Combine(_root, "out");

        var parts = PdfOperations.Split(file, 1, folder);

        Assert.Equal(expected, string.Join(" ", parts.Select(p => p.FileName)));
        Assert.Equal(expected.Split(' '), Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(parts, part => PdfStructureChecker.AssertValid(File.ReadAllBytes(Path.Combine(folder, part.FileName))));
    }

    [Fact]
    public void Twelve_parts_are_padded_to_two_digits()
    {
        var parts = PdfOperations.Split(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(12)), 1, Path.Combine(_root, "out"));

        Assert.Equal("part-01.pdf", parts[0].FileName);
        Assert.Equal("part-12.pdf", parts[11].FileName);
    }

    [Fact]
    public void Names_are_the_same_in_any_culture()
    {
        using var culture = new CultureScope("ar-SA");

        var parts = PdfOperations.Split(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10)), 1, Path.Combine(_root, "out"));

        Assert.Equal("part-10.pdf", parts[9].FileName);
    }

    [Fact]
    public void Missing_nested_folder_is_created()
    {
        string folder = Path.Combine(_root, "a", "b", "c");

        PdfOperations.Split(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)), 1, folder);

        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf" }, Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Existing_part_name_is_overwritten_and_other_files_are_untouched()
    {
        string folder = Path.Combine(_root, "out");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "part-2.pdf"), "old");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "keep");
        File.WriteAllText(Path.Combine(folder, "part-3.pdf"), "keep too");

        PdfOperations.Split(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)), 1, folder);

        PdfStructureChecker.AssertValid(File.ReadAllBytes(Path.Combine(folder, "part-2.pdf")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(folder, "notes.txt")));
        Assert.Equal("keep too", File.ReadAllText(Path.Combine(folder, "part-3.pdf")));
        Assert.Equal(4, Directory.GetFiles(folder).Length);
        Assert.Empty(Directory.GetDirectories(folder));
    }

    [Theory]
    [InlineData("a.b.pdf", "a.b-1.pdf")]
    [InlineData("report", "report-1.pdf")]
    [InlineData("..pdf", "part-1.pdf")]
    public void Names_never_leave_the_folder(string sourceName, string expected)
    {
        string folder = Path.Combine(_root, "out");

        var parts = PdfOperations.Split(PdfFile.FromFile(Source(sourceName, 1)), 1, folder);

        var part = Assert.Single(parts);
        Assert.Equal(expected, part.FileName);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, part.FileName);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, part.FileName);
        Assert.NotEqual("..", Path.GetFileNameWithoutExtension(part.FileName));
        Assert.True(File.Exists(Path.Combine(folder, expected)));
        Assert.Single(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public void Stream_and_byte_loads_use_part_as_the_base_name()
    {
        using var stream = new MemoryStream(ReaderFixtureBuilder.DocumentBytes(2));

        var fromStream = PdfOperations.Split(PdfFile.FromStream(stream), 1, Path.Combine(_root, "s"));
        var fromBytes = PdfOperations.Split(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)), 1, Path.Combine(_root, "b"));

        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf" }, fromStream.Select(p => p.FileName));
        Assert.Equal(new[] { "part-1.pdf", "part-2.pdf" }, fromBytes.Select(p => p.FileName));
    }

    [Fact]
    public void Invalid_arguments_fail_before_the_folder_is_created()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(3));
        string folder = Path.Combine(_root, "never");

        Assert.Equal("pagesPerPart", Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Split(file, 0, folder)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentOutOfRangeException>(() => PdfOperations.Split(file, new[] { new PdfPageRange(2, 4) }, folder)).ParamName);
        Assert.Equal("ranges", Assert.Throws<ArgumentException>(() => PdfOperations.Split(file, Array.Empty<PdfPageRange>(), folder)).ParamName);
        Assert.Equal("outputFolder", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(file, 1, (string)null!)).ParamName);
        Assert.Equal("outputFolder", Assert.Throws<ArgumentException>(() => PdfOperations.Split(file, 1, "  ")).ParamName);
        Assert.Equal("outputFolder", Assert.Throws<ArgumentException>(() => { _ = PdfOperations.SplitAsync(file, 1, ""); }).ParamName);
        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => PdfOperations.Split(null!, 1, folder)).ParamName);

        Assert.False(Directory.Exists(folder));
    }

    #endregion

    #region Private Methods

    private string Source(string name, int pages)
    {
        string folder = Path.Combine(_root, "in");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        File.WriteAllBytes(path, ReaderFixtureBuilder.DocumentBytes(pages));
        return path;
    }

    #endregion

    #endregion
}
