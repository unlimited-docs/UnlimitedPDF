using Microsoft.AspNetCore.Components;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Shows a sample's C# source with a Copy button that confirms the copy for two seconds.
/// </summary>
public partial class CodeView
{
    #region Fields and Constants

    private CopyState _copyState;

    #endregion

    #region Methods

    #region Private Methods

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

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

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
