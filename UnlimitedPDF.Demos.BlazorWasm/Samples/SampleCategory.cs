namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// A group of playground demos, shown as one sidebar section, for example "Create".
/// </summary>
/// <param name="Title">The group name.</param>
/// <param name="Description">One sentence describing the group, shown on the home page.</param>
/// <param name="Demos">The demos in sidebar order.</param>
/// <param name="Id">The home page section id the jump links point to, for example "merge", or null for none.</param>
/// <param name="Icon">The icon drawn beside the capability's heading on the home page, or null for none.</param>
/// <param name="Tone">The capability's color name on the home page ("blue", "teal", "amber", or "purple"), or null for none.</param>
/// <param name="Snippet">The name of the code sample shown in the capability's home page row, or null for none.</param>
/// <param name="Groups">The named parts the demos are shown in, in the order of <paramref name="Demos" />, or null to show one list.</param>
public sealed record SampleCategory(
    string Title,
    string Description,
    IReadOnlyList<SampleDemo> Demos,
    string? Id = null,
    string? Icon = null,
    string? Tone = null,
    string? Snippet = null,
    IReadOnlyList<SampleGroup>? Groups = null);
