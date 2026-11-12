using Microsoft.AspNetCore.Components;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Sets the page's meta description, the summary search results show under the page title.
/// </summary>
public partial class PageDescription
{
    #region Fields and Constants

    private string? _written;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (string.Equals(_written, Text, StringComparison.Ordinal))
        {
            return;
        }

        _written = Text;
        await Browser.SetDescriptionAsync(Text);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    [Inject] private BrowserInterop Browser { get; set; } = default!;

    /// <summary>
    /// Gets or sets the description: one or two sentences, about 160 characters or fewer, saying what the page offers.
    /// </summary>
    [Parameter, EditorRequired] public string Text { get; set; } = "";

    #endregion
}
