using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// The tabs over a demo's output: the result and the C# code. The tab a visitor chooses is kept in the browser, so a
/// developer reading code stays on the code tab from demo to demo.
/// </summary>
public partial class DemoTabs
{
    #region Fields and Constants

    /// <summary>
    /// The browser setting that keeps the tab last chosen: "code" or "output".
    /// </summary>
    public const string SettingKey = "unlimitedpdf.demo-tab";

    private ElementReference _codeTab;
    private ElementReference _outputTab;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && await Interop.ReadSettingAsync(SettingKey) == "code" && !CodeSelected)
        {
            await CodeSelectedChanged.InvokeAsync(true);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>Returns the id of the code panel for an id prefix.</summary>
    /// <param name="id">The id prefix given as <see cref="Id"/>.</param>
    public static string CodePanelIdOf(string id) => $"{id}-code-panel";

    /// <summary>Returns the id of the code tab for an id prefix.</summary>
    /// <param name="id">The id prefix given as <see cref="Id"/>.</param>
    public static string CodeTabIdOf(string id) => $"{id}-code-tab";

    /// <summary>Returns the id of the result panel for an id prefix.</summary>
    /// <param name="id">The id prefix given as <see cref="Id"/>.</param>
    public static string OutputPanelIdOf(string id) => $"{id}-output-panel";

    /// <summary>Returns the id of the result tab for an id prefix.</summary>
    /// <param name="id">The id prefix given as <see cref="Id"/>.</param>
    public static string OutputTabIdOf(string id) => $"{id}-output-tab";

    #endregion

    #region Private Methods

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        bool? code = args.Key switch
        {
            "ArrowLeft" or "ArrowRight" => !CodeSelected,
            "Home" => false,
            "End" => true,
            _ => null
        };
        if (code is { } value)
        {
            await SelectAsync(value);
            await (value ? _codeTab : _outputTab).FocusAsync();
        }
    }

    private async Task SelectAsync(bool code)
    {
        await Interop.WriteSettingAsync(SettingKey, code ? "code" : "output");
        if (code != CodeSelected)
        {
            await CodeSelectedChanged.InvokeAsync(code);
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the accessible name of the tab list.
    /// </summary>
    [Parameter] public string AriaLabel { get; set; } = "Result and code";

    /// <summary>
    /// Gets the id of the code panel the code tab controls.
    /// </summary>
    public string CodePanelId => CodePanelIdOf(Id);

    /// <summary>
    /// Gets or sets whether the code tab is the one shown.
    /// </summary>
    [Parameter] public bool CodeSelected { get; set; }

    /// <summary>
    /// Gets or sets what runs when the visitor chooses the other tab, with true for the code tab.
    /// </summary>
    [Parameter] public EventCallback<bool> CodeSelectedChanged { get; set; }

    /// <summary>
    /// Gets the id of the code tab.
    /// </summary>
    public string CodeTabId => CodeTabIdOf(Id);

    /// <summary>
    /// Gets or sets the id prefix shared by the tabs and their panels.
    /// </summary>
    [Parameter, EditorRequired] public string Id { get; set; } = "";

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets the result tab's label, for example "PDF".
    /// </summary>
    [Parameter] public string OutputLabel { get; set; } = "PDF";

    /// <summary>
    /// Gets the id of the result panel the result tab controls.
    /// </summary>
    public string OutputPanelId => OutputPanelIdOf(Id);

    /// <summary>
    /// Gets the id of the result tab.
    /// </summary>
    public string OutputTabId => OutputTabIdOf(Id);

    #endregion
}
