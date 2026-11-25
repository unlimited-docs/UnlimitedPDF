using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Shows a sample's C# source with a Copy button that confirms the copy for two seconds. With
/// <see cref="FullScreenTitle"/> set it can also show the code full screen, in a modal dialog over the whole window.
/// </summary>
public partial class CodeView : IDisposable
{
    #region Fields and Constants

    private readonly string _titleId = $"code-{Guid.NewGuid():N}-title";
    private CopyState _copyState;
    private ElementReference _dialog;
    private bool _fullScreen;
    private DotNetObjectReference<CodeView>? _listener;

    #endregion

    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public void Dispose()
    {
        _listener?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Called by the browser when the full screen dialog closes, however it closed (Close, Escape).
    /// </summary>
    [JSInvokable]
    public void FullScreenClosed()
    {
        _fullScreen = false;
        StateHasChanged();
    }

    /// <summary>
    /// Shows the code full screen. Does nothing unless <see cref="FullScreenTitle"/> is set.
    /// </summary>
    public async Task OpenFullScreenAsync()
    {
        if (FullScreenTitle is null)
        {
            return;
        }

        // The code is drawn in the dialog only while it is open, so the page holds one copy of a long program.
        _fullScreen = true;
        StateHasChanged();
        await Task.Yield();
        _listener ??= DotNetObjectReference.Create(this);
        await Interop.ShowDialogAsync(_dialog, _listener, nameof(FullScreenClosed));
    }

    #endregion

    #region Private Methods

    private async Task CloseFullScreenAsync() => await Interop.CloseDialogAsync(_dialog);

    private async Task CopyAsync()
    {
        _copyState = await Interop.CopyTextAsync(Source) ? CopyState.Copied : CopyState.Unavailable;
        StateHasChanged();
        await Task.Delay(2000);
        _copyState = CopyState.Idle;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the accessible name of the code block.
    /// </summary>
    [Parameter] public string AriaLabel { get; set; } = "Sample code";

    private string CopyLabel => _copyState switch
    {
        CopyState.Copied => "Copied",
        CopyState.Unavailable => "Copy not available",
        _ => "Copy code"
    };

    /// <summary>
    /// Gets or sets a short line shown beside the title in full screen, or null for none.
    /// </summary>
    [Parameter] public string? FullScreenNote { get; set; }

    /// <summary>
    /// Gets or sets the heading shown over the code in full screen, for example "Invoice · Program.cs"; null (the
    /// default) when the code is not shown full screen.
    /// </summary>
    [Parameter] public string? FullScreenTitle { get; set; }

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets whether long lines wrap instead of scrolling sideways, for text whose lines the page doesn't
    /// control, such as HTML being edited. Copy still copies the text as it is.
    /// </summary>
    [Parameter] public bool WrapLines { get; set; }

    /// <summary>
    /// Gets or sets the source text to show.
    /// </summary>
    [Parameter, EditorRequired] public string Source { get; set; } = "";

    #endregion

    #region Nested Types

    private enum CopyState
    {
        Idle,
        Copied,
        Unavailable
    }

    #endregion
}
