using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The package readme (QF-26111701): NuGet.org renders Markdown only and shows HTML tags as text, so the readme packed
/// into the package uses no HTML outside code.
/// </summary>
public class NugetReadmeTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void The_package_readme_has_no_html_outside_code()
    {
        string readme = File.ReadAllText(RepositoryFile("nuget/README.md"));

        // Fenced blocks and inline code may show HTML as an example; everything else must be Markdown.
        string prose = Regex.Replace(readme, "```.*?```", string.Empty, RegexOptions.Singleline);
        prose = Regex.Replace(prose, "`[^`\n]*`", string.Empty);
        var tags = Regex.Matches(prose, @"</?[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?/?>").Select(match => match.Value).ToList();

        Assert.Empty(tags);
    }

    #endregion

    #region Private Methods

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    #endregion

    #endregion
}
