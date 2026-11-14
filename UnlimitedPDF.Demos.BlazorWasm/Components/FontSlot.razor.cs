using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// One font slot of the Fonts and languages demo (D-026, D-028, D-030): the script's name, whether the chosen invoice
/// needs it, a link to a free font for it, and an upload field. A font that cannot be loaded leaves the slot's previous
/// font in place and says why; a font whose license restricts embedding can be loaded once the visitor ticks the rights
/// box. Fonts are read in the browser and never uploaded.
/// </summary>
public partial class FontSlot
{
    #region Fields and Constants

    private bool _covers = true;
    private UploadedFont? _coversChecked;
    private string? _error;
    private bool _reading;
    private byte[]? _restrictedBytes;
    private string _restrictedName = "";

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The letter check runs once per font, not on every render.
        if (!ReferenceEquals(Value, _coversChecked))
        {
            _coversChecked = Value;
            _covers = Value is null || FontSlots.Covers(Value.Font, Script);
        }
    }

    #endregion

    #region Private Methods

    private static string Describe(PdfFont font) => font.IsVariable
        ? string.Format(CultureInfo.InvariantCulture, "variable, weights {0} to {1}", font.MinimumWeight, font.MaximumWeight)
        : string.Format(CultureInfo.InvariantCulture, "weight {0}{1}", font.Weight, font.IsItalic ? ", italic" : "");

    private async Task AssertRightsAsync(ChangeEventArgs args)
    {
        if (args.Value is not true || _restrictedBytes is null) return;
        var result = UploadedFontLoader.Load(_restrictedName, _restrictedBytes, assertRights: true);
        _restrictedBytes = null;
        _error = result.Error;
        if (result.Font is not null) await ValueChanged.InvokeAsync(result.Font);
    }

    private async Task OnFileChosenAsync(InputFileChangeEventArgs args)
    {
        _error = null;
        _restrictedBytes = null;
        _reading = true;
        try
        {
            var (bytes, readError) = await UploadedFontLoader.ReadAsync(args.File);
            if (bytes is null)
            {
                _error = readError;
                return;
            }

            var result = UploadedFontLoader.Load(args.File.Name, bytes);
            if (result.Font is not null)
            {
                await ValueChanged.InvokeAsync(result.Font);
                return;
            }

            _error = result.Error;
            if (result.CanAssertRights)
            {
                _restrictedBytes = bytes;
                _restrictedName = args.File.Name;
            }
        }
        finally
        {
            _reading = false;
        }
    }

    private async Task RemoveAsync()
    {
        _error = null;
        _restrictedBytes = null;
        await ValueChanged.InvokeAsync(null);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether the chosen invoice has text in this slot's script.
    /// </summary>
    [Parameter] public bool Needed { get; set; }

    /// <summary>
    /// Gets or sets the slot's script.
    /// </summary>
    [Parameter, EditorRequired] public FontScript Script { get; set; }

    /// <summary>
    /// Gets or sets the slot's loaded font, or null when it is empty.
    /// </summary>
    [Parameter] public UploadedFont? Value { get; set; }

    /// <summary>
    /// Gets or sets the callback for a newly loaded or removed font.
    /// </summary>
    [Parameter] public EventCallback<UploadedFont?> ValueChanged { get; set; }

    #endregion
}
