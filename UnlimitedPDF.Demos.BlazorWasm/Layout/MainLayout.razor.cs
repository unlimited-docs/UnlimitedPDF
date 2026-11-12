using Microsoft.AspNetCore.Components;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Layout;

/// <summary>
/// The site layout: the top bar with the library version beside the logo, a "Skip to content" link as the first
/// focusable element, and the page's main content.
/// </summary>
public partial class MainLayout
{
    #region Methods

    #region Private Methods

    /// <summary>
    /// Moves keyboard focus past the top bar (and the playground sidebar) to the page's own content, so the next Tab
    /// press continues there.
    /// </summary>
    private async Task SkipToContentAsync() => await Interop.FocusSkipTargetAsync();

    #endregion

    #endregion

    #region Properties, Indexers

    [Inject] private BrowserInterop Interop { get; set; } = default!;

    #endregion
}
