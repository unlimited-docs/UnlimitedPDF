using System.Diagnostics;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Tests that change the process-wide HTML limits run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ProcessWideHtmlSettingsCollection
{
    public const string Name = "Process-wide HTML settings";
}

/// <summary>
/// HTML loads from a string, file, or stream, sync and async; resources load only from data: URIs, inside the base
/// folder, or through the resolver; limits fail before output; expected failures are warnings and others propagate
/// (WR-038, PR-018, AC-03, AC-14, AC-15, AC-19, AC-20, D-020).
/// </summary>
[Collection(ProcessWideHtmlSettingsCollection.Name)]
public sealed class PdfHtmlLoadingTests : IDisposable
{
    #region Fields and Constants

    private readonly string _folder;

    #endregion

    #region Constructors and Finalizers

    public PdfHtmlLoadingTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "updf-html-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_folder, "site", "img"));
        File.WriteAllBytes(Path.Combine(_folder, "site", "img", "logo.png"), Png());
        File.WriteAllText(Path.Combine(_folder, "site", "site.css"), "p { color: red }");
        File.WriteAllBytes(Path.Combine(_folder, "secret.png"), Png());
    }

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Strings_files_and_streams_load_the_same_content()
    {
        const string html = "<p>Café</p>";
        string path = Path.Combine(_folder, "site", "page.html");
        File.WriteAllText(path, html, new UTF8Encoding(false));

        foreach (var loaded in new[]
        {
            PdfHtml.FromString(html),
            PdfHtml.FromFile(path),
            PdfHtml.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(html))),
            await PdfHtml.FromStringAsync(html),
            await PdfHtml.FromFileAsync(path),
            await PdfHtml.FromStreamAsync(new MemoryStream(Encoding.UTF8.GetBytes(html)))
        })
        {
            Assert.Equal("Café", loaded.Model.Tree.Body.TextContent());
            Assert.Empty(loaded.Warnings);
        }
    }

    [Fact]
    public void Streams_are_read_from_their_position_and_left_open()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("IGNORED<p>x</p>"));
        stream.Position = 7;
        var html = PdfHtml.FromStream(stream);
        Assert.Equal("x", html.Model.Tree.Body.TextContent());
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Null_arguments_and_missing_files_throw_standard_exceptions()
    {
        Assert.Equal("html", Assert.Throws<ArgumentNullException>(() => PdfHtml.FromString(null!)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => PdfHtml.FromFile(null!)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => PdfHtml.FromStream(null!)).ParamName);
        Assert.Throws<FileNotFoundException>(() => PdfHtml.FromFile(Path.Combine(_folder, "missing.html")));
    }

    [Fact]
    public void A_file_path_sets_the_base_folder_for_images_and_stylesheets()
    {
        string path = Path.Combine(_folder, "site", "page.html");
        File.WriteAllText(path, "<link rel=stylesheet href=site.css><p>x</p><img src='img/logo.png'><img src='img/logo.png?v=2#top'>");
        var html = PdfHtml.FromFile(path);
        Assert.Empty(html.Warnings);
        Assert.Equal(2, html.Model.Images.Count);
        var p = html.Model.Styles.Single(pair => pair.Key.Name == "p").Value;
        Assert.Equal(1, p.Color.R);
    }

    [Theory]
    [InlineData("../secret.png")]
    [InlineData("img/../../secret.png")]
    [InlineData("%2e%2e/secret.png")]
    [InlineData("..%2Fsecret.png")]
    [InlineData("..\\secret.png")]
    [InlineData("/secret.png")]
    [InlineData("\\\\server\\share\\x.png")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("file:///C:/Windows/win.ini")]
    public void Paths_outside_the_base_folder_are_blocked(string src)
    {
        var html = PdfHtml.FromString($"<img src=\"{src}\" alt=x>", new PdfHtmlLoadOptions { BaseFolder = Path.Combine(_folder, "site") });
        var warning = Assert.Single(html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.ResourceBlocked, warning.Kind);
        Assert.Empty(html.Model.Images);
    }

    [Fact]
    public void Paths_through_a_symbolic_link_or_junction_are_blocked()
    {
        string link = Path.Combine(_folder, "site", "linked");
        if (!TryCreateDirectoryLink(link, _folder)) return; // the OS does not allow creating links here

        var html = PdfHtml.FromString("<img src='linked/secret.png'>", new PdfHtmlLoadOptions { BaseFolder = Path.Combine(_folder, "site") });
        Assert.Equal(PdfHtmlWarningKind.ResourceBlocked, Assert.Single(html.Warnings).Kind);
    }

    [Fact]
    public void Missing_files_inside_the_base_folder_are_not_found()
    {
        var html = PdfHtml.FromString("<img src='img/none.png'>", new PdfHtmlLoadOptions { BaseFolder = Path.Combine(_folder, "site") });
        var warning = Assert.Single(html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.ResourceNotFound, warning.Kind);
        Assert.Equal("img/none.png", warning.Source);
        Assert.Equal(1, warning.Line);
    }

    [Fact]
    public void Without_a_base_folder_no_file_is_read_and_references_go_to_the_resolver()
    {
        var asked = new List<string>();
        var html = PdfHtml.FromString("<img src='img/logo.png'><img src='https://cdn.example.com/a.png'><img src='//cdn/b.png'>", new PdfHtmlLoadOptions
        {
            ResourceResolver = reference => { asked.Add(reference); return reference.StartsWith("https", StringComparison.Ordinal) ? new MemoryStream(Png()) : null; }
        });
        Assert.Equal(new[] { "img/logo.png", "https://cdn.example.com/a.png", "//cdn/b.png" }, asked);
        Assert.Single(html.Model.Images);
        Assert.Equal(new[] { PdfHtmlWarningKind.ResourceNotResolved, PdfHtmlWarningKind.ResourceNotResolved }, html.Warnings.Select(w => w.Kind));
    }

    [Fact]
    public void Without_a_resolver_other_references_are_not_resolved()
    {
        var html = PdfHtml.FromString("<img src='https://example.com/x.png'><link rel=stylesheet href='http://example.com/s.css'>");
        Assert.All(html.Warnings, w => Assert.Equal(PdfHtmlWarningKind.ResourceNotResolved, w.Kind));
        Assert.Equal(2, html.Warnings.Count);
    }

    [Fact]
    public async Task Async_loads_use_the_async_resolver_and_dispose_returned_streams()
    {
        var returned = new TrackingStream(Png());
        var html = await PdfHtml.FromStringAsync("<img src='cdn:logo'>", new PdfHtmlLoadOptions
        {
            ResourceResolverAsync = (reference, token) => Task.FromResult<Stream?>(returned)
        });
        Assert.Single(html.Model.Images);
        Assert.True(returned.Disposed);
    }

    [Fact]
    public void A_sync_load_with_only_an_async_resolver_throws_before_reading()
    {
        Assert.Throws<InvalidOperationException>(() => PdfHtml.FromString("<p>x</p>", new PdfHtmlLoadOptions
        {
            ResourceResolverAsync = (reference, token) => Task.FromResult<Stream?>(null)
        }));
    }

    [Theory]
    [InlineData("data:image/png;base64,")]
    [InlineData("DATA:image/png;base64,")]
    public void Data_uris_load_in_base64_and_percent_forms(string prefix)
    {
        string base64 = Convert.ToBase64String(Png());
        var html = PdfHtml.FromString($"<img src=\"{prefix}{base64}\"><link rel=stylesheet href='data:text/css,p%20%7B%20color%3A%20red%20%7D'><p>x</p>");
        Assert.Empty(html.Warnings);
        Assert.Single(html.Model.Images);
        Assert.Equal(1, html.Model.Styles.Single(pair => pair.Key.Name == "p").Value.Color.R);
    }

    [Fact]
    public void Unsupported_and_damaged_images_are_warnings()
    {
        string gif = "data:image/gif;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes("GIF89a......"));
        string broken = "data:image/png;base64," + Convert.ToBase64String(Png().Take(30).ToArray());
        var html = PdfHtml.FromString($"<img src='{gif}' alt=g><img src='{broken}' alt=b>");
        Assert.Equal(new[] { PdfHtmlWarningKind.UnsupportedImageFormat, PdfHtmlWarningKind.InvalidImage }, html.Warnings.Select(w => w.Kind));
        Assert.Empty(html.Model.Images);
    }

    [Fact]
    public void Resolver_exceptions_and_cancellation_propagate()
    {
        var thrown = new InvalidOperationException("resolver bug");
        var ex = Assert.Throws<InvalidOperationException>(() => PdfHtml.FromString("<img src='cdn:y'>", new PdfHtmlLoadOptions { ResourceResolver = _ => throw thrown }));
        Assert.Same(thrown, ex);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfHtml.FromStringAsync("<img src='cdn:y'>", new PdfHtmlLoadOptions
        {
            ResourceResolverAsync = (r, t) => Task.FromResult<Stream?>(null)
        }, cancelled.Token).GetAwaiter().GetResult());
    }

    [Fact]
    public void Strict_mode_throws_the_first_load_warning()
    {
        var ex = Assert.Throws<PdfHtmlException>(() => PdfHtml.FromString("<p style='float: left'>x</p>", new PdfHtmlLoadOptions { FailOnWarning = true }));
        Assert.Equal(PdfHtmlErrorReason.Warning, ex.Reason);
        Assert.Equal(PdfHtmlWarningKind.UnsupportedProperty, ex.Warning!.Kind);
    }

    [Fact]
    public void Input_over_the_limit_fails_for_every_input_form()
    {
        var options = new PdfHtmlLoadOptions { MaxInputSize = 10 };
        const string html = "<p>more than ten bytes</p>";
        string path = Path.Combine(_folder, "big.html");
        File.WriteAllText(path, html);
        foreach (var load in new Func<PdfHtml>[]
        {
            () => PdfHtml.FromString(html, options),
            () => PdfHtml.FromFile(path, options),
            () => PdfHtml.FromStream(new NonSeekableStream(Encoding.UTF8.GetBytes(html)), options),
            () => PdfHtml.FromStreamAsync(new MemoryStream(Encoding.UTF8.GetBytes(html)), options).GetAwaiter().GetResult()
        })
        {
            Assert.Equal(PdfHtmlErrorReason.InputTooLarge, Assert.Throws<PdfHtmlException>(load).Reason);
        }
    }

    [Fact]
    public void Resource_limits_fail_per_resource_and_in_total()
    {
        string png = "data:image/png;base64," + Convert.ToBase64String(Png());
        int size = Png().Length;
        var one = Assert.Throws<PdfHtmlException>(() => PdfHtml.FromString($"<img src='{png}'>", new PdfHtmlLoadOptions { MaxResourceSize = size - 1 }));
        Assert.Equal(PdfHtmlErrorReason.ResourceTooLarge, one.Reason);

        // The same reference twice is loaded once; two different references count twice.
        PdfHtml.FromString($"<img src='{png}'><img src='{png}'>", new PdfHtmlLoadOptions { MaxTotalResourceSize = size });
        string other = "data:image/png;base64," + Convert.ToBase64String(Png(4));
        var total = Assert.Throws<PdfHtmlException>(() => PdfHtml.FromString($"<img src='{png}'><img src='{other}'>", new PdfHtmlLoadOptions { MaxTotalResourceSize = size + 10 }));
        Assert.Equal(PdfHtmlErrorReason.ResourcesTooLarge, total.Reason);

        var streamed = Assert.Throws<PdfHtmlException>(() => PdfHtml.FromString("<img src='cdn:y'>", new PdfHtmlLoadOptions
        {
            MaxResourceSize = 100,
            ResourceResolver = _ => new NonSeekableStream(new byte[1000])
        }));
        Assert.Equal(PdfHtmlErrorReason.ResourceTooLarge, streamed.Reason);
    }

    [Fact]
    public void Process_wide_limits_have_defaults_and_per_call_values_win()
    {
        Assert.Equal(52_428_800, PdfHtml.DefaultMaxInputSize);
        Assert.Equal(256, PdfHtml.DefaultMaxNestingDepth);
        Assert.Equal(52_428_800, PdfHtml.DefaultMaxResourceSize);
        Assert.Equal(209_715_200, PdfHtml.DefaultMaxTotalResourceSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfHtml.DefaultMaxNestingDepth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfHtmlLoadOptions { MaxInputSize = 0 });

        int original = PdfHtml.DefaultMaxNestingDepth;
        try
        {
            PdfHtml.DefaultMaxNestingDepth = 5;
            string deep = string.Concat(Enumerable.Repeat("<div>", 10));
            Assert.Equal(PdfHtmlErrorReason.NestingTooDeep, Assert.Throws<PdfHtmlException>(() => PdfHtml.FromString(deep)).Reason);
            PdfHtml.FromString(deep, new PdfHtmlLoadOptions { MaxNestingDepth = 50 });
        }
        finally
        {
            PdfHtml.DefaultMaxNestingDepth = original;
        }
    }

    #endregion

    #region Internal Methods

    internal static byte[] Png(int size = 5) => new PngBuilder { ColorType = 6, Width = size, Height = size }.Build();

    #endregion

    #region Private Methods

    private static bool TryCreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
        if (!OperatingSystem.IsWindows()) return false;
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
        process!.WaitForExit();
        return Directory.Exists(link);
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed class TrackingStream : MemoryStream
    {
        public TrackingStream(byte[] data) : base(data) { }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    #endregion
}
