namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// A group of playground demos, shown as one sidebar section, for example "Create".
/// </summary>
/// <param name="Title">The group name.</param>
/// <param name="Description">One sentence describing the group, shown on the home page.</param>
/// <param name="Demos">The demos in sidebar order.</param>
public sealed record SampleCategory(string Title, string Description, IReadOnlyList<SampleDemo> Demos);
