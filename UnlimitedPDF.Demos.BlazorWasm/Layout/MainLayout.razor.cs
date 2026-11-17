using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Layout;

/// <summary>
/// The site layout: a "Skip to content" link as the first focusable element, the top bar (logo and library version,
/// site-wide search, main links, and Install), the phone menu drawer, the page's main content, and the footer.
/// </summary>
public partial class MainLayout : IDisposable
{
    #region Fields and Constants

    /// <summary>
    /// The library's GitHub repository.
    /// </summary>
    private const string GitHubUrl = "https://github.com/unlimited-docs/UnlimitedPDF";

    /// <summary>
    /// The library's NuGet package page.
    /// </summary>
    private const string NuGetUrl = "https://www.nuget.org/packages/UnlimitedPDF";

    private DotNetObjectReference<MainLayout>? _listener;
    private ElementReference _menu;
    private bool _menuOpen;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _listener?.Dispose();
    }

    /// <summary>
    /// Notes that the menu drawer closed, however it closed (Close, Escape, or a link). Called from <c>js/app.js</c>.
    /// </summary>
    /// <param name="opening">Unused here: the dialog helper reports it for dialogs opened more than once at a time.</param>
    [JSInvokable]
    public void MenuClosed(int opening) => _menuOpen = false;

    #endregion

    #region Private Methods

    private async Task CloseMenuAsync() => await Interop.CloseDialogAsync(_menu);

    // A link in the drawer opens another page: the drawer closes so the page shows.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_menuOpen) _ = InvokeAsync(CloseMenuForPageAsync);
    }

    // The drawer returns focus to Menu as it closes; the new page's content takes it instead.
    private async Task CloseMenuForPageAsync()
    {
        await CloseMenuAsync();
        await Interop.FocusSkipTargetAsync();
    }

    private async Task OpenMenuAsync()
    {
        _listener ??= DotNetObjectReference.Create(this);
        _menuOpen = true;
        await Interop.ShowDialogAsync(_menu, _listener, nameof(MenuClosed), 0);
    }

    /// <summary>
    /// Moves keyboard focus past the top bar (and the playground sidebar) to the page's own content, so the next Tab
    /// press continues there.
    /// </summary>
    private async Task SkipToContentAsync() => await Interop.FocusSkipTargetAsync();

    #endregion

    #endregion

    #region Properties, Indexers

    [Inject] private BrowserInterop Interop { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    #endregion
}
