using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Demos.BlazorWasm.Layout;

/// <summary>
/// The playground layout: a breadcrumb to the scenario gallery and the current demo's capability, above the demo.
/// </summary>
public partial class PlaygroundLayout
{
    #region Fields and Constants

    private SampleCategory? _category;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        _category = CategoryOf(Navigation.Uri);
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    #endregion

    #region Private Methods

    // The capability of the demo at an address: its path is a demo's route, or a route without its query.
    private SampleCategory? CategoryOf(string uri)
    {
        string address = Navigation.ToBaseRelativePath(uri).Split('#')[0];
        string path = address.Split('?')[0];
        var demo = SampleCatalog.Pages.FirstOrDefault(d => string.Equals(d.Route, address, StringComparison.OrdinalIgnoreCase))
            ?? SampleCatalog.Pages.FirstOrDefault(d => string.Equals(d.Route, path, StringComparison.OrdinalIgnoreCase));
        return demo is null ? null : ScenarioGallery.CategoryOf(demo);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _category = CategoryOf(args.Location);
        _ = InvokeAsync(StateHasChanged);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    #endregion
}
