namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// A named part of a category's demos, shown under its own small title, for example "Packs" under Merge.
/// </summary>
/// <param name="Title">The group name.</param>
/// <param name="Demos">The group's demos, in the category's order.</param>
public sealed record SampleGroup(string Title, IReadOnlyList<SampleDemo> Demos);
