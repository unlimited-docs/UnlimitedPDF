namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The FAQ page's search: which questions stay visible for the text a visitor types.
/// </summary>
public static class FaqSearch
{
    #region Fields and Constants

    private static readonly char[] Separators = [' ', '\t', '\r', '\n'];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns whether a question matches the typed text: every word of it occurs in the question or in one of the
    /// answer's paragraphs, ignoring case. Empty or blank text matches every question.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="query">The typed text, or null.</param>
    /// <returns><see langword="true"/> when the question stays visible.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is null.</exception>
    public static bool Matches(FaqQuestion question, string? query)
    {
        ArgumentNullException.ThrowIfNull(question);

        var words = Words(query);
        return words.Length == 0 || words.All(word => Contains(question.Text, word) || question.Answer.Any(paragraph => Contains(paragraph, word)));
    }

    /// <summary>
    /// Returns the words of the typed text.
    /// </summary>
    /// <param name="query">The typed text, or null.</param>
    /// <returns>The words, without empty entries; empty when the text is null or blank.</returns>
    public static string[] Words(string? query) =>
        string.IsNullOrWhiteSpace(query) ? [] : query.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    #endregion

    #region Private Methods

    private static bool Contains(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);

    #endregion

    #endregion
}
