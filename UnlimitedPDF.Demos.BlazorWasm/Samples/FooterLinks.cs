using UnlimitedPDF.Demos.BlazorWasm.Services;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One link in the site footer.
/// </summary>
/// <param name="Text">The link text: what the page does, in the words people search with.</param>
/// <param name="Href">The address, relative to the site, or a full address for another site.</param>
/// <param name="External">Whether the link opens another site in a new tab.</param>
public sealed record FooterLink(string Text, string Href, bool External = false);

/// <summary>
/// One column of the site footer.
/// </summary>
/// <param name="Title">The column heading.</param>
/// <param name="Links">The links, in the order shown.</param>
public sealed record FooterColumn(string Title, IReadOnlyList<FooterLink> Links);

/// <summary>
/// The site footer's links, on every page (QF-26112201): the common C# tasks by the words people search with, the
/// languages, the free tools, and the site's other pages and project links. Every address comes from the
/// catalogs, so a link cannot point to a page that does not exist.
/// </summary>
public static class FooterLinks
{
    #region Fields and Constants

    /// <summary>
    /// The project's source repository.
    /// </summary>
    public const string GitHubUrl = "https://github.com/unlimited-docs/UnlimitedPDF";

    /// <summary>
    /// The package's page on NuGet.
    /// </summary>
    public const string NuGetUrl = "https://www.nuget.org/packages/UnlimitedPDF";

    private static IReadOnlyList<FooterColumn>? _columns;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the footer's columns, in the order shown.
    /// </summary>
    public static IReadOnlyList<FooterColumn> Columns => _columns ??=
    [
        new("C# guides",
        [
            new("Create a PDF in C#", SampleCatalog.TextAndPages.Route),
            new("Invoice PDF in C#", SampleCatalog.Invoice.Route),
            new("Add a table to a PDF", SampleCatalog.Tables.Route),
            new("Add images to a PDF", SampleCatalog.Images.Route),
            new("Headers and footers in a PDF", SampleCatalog.HeadersFooters.Route),
            new("Merge PDF files in C#", SampleCatalog.JoinFiles.Route),
            new("Split a PDF in C#", SampleCatalog.SplitEveryN.Route),
            new("Convert HTML to PDF in C#", SampleCatalog.HtmlToPdf.Route),
        ]),
        new("Languages",
        [
            .. LanguageInvoices.All.Select(language => new FooterLink(LanguageText(language), LanguageRoute(language))),
            new("Use your own fonts", SampleCatalog.FontsAndLanguages.Route),
        ]),
        new(ToolCatalog.HubHeading,
        [
            .. ToolCatalog.All.Select(tool => new FooterLink(tool.Name, tool.Route)),
            new("All tools", ToolCatalog.HubRoute),
        ]),
        new("Resources",
        [
            new($"All {ScenarioGallery.Scenarios.Count} scenarios", ScenarioGallery.Route),
            new("Real-life documents", $"{ScenarioGallery.Route}?capability=documents"),
            new("FAQ", "faq"),
            new("Release notes", LibraryRelease.NotesUrl, External: true),
            new("NuGet package", NuGetUrl, External: true),
            new("GitHub", GitHubUrl, External: true),
        ]),
    ];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the address that opens the languages demo with a language's invoice.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The demo's address with the language in its query.</returns>
    public static string LanguageRoute(LanguageInvoice language) => $"{SampleCatalog.FontsAndLanguages.Route}?language={language.Id}";

    #endregion

    #region Private Methods

    private static string LanguageText(LanguageInvoice language) =>
        language.Id == "mixed" ? "Mixed-language PDF" : $"PDF in {language.Name}";

    #endregion

    #endregion
}
