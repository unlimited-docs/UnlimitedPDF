namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One question of the FAQ page and its answer.
/// </summary>
/// <param name="Id">The question's stable link name, for example "merge-two-pdfs"; the page links to it as <c>faq#merge-two-pdfs</c>.</param>
/// <param name="Text">The question as the page shows it.</param>
/// <param name="Answer">The answer's paragraphs, as plain text.</param>
/// <param name="Demo">The demo that shows the answer, or null when none does.</param>
/// <param name="Snippet">The name of the answer's code sample in <c>FaqSnippets.cs</c>, or null when the answer has no code.</param>
/// <param name="NotYet">Whether the question asks about something the library does not do today; its answer then starts with "Not yet.".</param>
public sealed record FaqQuestion(string Id, string Text, IReadOnlyList<string> Answer, SampleDemo? Demo = null, string? Snippet = null, bool NotYet = false);
