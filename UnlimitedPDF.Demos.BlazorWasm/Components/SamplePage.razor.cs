using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// The page frame every playground demo shares: options with Generate, and the output's two tabs, the drawn PDF with
/// its status line and the C# code as a complete console program. It generates the default PDF when the demo opens,
/// runs one generation at a time, and keeps the last good preview visible while a new one is built or when generation
/// fails.
/// </summary>
public partial class SamplePage
{
    #region Fields and Constants

    private readonly string _id = $"demo-{Guid.NewGuid():N}";
    private string? _announcement;
    private bool _busy;
    private bool _codeSelected;
    private CodeView? _codeView;
    private string? _error;
    private ElementReference _optionsSection;
    private ElementReference _outputSection;
    private PdfPreview? _preview;
    private GenerationResult? _result;
    private string _source = "";
    private bool _stale;

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
    protected override void OnParametersSet()
    {
        if ((Build is null) == (BuildFile is null))
            throw new InvalidOperationException($"{nameof(SamplePage)} needs exactly one of {nameof(Build)} and {nameof(BuildFile)}.");
        string program = SampleSource.Read(ShownProgramFile);
        _source = ShapeProgram is null ? program : ShapeProgram(program);
    }

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

    // Focus moves to the options first (without a jump), so the next Tab press continues there; then the page scrolls.
    private async Task BackToOptionsAsync()
    {
        await _optionsSection.FocusAsync(preventScroll: true);
        await Interop.ScrollToElementAsync($"{_id}-options", smooth: true);
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
            byte[] pdfBytes;
            if (BuildFile is not null)
            {
                pdfBytes = await BuildFile();
            }
            else
            {
                var document = await Build!();
                using var stream = new MemoryStream();
                await document.SaveAsync(stream);
                pdfBytes = stream.ToArray();
            }

            await _preview.ShowAsync(pdfBytes, ShownFileName);
            stopwatch.Stop();

            int pageCount = PageCount ?? CountPages(pdfBytes);
            _result = new GenerationResult(pageCount, pdfBytes.Length, stopwatch.ElapsedMilliseconds);
            _stale = false;
            _announcement = $"PDF ready: {pageCount} {(pageCount == 1 ? "page" : "pages")}, generated in {stopwatch.ElapsedMilliseconds} milliseconds.";
        }
        catch (Exception exception)
        {
            _error = ErrorText.Plain(exception);
            _announcement = null; // the error box is an alert and announces itself
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
            // The visitor asked for a PDF: show it, even if the code tab was open.
            _codeSelected = false;
            StateHasChanged();
            await Interop.ScrollIntoViewOnNarrowAsync(_outputSection);
        }
    }

    private void OnTabChanged(bool code) => _codeSelected = code;

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

    private async Task ResetAsync()
    {
        if (_busy) return;
        await OnReset.InvokeAsync();
        NotifyInputsChanged();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the function that builds the demo's document from its current inputs, which the page saves; a demo
    /// whose library call writes the PDF itself sets <see cref="BuildFile"/> instead.
    /// </summary>
    [Parameter] public Func<Task<PdfDocument>>? Build { get; set; }

    /// <summary>
    /// Gets or sets the function that writes the demo's PDF from its current inputs and returns its bytes (one-call
    /// operations such as extract and remove), or null when <see cref="Build"/> builds a document.
    /// </summary>
    [Parameter] public Func<Task<byte[]>>? BuildFile { get; set; }

    /// <summary>
    /// Gets or sets the choice among the demo's variants that a host page gives, shown above the options (a
    /// document type, a pack's domain); null when the demo has none.
    /// </summary>
    [CascadingParameter(Name = "DemoChoice")] public RenderFragment? Choice { get; set; }

    /// <summary>
    /// Gets or sets the language tag of the PDF's text when it isn't English, for the drawn pages' <c>lang</c>
    /// (WCAG 3.1.2), or null.
    /// </summary>
    [Parameter] public string? ContentLanguage { get; set; }

    /// <summary>
    /// Gets or sets the demo shown on this page.
    /// </summary>
    [Parameter, EditorRequired] public SampleDemo Demo { get; set; } = default!;

    /// <summary>
    /// Gets or sets the PDF's file name, or null for the demo's own (<see cref="SampleDemo.FileName"/>).
    /// </summary>
    [Parameter] public string? FileName { get; set; }

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

    private string ShownFileName => FileName ?? Demo.FileName;

    // The complete console program the code tab shows: the page's own, else the one for the builder file shown.
    private string ShownProgramFile => ProgramFile ?? SampleDemo.ProgramFileOf(SourceFile ?? Demo.SourceFile);

    /// <summary>
    /// Gets or sets the program file the code tab shows when it is not the one named after the builder file (see
    /// <see cref="SampleDemo.ProgramFileOf"/>), for example a page whose defaults differ from the demo it shares a
    /// builder with; null for the builder's program.
    /// </summary>
    [Parameter] public string? ProgramFile { get; set; }

    /// <summary>
    /// Gets or sets whether the code tab wraps long lines, for a program holding text the visitor edits (HTML), whose
    /// lines can be of any length. The programs themselves keep to 100 characters a line.
    /// </summary>
    [Parameter] public bool WrapCode { get; set; }

    /// <summary>
    /// Gets or sets a change the page makes to the program before it is shown, such as putting in the HTML being
    /// edited; null to show the program as it is.
    /// </summary>
    [Parameter] public Func<string, string>? ShapeProgram { get; set; }

    /// <summary>
    /// Gets or sets the sample builder file of the way the PDF is currently built, or null for the demo's own
    /// (<see cref="SampleDemo.SourceFile"/>); the code tab shows its program (<see cref="SampleDemo.ProgramFileOf"/>).
    /// A demo with more than one way of building its PDF sets the file of the way currently chosen.
    /// </summary>
    [Parameter] public string? SourceFile { get; set; }

    #endregion

    #region Nested Types

    private sealed record GenerationResult(int Pages, int Bytes, long Milliseconds);

    #endregion
}
