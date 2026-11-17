namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The scenario gallery (<c>/playground</c>): every demo as a card, filtered by capability and by business domain.
/// Both filters are kept in the address (<c>?capability=merge&amp;domain=finance</c>), so a filtered view can be
/// linked to.
/// </summary>
public static class ScenarioGallery
{
    #region Fields and Constants

    /// <summary>
    /// The gallery's address, without a leading slash.
    /// </summary>
    public const string Route = "playground";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the gallery's address for a capability and a domain.
    /// </summary>
    /// <param name="capability">A category id (see <see cref="SampleCategory.Id"/>), or null for every capability.</param>
    /// <param name="domain">A domain from <see cref="Domains"/>, or null for every domain.</param>
    /// <returns>The address without a leading slash; no query when neither filter is set.</returns>
    public static string Address(string? capability, string? domain)
    {
        var query = new List<string>();
        if (capability is not null) query.Add("capability=" + Uri.EscapeDataString(capability));
        if (domain is not null) query.Add("domain=" + Uri.EscapeDataString(domain.ToLowerInvariant()));
        return query.Count == 0 ? Route : Route + "?" + string.Join("&", query);
    }

    /// <summary>
    /// Returns the category a demo is listed under; the Invoice demo's other document types belong to its category.
    /// </summary>
    /// <param name="demo">A demo page from <see cref="SampleCatalog.Pages"/>.</param>
    /// <returns>The category.</returns>
    /// <exception cref="ArgumentException">The demo is not in the catalog.</exception>
    public static SampleCategory CategoryOf(SampleDemo demo)
    {
        var listed = SampleCatalog.InvoiceTypes.Contains(demo) ? SampleCatalog.Invoice : demo;
        return SampleCatalog.Categories.FirstOrDefault(c => c.Demos.Contains(listed))
            ?? throw new ArgumentException($"'{demo.Title}' is not in the catalog.", nameof(demo));
    }

    /// <summary>
    /// Returns the business domains a scenario's card shows and is filtered by: its own, and for the Invoice demo
    /// those of every document type it makes.
    /// </summary>
    /// <param name="demo">A scenario.</param>
    /// <returns>The domains in <see cref="Domains"/> order.</returns>
    public static IReadOnlyList<string> DomainsOf(SampleDemo demo)
    {
        var all = demo == SampleCatalog.Invoice ? demo.Domains.Concat(SampleCatalog.InvoiceTypes.SelectMany(t => t.Domains)) : demo.Domains;
        var set = all.ToHashSet(StringComparer.Ordinal);
        return Domains.Where(set.Contains).ToList();
    }

    /// <summary>
    /// Returns the scenarios a capability and a domain select.
    /// </summary>
    /// <param name="capability">A category id, or null for every capability.</param>
    /// <param name="domain">A domain, or null for every domain.</param>
    /// <returns>The matching scenarios in catalog order.</returns>
    public static IReadOnlyList<SampleDemo> Filter(string? capability, string? domain) =>
        Scenarios
            .Where(demo => capability is null || CategoryOf(demo).Id == capability)
            .Where(demo => domain is null || DomainsOf(demo).Contains(domain))
            .ToList();

    /// <summary>
    /// Reads the capability from the address, ignoring case.
    /// </summary>
    /// <param name="value">The address's capability value.</param>
    /// <returns>The category id, or null for a missing or unknown value.</returns>
    public static string? FindCapability(string? value) =>
        SampleCatalog.Categories.Select(c => c.Id).FirstOrDefault(id => id is not null && string.Equals(id, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads the domain from the address, ignoring case.
    /// </summary>
    /// <param name="value">The address's domain value.</param>
    /// <returns>The domain as <see cref="Domains"/> spells it, or null for a missing or unknown value.</returns>
    public static string? FindDomain(string? value) =>
        Domains.FirstOrDefault(d => string.Equals(d, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the base name of a scenario's picture in <c>images/gallery/</c>, the first page of the PDF it makes.
    /// </summary>
    /// <param name="demo">A scenario.</param>
    /// <returns>The picture's base name, or null for a scenario without one, which shows its capability's icon.</returns>
    public static string? PictureOf(SampleDemo demo) => DocumentGallery.Documents.FirstOrDefault(d => d.Demo == demo)?.Picture;

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the business domains, in the order the gallery's chips show them.
    /// </summary>
    public static IReadOnlyList<string> Domains { get; } = ["Finance", "HR", "Healthcare", "Retail", "Logistics", "General"];

    /// <summary>
    /// Gets the scenarios the home page shows first: the documents and tasks developers ask about most.
    /// </summary>
    public static IReadOnlyList<SampleDemo> Popular =>
    [
        SampleCatalog.Invoice, SampleCatalog.AccountStatement, SampleCatalog.DocumentPacks,
        SampleCatalog.HtmlToPdf, SampleCatalog.FontsAndLanguages, SampleCatalog.Payslip,
    ];

    /// <summary>
    /// Gets every scenario in catalog order: each demo of the catalog once. The Invoice demo's other document types are
    /// part of its card.
    /// </summary>
    public static IReadOnlyList<SampleDemo> Scenarios => SampleCatalog.Categories.SelectMany(c => c.Demos).Distinct().ToList();

    #endregion
}
