using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// What a search result points at.
/// </summary>
public enum SearchKind
{
    /// <summary>
    /// A demo page.
    /// </summary>
    Demo,

    /// <summary>
    /// A free tool's page.
    /// </summary>
    Tool,

    /// <summary>
    /// An FAQ answer.
    /// </summary>
    Faq,

    /// <summary>
    /// A line of a demo's C# code.
    /// </summary>
    Code,
}

/// <summary>
/// One result of the site-wide search.
/// </summary>
/// <param name="Kind">What the result points at.</param>
/// <param name="Title">The result's name: a demo's title, an FAQ question, or the demo whose code matched.</param>
/// <param name="Detail">One line under the title: a demo's description, the answer's start, or the matching code line.</param>
/// <param name="Address">The page the result opens, without a leading slash.</param>
public sealed record SearchResult(SearchKind Kind, string Title, string Detail, string Address);

/// <summary>
/// One piece of a shown text: plain, or a part the query matched. Pieces are shown as text, never as markup.
/// </summary>
/// <param name="Text">The piece's text.</param>
/// <param name="IsMatch">Whether the query matched this piece.</param>
public readonly record struct TextPart(string Text, bool IsMatch);

/// <summary>
/// The site-wide search in the top bar: demos, free tools, FAQ answers, and the demos' C# code. A query is a few words, each
/// matched as plain text, ignoring case; a result holds every word.
/// </summary>
public static class SiteSearch
{
    #region Fields and Constants

    /// <summary>
    /// The longest detail line shown, before the ellipses that mark a cut.
    /// </summary>
    public const int MaxDetailLength = 110;

    /// <summary>
    /// The most results shown for each kind.
    /// </summary>
    public const int MaxPerKind = 5;

    /// <summary>
    /// The longest query read; the rest is ignored.
    /// </summary>
    public const int MaxQueryLength = 100;

    /// <summary>
    /// The most words of a query used.
    /// </summary>
    public const int MaxWords = 8;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Finds the demos, FAQ answers, and code lines that hold every word of a query.
    /// </summary>
    /// <param name="query">What the visitor typed.</param>
    /// <param name="source">Reads a demo's sample source by file name (see <see cref="SampleDemo.SourceFile"/>).</param>
    /// <returns>Up to <see cref="MaxPerKind"/> demos, then tools, then FAQ answers, then code lines; empty for an empty query.</returns>
    public static IReadOnlyList<SearchResult> Find(string? query, Func<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var words = Words(query);
        if (words.Length == 0) return [];

        var results = new List<SearchResult>();
        results.AddRange(SampleCatalog.Pages
            .Where(demo => words.All(word => Contains(DemoText(demo), word)))
            .OrderBy(demo => words.All(word => Contains(demo.Title, word)) ? 0 : 1)
            .Take(MaxPerKind)
            .Select(demo => new SearchResult(SearchKind.Demo, demo.Title, demo.Description, demo.Route)));

        results.AddRange(ToolCatalog.All
            .Where(tool => words.All(word => Contains(ToolText(tool), word)))
            .OrderBy(tool => words.All(word => Contains(tool.Name, word)) ? 0 : 1)
            .Take(MaxPerKind)
            .Select(tool => new SearchResult(SearchKind.Tool, tool.Name, tool.Description, tool.Route)));

        results.AddRange(FaqQuestions.Questions
            .Where(question => words.All(word => Contains(question.Text, word) || question.Answer.Any(paragraph => Contains(paragraph, word))))
            .OrderBy(question => words.All(word => Contains(question.Text, word)) ? 0 : 1)
            .Take(MaxPerKind)
            .Select(question => new SearchResult(SearchKind.Faq, question.Text, Shorten(question.Answer.FirstOrDefault() ?? string.Empty, null), FaqAddress(question.Id))));

        foreach (var demo in SampleCatalog.Pages.DistinctBy(demo => demo.SourceFile))
        {
            if (results.Count(r => r.Kind == SearchKind.Code) == MaxPerKind) break;
            string code = source(demo.SourceFile);
            if (!words.All(word => Contains(code, word))) continue;
            // The line holding the most of the words; the first such line when several hold as many.
            string line = code.Split('\n').Select(l => l.Trim()).MaxBy(l => words.Count(word => Contains(l, word)))!;
            string word = words.First(w => Contains(line, w));
            results.Add(new SearchResult(SearchKind.Code, demo.Title, Shorten(line, word), demo.Route));
        }

        return results;
    }

    /// <summary>
    /// Returns the address of an FAQ answer. The query makes it a move to another address even from the FAQ itself,
    /// so the FAQ page hears it and opens the answer; the fragment names the question.
    /// </summary>
    /// <param name="questionId">The question's id.</param>
    /// <returns>The address without a leading slash.</returns>
    public static string FaqAddress(string questionId) => $"faq?open={Uri.EscapeDataString(questionId)}#{questionId}";

    /// <summary>
    /// Splits a text into the parts a query matches and the parts between them, for highlighting.
    /// </summary>
    /// <param name="text">The shown text.</param>
    /// <param name="query">What the visitor typed.</param>
    /// <returns>The text in order, as plain and matched parts; one plain part when nothing matches.</returns>
    public static IReadOnlyList<TextPart> Highlight(string text, string? query)
    {
        ArgumentNullException.ThrowIfNull(text);
        var marked = new bool[text.Length];
        foreach (var word in Words(query))
        {
            for (int at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
                Array.Fill(marked, true, at, word.Length);
        }

        var parts = new List<TextPart>();
        int start = 0;
        for (int i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && marked[i] == marked[start]) continue;
            parts.Add(new TextPart(text[start..i], marked[start]));
            start = i;
        }

        return parts.Count == 0 ? [new TextPart(text, false)] : parts;
    }

    /// <summary>
    /// Splits a query into its words: at most <see cref="MaxWords"/>, from its first <see cref="MaxQueryLength"/>
    /// characters.
    /// </summary>
    /// <param name="query">What the visitor typed.</param>
    /// <returns>The words, without repeats; empty for a blank query.</returns>
    public static string[] Words(string? query)
    {
        if (query is { Length: > MaxQueryLength }) query = query[..MaxQueryLength];
        return FaqSearch.Words(query).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxWords).ToArray();
    }

    #endregion

    #region Private Methods

    private static bool Contains(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);

    // What a demo is found by: its names, description, business domains, and capability.
    // What a tool is found by: its name, heading, description, and task words (26112201 AC-12).
    private static string ToolText(PdfTool tool) =>
        string.Join(' ', [tool.Name, tool.Heading, tool.Description, .. tool.SearchWords]);

    private static string DemoText(SampleDemo demo) =>
        string.Join(' ', [demo.Title, demo.Heading ?? string.Empty, demo.PageTitle, demo.Description, .. demo.Domains, ScenarioGallery.CategoryOf(demo).Title]);

    // One line of at most MaxDetailLength characters, around the word when the line is longer.
    private static string Shorten(string text, string? word)
    {
        text = text.Trim();
        if (text.Length <= MaxDetailLength) return text;
        int at = word is null ? 0 : Math.Max(0, text.IndexOf(word, StringComparison.OrdinalIgnoreCase));
        int start = Math.Clamp(at - 30, 0, text.Length - MaxDetailLength);
        return (start > 0 ? "…" : string.Empty) + text.Substring(start, MaxDetailLength).Trim() + (start + MaxDetailLength < text.Length ? "…" : string.Empty);
    }

    #endregion

    #endregion
}
