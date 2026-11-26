using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Reads the repository's readmes, guides (<c>docs/guides</c>), and changelog, so tests can check that a guide and the
/// package readme show the same text (QF-26112603).
/// </summary>
internal static class RepositoryDocs
{
    #region Methods

    #region Public Methods

    /// <summary>A guide's text after its title and link line.</summary>
    public static string GuideBody(string file)
    {
        string text = Read(Path.Combine("docs", "guides", file));
        int title = text.IndexOf("\n\n", StringComparison.Ordinal);
        int links = text.IndexOf("\n\n", title + 2, StringComparison.Ordinal);
        Assert.True(title > 0 && links > title, $"The guide {file} starts with a title and a link line.");
        return text[(links + 2)..].TrimEnd('\n');
    }

    /// <summary>A file of the repository, with LF line endings.</summary>
    public static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The text of the section under <paramref name="heading" />, without the heading line, up to the next level 2 heading.</summary>
    public static string SectionBody(string text, string heading)
    {
        int start = text.IndexOf(heading + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The section {heading} is missing.");
        start += heading.Length + 1;
        int end = text.IndexOf("\n## ", start, StringComparison.Ordinal);
        return text[start..(end < 0 ? text.Length : end)].Trim('\n');
    }

    /// <summary>Text with every link target removed, so a guide's links to other guides match the package readme's in-page anchors.</summary>
    public static string WithoutLinkTargets(string text) => Regex.Replace(text, @"\]\([^)\s]*\)", "]()");

    #endregion

    #region Private Methods

    private static string Root([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, ".."));

    #endregion

    #endregion
}
