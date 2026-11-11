using Microsoft.AspNetCore.Components;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// One question of the FAQ page with its answer, its code sample and demo link when it has them, and a button that
/// copies a link to the question.
/// </summary>
public partial class FaqQuestionItem
{
    #region Fields and Constants

    // The code samples' compiled source file, read once (PR-012: the page shows the code that compiles).
    private static readonly Lazy<string> _snippetSource = new(() => SampleSource.Read("FaqSnippets.cs"));

    private CopyState _copyState;
    private string? _snippet;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        _snippet = Question.Snippet is null ? null : SnippetText.Extract(_snippetSource.Value, Question.Snippet);

    #endregion

    #region Private Methods

    private async Task CopyLinkAsync()
    {
        var link = Navigation.ToAbsoluteUri($"faq#{Question.Id}").ToString();
        _copyState = await Interop.CopyTextAsync(link) ? CopyState.Copied : CopyState.Unavailable;
        StateHasChanged();
        await Task.Delay(2000);
        _copyState = CopyState.Idle;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets the navigation manager, used to build the question's full link.
    /// </summary>
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// Gets or sets the callback run when the question's button is pressed.
    /// </summary>
    [Parameter] public EventCallback OnToggle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the answer is shown.
    /// </summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>
    /// Gets or sets the question shown.
    /// </summary>
    [Parameter, EditorRequired] public FaqQuestion Question { get; set; } = default!;

    private string AnswerId => $"{Question.Id}-answer";

    private string? LinkStatus => _copyState switch
    {
        CopyState.Copied => "Link copied",
        CopyState.Unavailable => "Copy not available",
        _ => null
    };

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
