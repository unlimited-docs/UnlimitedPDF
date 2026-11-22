using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// The group a tool sits in on the hub and the home strip.
/// </summary>
public enum ToolGroup
{
    /// <summary>
    /// Tools that make one PDF from several files.
    /// </summary>
    Combine,

    /// <summary>
    /// Tools that take pages out of one PDF.
    /// </summary>
    SplitAndExtract,

    /// <summary>
    /// Tools that turn another format into a PDF.
    /// </summary>
    Convert,
}

/// <summary>
/// One numbered step of a tool's "How to" guide.
/// </summary>
/// <param name="Title">The step's short title, for example "Add your PDFs".</param>
/// <param name="Text">One or two sentences saying what to do.</param>
public sealed record ToolStep(string Title, string Text);

/// <summary>
/// One free tool: what the hub, the home strip, the search, the scenario links, and the tool's own page show about it.
/// </summary>
/// <param name="Slug">The last part of the tool's address, for example "merge".</param>
/// <param name="Name">The tool's short name on cards and links, for example "Merge PDFs".</param>
/// <param name="Heading">The large title on the tool's page, for example "Merge PDF files".</param>
/// <param name="Subtitle">The line under the heading. It is also the page's meta description.</param>
/// <param name="Group">The group the tool sits in.</param>
/// <param name="Description">One sentence for the hub card.</param>
/// <param name="InputOutput">What goes in and what comes out, for example "PDF files · result: 1 PDF".</param>
/// <param name="Icon">The icon name drawn by the Icon component.</param>
/// <param name="PageTitle">The browser tab and search result title before the site name.</param>
/// <param name="Scenarios">The matching scenarios, the main one first. Each links to this tool, and this tool links back.</param>
/// <param name="Steps">The "How to" guide, in order.</param>
/// <param name="SearchWords">Task words the site search matches besides the name, for example "combine" and "join".</param>
public sealed record PdfTool(
    string Slug,
    string Name,
    string Heading,
    string Subtitle,
    ToolGroup Group,
    string Description,
    string InputOutput,
    string Icon,
    string PageTitle,
    IReadOnlyList<SampleDemo> Scenarios,
    IReadOnlyList<ToolStep> Steps,
    IReadOnlyList<string> SearchWords)
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the full page title: <see cref="PageTitle"/> followed by the site name.
    /// </summary>
    public string DocumentTitle => $"{PageTitle} · {SampleDemo.SiteName}";

    /// <summary>
    /// Gets the tool's address without a leading slash, for example "tools/merge".
    /// </summary>
    public string Route => $"{ToolCatalog.HubRoute}/{Slug}";

    /// <summary>
    /// Gets the main matching scenario, the one the tool's page links to.
    /// </summary>
    public SampleDemo Scenario => Scenarios[0];

    #endregion
}
