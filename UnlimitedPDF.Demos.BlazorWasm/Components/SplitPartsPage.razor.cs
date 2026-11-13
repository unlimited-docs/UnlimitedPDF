using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// The page frame of the Split demos (WR-037): options with Split, a list of the parts with a preview and a download
/// for each, all parts as one zip, the C# code, and the status line. It splits the default file when the demo opens,
/// runs one split at a time, and keeps the last good parts visible while a new split runs or when it fails.
/// </summary>
public partial class SplitPartsPage : IAsyncDisposable
{
    #region Fields and Constants

    private readonly string _id = $"demo-{Guid.NewGuid():N}";
    private string? _announcement;
    private bool _busy;
    private string? _error;
    private ElementReference _optionsSection;
    private ElementReference _outputSection;
    private IReadOnlyList<SplitPartFile> _parts = Array.Empty<SplitPartFile>();
    private PdfPreview? _preview;
    private SplitResult? _result;
    private int _selected;
    private string _source = "";
    private bool _stale;
    private OutputTab _tab = OutputTab.Preview;
    private string? _zipUrl;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Show the default parts straight away; the visitor sees the result before touching anything.
            await SplitAsync(userStarted: false);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => _source = SampleSource.Read(Demo.SourceFile);

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Interop.RevokeObjectUrlAsync(_zipUrl);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Marks the shown parts as out of date. Demos call this after changes that raise no input event, such as a new
    /// file being loaded.
    /// </summary>
    public void NotifyInputsChanged()
    {
        if (_result is not null)
        {
            _stale = true;
            StateHasChanged();
        }
    }

    #endregion

    #region Private Methods

    private static string FormatSize(int bytes) =>
        bytes < 1024
            ? $"{bytes} B"
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} KB", bytes / 1024.0);

    private static string PagesText(PdfSplitPart part) =>
        part.FirstPage == part.LastPage
            ? $"page {part.FirstPage}"
            : $"pages {part.FirstPage}-{part.LastPage}";

    // Focus moves to the options first (without a jump), so the next Tab press continues there; then the page scrolls.
    private async Task BackToOptionsAsync()
    {
        await _optionsSection.FocusAsync(preventScroll: true);
        await Interop.ScrollToElementAsync($"{_id}-options", smooth: true);
    }

    private async Task DownloadPartAsync(int index)
    {
        if (_preview is null || index >= _parts.Count)
        {
            return;
        }

        // The preview holds the part's file, so the download uses the same browser URL.
        if (index != _selected)
        {
            await SelectAsync(index);
        }
        await _preview.DownloadAsync(_parts[index].Part.FileName);
    }

    private async Task DownloadZipAsync()
    {
        if (_parts.Count == 0)
        {
            return;
        }

        var previous = _zipUrl;
        _zipUrl = await Interop.CreateObjectUrlAsync(SplitPartFile.Zip(_parts), "application/zip");
        await Interop.DownloadAsync(_zipUrl, Demo.FileName);
        await Interop.RevokeObjectUrlAsync(previous);
    }

    private void OnInputsChanged(ChangeEventArgs _)
    {
        if (_result is not null)
        {
            _stale = true;
        }
    }

    private async Task OnInputsKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && (args.CtrlKey || args.MetaKey))
        {
            // Commit a number still being typed (it binds on change), then split.
            await Interop.BlurActiveElementAsync();
            await Task.Delay(1);
            await SplitAsync(userStarted: true);
        }
    }

    private async Task ResetAsync()
    {
        await OnReset.InvokeAsync();
        NotifyInputsChanged();
    }

    private async Task SelectAsync(int index)
    {
        if (_preview is null || index >= _parts.Count)
        {
            return;
        }

        _selected = index;
        await _preview.ShowAsync(_parts[index].Bytes, _parts[index].Part.FileName);
        _announcement = $"Showing {_parts[index].Part.FileName}.";
        StateHasChanged();
    }

    private async Task SplitAsync(bool userStarted)
    {
        if (_busy || _preview is null)
        {
            return;
        }

        _busy = true;
        _error = null;
        _announcement = "Splitting the PDF.";
        StateHasChanged();

        // Let the browser paint the busy state before .NET starts working on the single UI thread.
        await Task.Delay(1);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var parts = await Build();
            stopwatch.Stop();

            _parts = parts;
            _selected = 0;
            await _preview.ShowAsync(parts[0].Bytes, parts[0].Part.FileName);

            _result = new SplitResult(parts.Count, parts.Sum(part => part.Bytes.Length), stopwatch.ElapsedMilliseconds);
            _stale = false;
            _tab = OutputTab.Preview;
            _announcement = $"Split into {parts.Count} {(parts.Count == 1 ? "part" : "parts")} in {stopwatch.ElapsedMilliseconds} milliseconds.";
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            _announcement = $"Couldn't split the PDF. {exception.Message}";
            // Split errors are expected while options are being edited; the status line shows them, so they are
            // logged without the app's error banner.
            Console.WriteLine(exception);
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }

        if (userStarted && _error is null)
        {
            await Interop.ScrollIntoViewOnNarrowAsync(_outputSection);
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the function that splits the demo's file from its current inputs.
    /// </summary>
    [Parameter, EditorRequired] public Func<Task<IReadOnlyList<SplitPartFile>>> Build { get; set; } = default!;

    /// <summary>
    /// Gets or sets the demo shown on this page; its file name names the zip download.
    /// </summary>
    [Parameter, EditorRequired] public SampleDemo Demo { get; set; } = default!;

    /// <summary>
    /// Gets or sets the demo's option fields.
    /// </summary>
    [Parameter] public RenderFragment? Inputs { get; set; }

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets the callback that restores the demo's default inputs.
    /// </summary>
    [Parameter] public EventCallback OnReset { get; set; }

    #endregion

    #region Nested Types

    private enum OutputTab
    {
        Preview,
        Code
    }

    private sealed record SplitResult(int Parts, int Bytes, long Milliseconds);

    #endregion
}
