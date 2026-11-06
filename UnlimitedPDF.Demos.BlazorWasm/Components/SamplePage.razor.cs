using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// The page frame every playground demo shares: options with Generate, a Preview and C# code card, and the status
/// line. It generates the default PDF when the demo opens, runs one generation at a time, and keeps the last good
/// preview visible while a new one is built or when generation fails.
/// </summary>
public partial class SamplePage
{
    #region Fields and Constants

    private readonly string _id = $"demo-{Guid.NewGuid():N}";
    private string? _announcement;
    private bool _busy;
    private string? _error;
    private ElementReference _outputSection;
    private PdfPreview? _preview;
    private GenerationResult? _result;
    private string _source = "";
    private bool _stale;
    private OutputTab _tab = OutputTab.Preview;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Show the default PDF straight away; the visitor sees the result before touching anything.
            await GenerateAsync(userStarted: false);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => _source = SampleSource.Read(Demo.SourceFile);

    #endregion

    #region Public Methods

    /// <summary>
    /// Marks the shown PDF as out of date. Demos call this after changes that raise no input event, such as adding
    /// or removing a row.
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

    // The library writes one "/Type /Page" entry per page object and "/Type /Pages" for the page tree.
    private static int CountPages(byte[] pdf) =>
        System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.ASCII.GetString(pdf), @"/Type /Page\b").Count;

    private static string FormatSize(int bytes) =>
        bytes < 1024
            ? $"{bytes} B"
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} KB", bytes / 1024.0);

    private async Task DownloadAsync()
    {
        if (_preview is not null)
        {
            await _preview.DownloadAsync(Demo.FileName);
        }
    }

    private async Task GenerateAsync(bool userStarted)
    {
        if (_busy || _preview is null)
        {
            return;
        }

        _busy = true;
        _error = null;
        _announcement = "Generating the PDF.";
        StateHasChanged();

        // Let the browser paint the busy state before .NET starts working on the single UI thread.
        await Task.Delay(1);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var document = await Build();
            byte[] pdfBytes;
            using (var stream = new MemoryStream())
            {
                await document.SaveAsync(stream);
                pdfBytes = stream.ToArray();
            }

            await _preview.ShowAsync(pdfBytes);
            stopwatch.Stop();

            int pageCount = PageCount ?? CountPages(pdfBytes);
            _result = new GenerationResult(pageCount, pdfBytes.Length, stopwatch.ElapsedMilliseconds);
            _stale = false;
            _tab = OutputTab.Preview;
            _announcement = $"PDF ready: {pageCount} {(pageCount == 1 ? "page" : "pages")}, generated in {stopwatch.ElapsedMilliseconds} milliseconds.";
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            _announcement = $"Couldn't generate the PDF. {exception.Message}";
            // Generation errors are expected while options are being edited; the status line shows them, so they are
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
            // Commit a number still being typed (it binds on change), then generate.
            await Interop.BlurActiveElementAsync();
            await Task.Delay(1);
            await GenerateAsync(userStarted: true);
        }
    }

    private async Task OpenInNewTabAsync()
    {
        if (_preview is not null)
        {
            await _preview.OpenInNewTabAsync();
        }
    }

    private async Task ResetAsync()
    {
        await OnReset.InvokeAsync();
        NotifyInputsChanged();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the function that builds the demo's document from its current inputs.
    /// </summary>
    [Parameter, EditorRequired] public Func<Task<PdfDocument>> Build { get; set; } = default!;

    /// <summary>
    /// Gets or sets the demo shown on this page.
    /// </summary>
    [Parameter, EditorRequired] public SampleDemo Demo { get; set; } = default!;

    private bool HasPdf => _preview?.HasPdf == true;

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

    /// <summary>
    /// Gets or sets how many pages the current inputs produce, shown in the status line, or null to count the pages of
    /// the generated PDF (for demos whose page count depends on the content, such as a flowing table).
    /// </summary>
    [Parameter] public int? PageCount { get; set; } = 1;

    #endregion

    #region Nested Types

    private enum OutputTab
    {
        Preview,
        Code
    }

    private sealed record GenerationResult(int Pages, int Bytes, long Milliseconds);

    #endregion
}
