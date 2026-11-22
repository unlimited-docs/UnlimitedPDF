namespace UnlimitedPDF.Demos.BlazorWasm.Components.Tools;

/// <summary>
/// What picking a page in a page grid means.
/// </summary>
public enum PageGridMode
{
    /// <summary>
    /// Picked pages are kept; they are ticked.
    /// </summary>
    Keep,

    /// <summary>
    /// Picked pages are removed; they are crossed out.
    /// </summary>
    Remove,

    /// <summary>
    /// Pages aren't picked; the cuts between parts are marked.
    /// </summary>
    Cut,
}
