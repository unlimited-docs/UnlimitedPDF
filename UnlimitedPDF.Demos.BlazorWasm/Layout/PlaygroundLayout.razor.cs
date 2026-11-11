using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Layout;

/// <summary>
/// The playground layout: a sidebar grouped by capability beside the current demo. On small screens the sidebar
/// collapses behind a menu button. The current demo's link is kept in view in the sidebar (and in the open menu),
/// whether the demo was opened from the sidebar, a link, or a reload.
/// </summary>
public partial class PlaygroundLayout
{
    #region Fields and Constants

    private bool _menuOpen;

    private bool _revealActive = true;

    private ElementReference _sidebar;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_revealActive)
        {
            return;
        }

        _revealActive = false;
        try
        {
            await Interop.RevealActiveLinkAsync(_sidebar);
        }
        catch (JSDisconnectedException)
        {
            // The page is closing; there is no sidebar to scroll.
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    #endregion

    #region Private Methods

    private void CloseMenu() => _menuOpen = false;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        // Another demo is current: once it has rendered, bring its link into view.
        _revealActive = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private void ToggleMenu()
    {
        _menuOpen = !_menuOpen;
        _revealActive = _menuOpen;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    [Inject] private BrowserInterop Interop { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    #endregion
}
