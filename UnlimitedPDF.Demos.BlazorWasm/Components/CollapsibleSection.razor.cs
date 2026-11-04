using Microsoft.AspNetCore.Components;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// A titled group of demo options that the user can collapse and expand.
/// </summary>
public partial class CollapsibleSection
{
    #region Fields and Constants

    private readonly string _bodyId = $"section-{Guid.NewGuid():N}";
    private bool _open = true;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override void OnInitialized() => _open = InitiallyOpen;

    #endregion

    #region Private Methods

    private void Toggle() => _open = !_open;

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the section content.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the section starts expanded. The default is <see langword="true"/>.
    /// </summary>
    [Parameter] public bool InitiallyOpen { get; set; } = true;

    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    [Parameter, EditorRequired] public string Title { get; set; } = "";

    #endregion
}
