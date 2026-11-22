using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF.Demos.BlazorWasm.Services;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Demos.BlazorWasm.Components.Tools;

/// <summary>
/// The step a tool page shows.
/// </summary>
public enum ToolPageStep
{
    /// <summary>
    /// The landing: title, the large upload button, the privacy and limits lines, the how-to, and the code.
    /// </summary>
    Landing,

    /// <summary>
    /// The workspace: the visitor's files or pages, with the action bar.
    /// </summary>
    Workspace,

    /// <summary>
    /// The done screen: the download, the preview, and the code that did it.
    /// </summary>
    Done,
}

/// <summary>
/// What every free tool page shares (26112201 AC-04): the three steps on one address, the hidden file input the large
/// button and a drop anywhere on the page feed, the run with its busy state and message, and the downloads. Files are
/// read and processed only in the browser (AC-10).
/// </summary>
public abstract class ToolPageBase : ComponentBase, IAsyncDisposable
{
    #region Fields and Constants

    private readonly Dictionary<ToolPdf, string> _urls = [];
    private string? _zipUrl;
    private bool _dropReady;
    private bool _focusStep;

    #endregion

    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeToolAsync();
        await RevokeUrlsAsync();
        if (FileInput?.Element is { } input) await Interop.DisposePageDropAsync(input);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Opens the file picker.
    /// </summary>
    /// <returns>A task that completes when the picker is asked to open.</returns>
    protected async Task ChooseFilesAsync()
    {
        if (FileInput?.Element is { } input) await Interop.OpenFilePickerAsync(input);
    }

    /// <summary>
    /// Releases what a tool page holds besides the run's result, for example its pictures' browser URLs.
    /// </summary>
    /// <returns>A task that completes when it is released.</returns>
    protected virtual ValueTask DisposeToolAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Downloads one PDF the run made, under its file name.
    /// </summary>
    /// <param name="pdf">The PDF.</param>
    /// <returns>A task that completes when the download starts.</returns>
    protected async Task DownloadAsync(ToolPdf pdf) => await Interop.DownloadAsync(await UrlAsync(pdf), pdf.FileName);

    /// <summary>
    /// Downloads every PDF the run made as one zip.
    /// </summary>
    /// <param name="fileName">The zip's file name.</param>
    /// <returns>A task that completes when the download starts.</returns>
    protected async Task DownloadAllAsync(string fileName)
    {
        if (Result is null) return;
        var previous = _zipUrl;
        _zipUrl = await Interop.CreateObjectUrlAsync(ZipBuilder.Zip(Result.Files.Select(f => (f.FileName, f.Bytes))), "application/zip");
        await Interop.DownloadAsync(_zipUrl, fileName);
        await Interop.RevokeObjectUrlAsync(previous);
    }

    /// <summary>
    /// Moves the page to a step and moves the keyboard focus to that step's heading.
    /// </summary>
    /// <param name="step">The step.</param>
    protected void GoTo(ToolPageStep step)
    {
        Step = step;
        RunError = null;
        _focusStep = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_dropReady && FileInput?.Element is { } input)
        {
            _dropReady = true;
            await Interop.InitPageDropAsync(input);
        }

        if (_focusStep)
        {
            _focusStep = false;
            await Interop.FocusHeadingAsync(HeadingId);
        }
    }

    /// <summary>
    /// Reads the PDFs the visitor chose or dropped into <paramref name="set"/>, each checked against the room left before
    /// it is read; a refused file adds its message to <see cref="FileErrors"/> and the others go on (AC-08, AC-10).
    /// </summary>
    /// <param name="args">The chosen files.</param>
    /// <param name="set">The files so far.</param>
    /// <param name="replace">Whether a new file replaces the one held, for a tool that takes one PDF.</param>
    /// <returns>A task that completes when every file is read.</returns>
    protected async Task AddPdfsAsync(InputFileChangeEventArgs args, ToolFileSet<PdfFile> set, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(set);
        FileErrors.Clear();
        RunError = null;
        Busy = true;
        StateHasChanged();
        try
        {
            var chosen = args.GetMultipleFiles(Math.Max(1, args.FileCount));
            if (replace && chosen.Count > 1) FileErrors.Add("This tool takes one PDF at a time, so the first one was used.");
            foreach (var browserFile in replace ? chosen.Take(1) : chosen)
            {
                if ((replace ? null : set.CheckRoomFor(browserFile.Size)) is { } room)
                {
                    FileErrors.Add($"{browserFile.Name}: {room}");
                    continue;
                }

                var (file, error) = await UploadedPdfLoader.LoadAsync(browserFile, ToolLimits.MaxPdfBytes);
                if (file is null)
                {
                    FileErrors.Add($"{browserFile.Name}: {error}");
                    continue;
                }

                if (replace) set.Clear();
                set.Add(browserFile.Name, browserFile.Size, file);
            }
        }
        finally
        {
            Busy = false;
        }

        if (set.Count > 0 && Step == ToolPageStep.Landing) GoTo(ToolPageStep.Workspace);
    }

    /// <summary>
    /// Saves a sample document and loads it back as a PDF the visitor might have added.
    /// </summary>
    /// <param name="set">The files so far.</param>
    /// <param name="name">The sample's file name.</param>
    /// <param name="document">The sample document.</param>
    protected static void AddSample(ToolFileSet<PdfFile> set, string name, PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        document.Save(stream);
        byte[] bytes = stream.ToArray();
        set.Add(name, bytes.LongLength, PdfFile.FromBytes(bytes));
    }

    /// <summary>
    /// Returns a file size for a card or a summary, for example "412 KB" or "1.7 MB".
    /// </summary>
    /// <param name="bytes">The size, in bytes.</param>
    /// <returns>The size in words.</returns>
    protected static string SizeText(long bytes) =>
        bytes >= 1024 * 1024
            ? (bytes / (1024.0 * 1024)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB"
            : Math.Max(1, (bytes + 1023) / 1024).ToString(System.Globalization.CultureInfo.InvariantCulture) + " KB";

    /// <summary>
    /// Returns a count with its noun, for example "1 page" or "3 pages".
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="noun">The noun, singular.</param>
    /// <returns>The words.</returns>
    protected static string CountText(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} {noun}s";

    /// <summary>
    /// Opens one PDF the run made in a new browser tab.
    /// </summary>
    /// <param name="pdf">The PDF.</param>
    /// <returns>A task that completes when the tab is asked to open.</returns>
    protected async Task OpenAsync(ToolPdf pdf) => await Interop.OpenInNewTabAsync(await UrlAsync(pdf));

    /// <summary>
    /// Runs the tool: shows the busy state, then the done screen, or the message in the action bar with the workspace kept.
    /// </summary>
    /// <param name="run">The work.</param>
    /// <returns>A task that completes when the run ends.</returns>
    protected async Task RunAsync(Func<Task<ToolResult>> run)
    {
        if (Busy) return;
        Busy = true;
        RunError = null;
        StateHasChanged();
        // Lets the browser paint the busy state before the work, which holds the page's only thread.
        await Task.Delay(30);
        try
        {
            var result = await run();
            if (!result.Succeeded)
            {
                RunError = result.Error ?? "This didn't work. Try again.";
                return;
            }

            await RevokeUrlsAsync();
            Result = result;
            GoTo(ToolPageStep.Done);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Drops the run's result and its browser URLs.
    /// </summary>
    /// <returns>A task that completes when the URLs are released.</returns>
    protected async Task ClearResultAsync()
    {
        await RevokeUrlsAsync();
        Result = null;
    }

    #endregion

    #region Private Methods

    private async Task RevokeUrlsAsync()
    {
        foreach (var url in _urls.Values) await Interop.RevokeObjectUrlAsync(url);
        _urls.Clear();
        await Interop.RevokeObjectUrlAsync(_zipUrl);
        _zipUrl = null;
    }

    private async Task<string> UrlAsync(ToolPdf pdf)
    {
        if (!_urls.TryGetValue(pdf, out var url))
        {
            url = await Interop.CreateObjectUrlAsync(pdf.Bytes, "application/pdf");
            _urls[pdf] = url;
        }

        return url;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether a run or a file read is under way.
    /// </summary>
    protected bool Busy { get; set; }

    /// <summary>
    /// Gets the messages for files that were refused, one per file; the other files stay.
    /// </summary>
    protected List<string> FileErrors { get; } = [];

    /// <summary>
    /// Gets or sets the hidden file input the large button and page drops feed.
    /// </summary>
    protected InputFile? FileInput { get; set; }

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] protected BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets the last run's result.
    /// </summary>
    protected ToolResult? Result { get; private set; }

    /// <summary>
    /// Gets or sets the message of a run that failed, shown in the action bar.
    /// </summary>
    protected string? RunError { get; set; }

    /// <summary>
    /// Gets or sets the step shown.
    /// </summary>
    protected ToolPageStep Step { get; set; } = ToolPageStep.Landing;

    /// <summary>
    /// Gets the id every step gives its heading, which takes the focus when the step changes.
    /// </summary>
    protected static string HeadingId => "tool-heading";

    #endregion
}
