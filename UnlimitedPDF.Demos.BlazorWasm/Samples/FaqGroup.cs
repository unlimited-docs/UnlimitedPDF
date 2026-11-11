namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One topic of the FAQ page and its questions.
/// </summary>
/// <param name="Id">The topic's link name on the page, for example "merge-and-split"; it is also the section's HTML ID.</param>
/// <param name="Title">The topic heading, for example "Merge and split".</param>
/// <param name="Questions">The topic's questions in the order the page shows them.</param>
public sealed record FaqGroup(string Id, string Title, IReadOnlyList<FaqQuestion> Questions);
