using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// The site-wide search box (D-024): the top bar has one on wide screens and the phone menu has another. Results are
/// found in the app itself (<see cref="SiteSearch"/>), with no request to a server.
/// </summary>
public partial class SiteSearchBox : IAsyncDisposable
{
    #region Fields and Constants

    private bool _closed;
    private DotNetObjectReference<SiteSearchBox>? _listener;
    private string _query = string.Empty;
    private IReadOnlyList<SearchResult> _results = [];
    private ElementReference _root;
    private string _status = string.Empty;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _listener = DotNetObjectReference.Create(this);
        await Interop.InitSiteSearchAsync(_root, _listener);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Hides the results and keeps the query, as Escape and leaving the search do. Called from <c>js/app.js</c>.
    /// </summary>
    [JSInvokable]
    public void CloseResults()
    {
        if (_closed) return;
        _closed = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Interop.DisposeSiteSearchAsync(_root);
        _listener?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Methods

    private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

    private static string GroupName(SearchKind kind) => kind switch
    {
        SearchKind.Demo => "Scenarios",
        SearchKind.Faq => "FAQ answers",
        _ => "Code",
    };

    // Following a result opens another page: the box empties for the next search.
    private void Clear()
    {
        _query = string.Empty;
        _results = [];
        _status = string.Empty;
    }

    private void OnInput(ChangeEventArgs args)
    {
        _query = args.Value?.ToString() ?? string.Empty;
        _closed = false;
        _results = SiteSearch.Find(_query, SampleSource.Read);
        _status = SiteSearch.Words(_query).Length == 0 ? string.Empty
            : _results.Count == 0 ? "No results."
            : $"{Count(_results.Count(r => r.Kind == SearchKind.Demo), "scenario", "scenarios")}, "
              + $"{Count(_results.Count(r => r.Kind == SearchKind.Faq), "FAQ answer", "FAQ answers")}, "
              + $"{Count(_results.Count(r => r.Kind == SearchKind.Code), "code line", "code lines")}.";
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the search field's element id, unique on the page.
    /// </summary>
    [Parameter, EditorRequired] public string InputId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the Ctrl K hint shows in the box (on wide screens, where a keyboard is likely).
    /// </summary>
    [Parameter] public bool ShowShortcut { get; set; }

    private string HintId => InputId + "-hint";

    [Inject] private BrowserInterop Interop { get; set; } = default!;

    private bool IsOpen => !_closed && SiteSearch.Words(_query).Length > 0;

    private string ResultsId => InputId + "-results";

    #endregion
}
