namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Reads one code sample out of a source file: the lines between a <c>// snippet:&lt;name&gt;</c> marker and the
/// next <c>// end</c> marker, without their common indentation.
/// </summary>
public static class SnippetText
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Gets the lines of one code sample.
    /// </summary>
    /// <param name="source">The source file's text.</param>
    /// <param name="name">The sample name, for example "merge".</param>
    /// <returns>The sample's lines joined with "\n", without a trailing line break.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="name" /> is null.</exception>
    /// <exception cref="InvalidOperationException">The sample is missing, has no end marker, or is empty.</exception>
    public static string Extract(string source, string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(name);

        var lines = source.Replace("\r\n", "\n").Split('\n');
        int start = Array.FindIndex(lines, line => line.Trim() == $"// snippet:{name}");
        if (start < 0) throw new InvalidOperationException($"The code sample '{name}' is not in the source.");

        int end = Array.FindIndex(lines, start + 1, line => line.Trim() == "// end");
        if (end < 0) throw new InvalidOperationException($"The code sample '{name}' has no end marker.");

        var body = lines[(start + 1)..end];
        if (body.All(string.IsNullOrWhiteSpace)) throw new InvalidOperationException($"The code sample '{name}' is empty.");

        int indent = body.Where(line => line.Trim().Length > 0).Min(line => line.Length - line.TrimStart().Length);
        return string.Join("\n", body.Select(line => line.Length >= indent ? line[indent..].TrimEnd() : line.Trim()));
    }

    #endregion

    #endregion
}
