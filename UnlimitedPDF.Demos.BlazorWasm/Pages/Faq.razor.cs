using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Pages;

/// <summary>
/// The FAQ page: topics with an index, a search that filters the questions as you type, answers that open and close,
/// and a link to each question that opens it.
/// </summary>
public partial class Faq
{
    #region Fields and Constants

    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private readonly ElementReference[] _sections = new ElementReference[FaqQuestions.Groups.Count];
    private string? _currentGroup;
    private DotNetObjectReference<Faq>? _listener;
    private string _query = "";
    private bool _refreshSections;
    private ElementReference _searchBox;
    private string? _scrollTo;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (OpenLinkedQuestion(Navigation.Uri)) StateHasChanged();

            _listener = DotNetObjectReference.Create(this);
            await Interop.ObserveSectionsAsync(_sections, _listener, nameof(SetCurrentGroup));
            return;
        }

        if (_scrollTo is { } id)
        {
            _scrollTo = null;
            await Interop.ScrollToElementAsync(id, smooth: false);
        }

        if (_refreshSections)
        {
            _refreshSections = false;
            await Interop.RefreshSectionsAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        try
        {
            await Interop.StopObservingSectionsAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is closing; nothing to stop.
        }

        _listener?.Dispose();
    }

    /// <summary>
    /// Marks the topic in view in the index; called by the browser module.
    /// </summary>
    /// <param name="groupId">The topic's ID.</param>
    [JSInvokable]
    public void SetCurrentGroup(string groupId)
    {
        _currentGroup = groupId;
        StateHasChanged();
    }

    #endregion

    #region Private Methods

    private async Task ClearSearchAsync()
    {
        _query = "";
        _refreshSections = true;
        await _searchBox.FocusAsync();
    }

    private bool HasMatch(FaqGroup group) => group.Questions.Any(Matches);

    private bool Matches(FaqQuestion question) => FaqSearch.Matches(question, _query);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        // A question link followed while the page is open (only the fragment changes).
        if (OpenLinkedQuestion(args.Location)) _ = InvokeAsync(StateHasChanged);
    }

    private void OnSearch(ChangeEventArgs args)
    {
        _query = args.Value?.ToString() ?? "";
        _refreshSections = true;
    }

    // A link to a question (faq#<id>) opens its answer and brings it into view; any other fragment opens nothing.
    private bool OpenLinkedQuestion(string uri)
    {
        var fragment = new Uri(uri).Fragment.TrimStart('#');
        if (!FaqQuestions.Questions.Any(q => q.Id == fragment)) return false;

        _open.Add(fragment);
        _scrollTo = fragment;
        return true;
    }

    private void Toggle(string questionId)
    {
        if (!_open.Remove(questionId)) _open.Add(questionId);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets the navigation manager, used to read a link's question.
    /// </summary>
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string CountText
    {
        get
        {
            int total = FaqQuestions.Questions.Count();
            if (FaqSearch.Words(_query).Length == 0) return $"{total} questions";
            return $"{MatchCount} of {total} questions";
        }
    }

    private int MatchCount => FaqQuestions.Questions.Count(Matches);

    #endregion
}
