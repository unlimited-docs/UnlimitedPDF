using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The contributor license agreement, the contributor guide, and the CLA check workflow (PR-026, PR-027).
/// </summary>
public class ContributingFilesTests
{
    #region Fields and Constants

    private const string SignSentence = "I have read the CLA Document and I hereby sign the CLA";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void The_CLA_names_the_owner_its_version_its_grants_and_how_to_sign()
    {
        string cla = Read("CLA.md");

        Assert.StartsWith("# UnlimitedPDF Individual Contributor License Agreement", cla, StringComparison.Ordinal);
        Assert.Contains("\nVersion 1.0\n", cla, StringComparison.Ordinal);
        Assert.Contains("Vikram Reddy (the \"Owner\"), the copyright owner of UnlimitedPDF", cla, StringComparison.Ordinal);
        Assert.Contains("perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable copyright license to reproduce, prepare derivative works of, publicly display, publicly perform, sublicense, and distribute", cla, StringComparison.Ordinal);
        Assert.Contains("## 2a. Other License Terms", cla, StringComparison.Ordinal);
        Assert.Contains("The Owner may also offer Your Contributions, and works that include them, under other license terms.", cla, StringComparison.Ordinal);
        Assert.Contains("You can't withdraw a Contribution once You have submitted it.", cla, StringComparison.Ordinal);
        Assert.Contains("## 3. Grant of Patent License", cla, StringComparison.Ordinal);
        Assert.Contains("that You have received permission to make Contributions on behalf of that employer, or that Your employer has waived such rights", cla, StringComparison.Ordinal);
        Assert.Contains("[support@unlimitedpdf.com](mailto:support@unlimitedpdf.com)", cla, StringComparison.Ordinal);
        Assert.Contains("```\n" + SignSentence + "\n```", cla, StringComparison.Ordinal);
        Assert.Contains("adapted from the Apache Software Foundation's Individual Contributor License Agreement V2.2", cla, StringComparison.Ordinal);
        Assert.DoesNotContain("governed by", cla, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_contributor_guide_has_its_sections_and_links()
    {
        string guide = Read("CONTRIBUTING.md");

        string[] headings = Regex.Matches(guide, "^## (.+)$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["Before you start: sign the CLA", "Opening a pull request", "Build and test", "Code of conduct", "Reporting a security issue"], headings);
        Assert.Contains("[Contributor License Agreement](CLA.md)", guide, StringComparison.Ordinal);
        Assert.Contains("[Code of Conduct](CODE_OF_CONDUCT.md)", guide, StringComparison.Ordinal);
        Assert.Contains("```\n" + SignSentence + "\n```", guide, StringComparison.Ordinal);
        Assert.Contains("dotnet build UnlimitedPDF.slnx\ndotnet test UnlimitedPDF.Tests", guide, StringComparison.Ordinal);
        Assert.Contains("[support@unlimitedpdf.com](mailto:support@unlimitedpdf.com) instead of opening a public issue", guide, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryFile("CODE_OF_CONDUCT.md")) && File.Exists(RepositoryFile("UnlimitedPDF.slnx")), "The guide's links and commands point at real files.");
    }

    [Fact]
    public void The_readme_ends_with_a_contributing_section_listed_in_its_contents()
    {
        string readme = Read("README.md");

        Assert.Equal("Contributing", Regex.Matches(readme, "^## (.+)$", RegexOptions.Multiline).Last().Groups[1].Value);
        Assert.Contains("- [Changes in 1.0.0](#changes-in-100)\n- [Contributing](#contributing)\n", readme, StringComparison.Ordinal);
        Assert.Contains("See [CONTRIBUTING.md](CONTRIBUTING.md) for how to open a pull request. Every contributor signs the [Contributor License Agreement](CLA.md) once, on their first pull request.", ContributingSection(readme), StringComparison.Ordinal);
    }

    [Fact]
    public void The_pull_request_template_reminds_authors_of_the_cla()
    {
        string template = Read(".github/pull_request_template.md");

        Assert.StartsWith("## What and why", template, StringComparison.Ordinal);
        Assert.Contains("- [ ] I have read [CONTRIBUTING.md](https://github.com/unlimited-docs/UnlimitedPDF/blob/main/CONTRIBUTING.md), and I'll sign the CLA when the check asks.", template, StringComparison.Ordinal);
    }

    [Fact]
    public void The_contributor_texts_promise_nothing_about_future_licensing()
    {
        // The texts this agreement added say nothing about later licensing or price, so they stay true whatever the
        // owner decides later (PR-012).
        string[] texts = [Read("CLA.md"), Read("CONTRIBUTING.md"), Read(".github/pull_request_template.md"), ContributingSection(Read("README.md"))];

        foreach (string text in texts)
            foreach (string word in new[] { "commercial", "forever", "paid", "proprietary" })
                Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_project_license_stays_apache_2_0()
    {
        Assert.Contains("Apache License\n                           Version 2.0, January 2004", Read("LICENSE"), StringComparison.Ordinal);
        Assert.Contains("<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>", Read("UnlimitedPDF/UnlimitedPDF.csproj"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_cla_check_runs_on_pull_requests_to_main_and_new_comments_only()
    {
        string workflow = Read(".github/workflows/cla.yml");

        Assert.Contains("on:\n  pull_request_target:\n    types: [opened, synchronize, reopened]\n    branches: [main]\n  issue_comment:\n    types: [created]\n", workflow, StringComparison.Ordinal);
        Assert.Contains("    if: github.event_name == 'pull_request_target' || github.event.issue.pull_request\n", workflow, StringComparison.Ordinal);
        Assert.Contains("const STATUS_CONTEXT = 'CLA';", Script(workflow), StringComparison.Ordinal);
    }

    [Fact]
    public void The_cla_check_has_least_permissions_and_one_pinned_first_party_action()
    {
        // PR-027: a write token on pull requests from forks, so only the permissions it needs and one action pinned
        // by its full commit SHA.
        string workflow = Read(".github/workflows/cla.yml");

        Assert.Contains("\npermissions: {}\n", workflow, StringComparison.Ordinal);
        Assert.Contains("    permissions:\n      contents: write\n      pull-requests: write\n      statuses: write\n    env:", workflow, StringComparison.Ordinal);
        string[] actions = Regex.Matches(workflow, @"^\s*(?:-\s*)?uses:\s*(\S+)", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["actions/github-script@3a2844b7e9c422d3c10d287c895573f7108da1b3"], actions);
    }

    [Fact]
    public void The_cla_check_never_runs_pull_request_code_or_expands_event_text_in_its_script()
    {
        string workflow = Read(".github/workflows/cla.yml");
        string script = Script(workflow);

        Assert.DoesNotContain("actions/checkout", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("run:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("${{", script, StringComparison.Ordinal);
        Assert.Contains("const body = (context.payload.comment.body || '').trim();", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_cla_check_version_matches_the_cla()
    {
        // A new CLA version starts a new signature file, so contributors sign it again.
        string version = Regex.Match(Read(".github/workflows/cla.yml"), @"^\s+CLA_VERSION: v(\d+\.\d+)$", RegexOptions.Multiline).Groups[1].Value;

        Assert.Equal("1.0", version);
        Assert.Contains($"\nVersion {version}\n", Read("CLA.md"), StringComparison.Ordinal);
        Assert.Contains("const PATH = `signatures/${VERSION}/cla.json`;", Script(Read(".github/workflows/cla.yml")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_cla_check_asks_for_the_same_sentence_as_the_documents_and_exempts_the_owner_and_bots()
    {
        string script = Script(Read(".github/workflows/cla.yml"));

        Assert.Contains($"const SIGN_SENTENCE = '{SignSentence}';", script, StringComparison.Ordinal);
        Assert.Contains("const BRANCH = 'cla-signatures';", script, StringComparison.Ordinal);
        Assert.Contains("const EXEMPT = ['gvreddy04'];", script, StringComparison.Ordinal);
        Assert.Contains("const isExempt = login => EXEMPT.includes(login) || login.endsWith('[bot]');", script, StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string ContributingSection(string readme) => readme[readme.LastIndexOf("\n## Contributing\n", StringComparison.Ordinal)..];

    private static string Read(string relative) => File.ReadAllText(RepositoryFile(relative)).Replace("\r\n", "\n");

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    // The inline script of the github-script step: the lines indented under "script: |".
    private static string Script(string workflow)
    {
        string[] lines = workflow.Split('\n');
        int start = Array.FindIndex(lines, l => l.TrimEnd() == "          script: |");
        Assert.True(start >= 0, "The workflow has an inline script.");
        return string.Join('\n', lines.Skip(start + 1).TakeWhile(l => l.Length == 0 || l.StartsWith("            ", StringComparison.Ordinal)));
    }

    #endregion

    #endregion
}
